from __future__ import annotations

import json
from statistics import mean
from typing import Any

from .config import Settings
from .cv_parser import normalize_cv_profile, profile_search_query, profile_to_context
from .ollama_client import OllamaClient
from .prompts import (
    EVALUATION_SYSTEM,
    FOLLOWUP_SYSTEM,
    QUESTION_SYSTEM,
    difficulty_plan,
    evaluation_prompt,
    followup_prompt,
    question_prompt,
)
from .scoring import RubricScorer
from .vector_store import VectorStore


class InterviewService:
    """Select/evaluate interviews while keeping uploaded CVs outside Qdrant."""

    def __init__(self, settings: Settings):
        self.settings = settings
        self.store = VectorStore(settings)
        self.ollama = OllamaClient(settings)
        self.scorer = RubricScorer(settings.rubric_path)

    @staticmethod
    def normalize_profile(cv_profile: dict[str, Any]) -> dict[str, Any]:
        return normalize_cv_profile(cv_profile)

    @staticmethod
    def _payload_value(payload: dict[str, Any], aliases: tuple[str, ...]) -> str:
        lowered = {str(key).strip().lower(): value for key, value in payload.items()}
        for alias in aliases:
            value = lowered.get(alias.lower())
            if value not in (None, ""):
                return str(value).strip()
        return ""

    @classmethod
    def _matches_difficulty(cls, payload: dict[str, Any], level: str) -> bool:
        actual = cls._payload_value(
            payload,
            ("difficulty", "level", "difficulty_level", "level_tags"),
        ).lower()
        if not actual:
            return False
        expected = level.lower()
        aliases = {
            "easy": {"easy", "beginner", "basic", "dễ"},
            "medium": {"medium", "intermediate", "trung bình", "vừa"},
            "hard": {"hard", "advanced", "difficult", "khó"},
        }
        return actual in aliases.get(expected, {expected}) or expected in actual

    @classmethod
    def _matches_language(cls, payload: dict[str, Any], language: str) -> bool:
        actual = cls._payload_value(payload, ("language", "lang", "locale")).lower()
        if not actual:
            return True  # Existing collections may contain only one language and omit the field.
        expected = language.lower()
        return actual == expected or actual.startswith(expected + "-")

    def _retrieve_templates_by_difficulty(
        self,
        query: str,
        interview_type: str,
        language: str,
        count: int,
    ) -> dict[str, list[dict[str, Any]]]:
        collection = self.settings.collection_for(interview_type)
        if not self.store.collection_exists(collection):
            raise ValueError(
                f"Qdrant collection '{collection}' does not exist. "
                "Check TECHNICAL_COLLECTION/BEHAVIORAL_COLLECTION in .env."
            )

        grouped: dict[str, list[dict[str, Any]]] = {}
        per_level = max(1, self.settings.templates_per_difficulty)

        for level in dict.fromkeys(difficulty_plan(count)):
            # The difficulty is also included in semantic search, so the system still
            # works when the existing collection has no payload indexes.
            level_query = f"{query}\nDifficulty: {level}"
            hits = self.store.query_with_filter_fallback(
                collection,
                level_query,
                limit=max(per_level * 3, 10),
                filter_candidates=[
                    {"language": language, "difficulty": level},
                    {"difficulty": level},
                    {"language": language},
                ],
            )
            payloads = [item["payload"] for item in hits]
            exact = [
                payload
                for payload in payloads
                if self._matches_language(payload, language)
                and self._matches_difficulty(payload, level)
            ]
            language_only = [
                payload for payload in payloads if self._matches_language(payload, language)
            ]
            grouped[level] = (exact or language_only or payloads)[:per_level]

        if not any(grouped.values()):
            fallback_hits = self.store.query_with_filter_fallback(
                collection,
                query,
                limit=self.settings.top_k_questions,
                filter_candidates=[{"language": language}],
            )
            grouped["Fallback"] = [item["payload"] for item in fallback_hits]
        return grouped

    @staticmethod
    def _compact_template(payload: dict[str, Any]) -> dict[str, Any]:
        """Keep only fields that help generate a question.

        Qdrant payloads include ingestion metadata and detailed scoring rubrics.
        Those fields are useful elsewhere but needlessly inflate the LLM prompt.
        """
        aliases = {
            "id": ("id", "source_id"),
            "difficulty": ("difficulty", "level", "difficulty_level"),
            "skill": ("skill", "subskill", "skill_or_competency"),
            "question": ("question_text", "question"),
            "expected_answer": ("expected_answer",),
            "expected_key_points": ("expected_key_points",),
            "follow_ups": ("follow_up_1", "follow_up_2", "clarification_question"),
        }
        compact: dict[str, Any] = {}
        for target, candidates in aliases.items():
            values = [payload[key] for key in candidates if payload.get(key) not in (None, "", [])]
            if not values:
                continue
            compact[target] = values if target == "follow_ups" else values[0]
        return compact

    def _template_context(self, grouped_templates: dict[str, list[dict[str, Any]]]) -> str:
        compact = {
            level: [self._compact_template(payload) for payload in templates]
            for level, templates in grouped_templates.items()
        }
        return json.dumps(compact, ensure_ascii=False, default=str)[: self.settings.template_context_chars]

    @staticmethod
    def _question_from_template(
        payload: dict[str, Any],
        *,
        interview_type: str,
        difficulty: str,
    ) -> dict[str, Any]:
        """Resolve a question-bank payload into the runtime question contract.

        This deliberately does not ask the LLM to rewrite question text.  The
        bank is the authority for question IDs/text; a fine-tuned model is used
        later for answer evaluation only.
        """
        question_id = InterviewService._payload_value(payload, ("id", "source_id"))
        question_text = InterviewService._payload_value(payload, ("question_text", "question"))
        if not question_id or not question_text:
            raise ValueError("Question-bank record must contain id and question_text")

        raw_followups = [
            payload[key]
            for key in ("follow_up_1", "follow_up_2", "clarification_question")
            if payload.get(key) not in (None, "", [])
        ]
        if not raw_followups and isinstance(payload.get("follow_ups"), list):
            raw_followups = payload["follow_ups"]

        expected_key_points = payload.get("expected_key_points", [])
        if isinstance(expected_key_points, str):
            expected_key_points = [item.strip() for item in expected_key_points.split(";") if item.strip()]

        return {
            "question_id": question_id,
            "source_template_id": question_id,
            "interview_type": interview_type,
            "question": question_text,
            "difficulty": InterviewService._payload_value(
                payload, ("difficulty", "level", "difficulty_level")
            ) or difficulty,
            "skill_or_competency": InterviewService._payload_value(
                payload, ("skill", "subskill", "skill_or_competency")
            ),
            "expected_answer": payload.get("expected_answer", ""),
            "expected_key_points": expected_key_points,
            "follow_ups": [str(item).strip() for item in raw_followups if str(item).strip()],
        }

    def select_main_questions(
        self,
        cv_profile: dict[str, Any],
        interview_type: str = "technical",
        count: int = 3,
        language: str = "vi",
    ) -> list[dict[str, Any]]:
        """Preselect and cache main question IDs without an LLM generation call."""
        interview_type = interview_type.lower()
        language = language.lower()
        if interview_type not in {"technical", "behavioral"}:
            raise ValueError("interview_type must be technical or behavioral")
        if language not in {"vi", "en"}:
            raise ValueError("language must be vi or en")
        if count < 1 or count > 3:
            raise ValueError("count must be between 1 and 3 for the adaptive demo")

        normalized = normalize_cv_profile(cv_profile)
        query = profile_search_query(normalized, interview_type)
        grouped_templates = self._retrieve_templates_by_difficulty(
            query, interview_type, language, count
        )
        selected: list[dict[str, Any]] = []
        seen_ids: set[str] = set()
        fallback = grouped_templates.get("Fallback", [])
        for difficulty in difficulty_plan(count):
            candidates = grouped_templates.get(difficulty, []) or fallback
            for payload in candidates:
                question = self._question_from_template(
                    payload, interview_type=interview_type, difficulty=difficulty
                )
                if question["question_id"] not in seen_ids:
                    question["difficulty"] = difficulty
                    selected.append(question)
                    seen_ids.add(question["question_id"])
                    break
            else:
                raise ValueError(f"No unique {difficulty} question available in the question bank")
        return selected

    def retrieve_questions(
        self,
        cv_profile: dict[str, Any],
        interview_type: str = "technical",
        count: int = 3,
        language: str = "vi",
    ) -> list[dict[str, Any]]:
        """Retrieve raw question-bank records from Qdrant without calling any LLM."""
        interview_type = interview_type.lower()
        language = language.lower()
        if interview_type not in {"technical", "behavioral"}:
            raise ValueError("interview_type must be technical or behavioral")
        if language not in {"vi", "en"}:
            raise ValueError("language must be vi or en")

        normalized = normalize_cv_profile(cv_profile)
        query = profile_search_query(normalized, interview_type)
        grouped_templates = self._retrieve_templates_by_difficulty(
            query, interview_type, language, count
        )

        results: list[dict[str, Any]] = []
        seen_ids: set[str] = set()

        for level, templates in grouped_templates.items():
            for payload in templates:
                is_active = payload.get("is_active")
                if is_active is False or is_active == 0 or str(is_active).lower() == "false":
                    continue

                q_id = str(self._payload_value(payload, ("id", "source_id"))).strip()
                q_text = self._payload_value(payload, ("question_text", "question"))
                if not q_id or not q_text or not str(q_text).strip():
                    continue

                if q_id in seen_ids:
                    continue

                raw_kp = payload.get("expected_key_points")
                if isinstance(raw_kp, list):
                    expected_key_points = [str(item).strip() for item in raw_kp if str(item).strip()]
                elif isinstance(raw_kp, str) and raw_kp.strip():
                    kp_str = raw_kp.strip()
                    if ";" in kp_str:
                        expected_key_points = [item.strip() for item in kp_str.split(";") if item.strip()]
                    elif "," in kp_str:
                        expected_key_points = [item.strip() for item in kp_str.split(",") if item.strip()]
                    else:
                        expected_key_points = [kp_str]
                else:
                    expected_key_points = []

                seen_ids.add(q_id)
                results.append({
                    "id": q_id,
                    "question_text": str(q_text).strip(),
                    "skill": self._payload_value(payload, ("skill", "skill_or_competency")) or "",
                    "subskill": self._payload_value(payload, ("subskill",)) or "",
                    "difficulty": self._payload_value(payload, ("difficulty", "level")) or level,
                    "language": self._payload_value(payload, ("language", "lang")) or language,
                    "expected_answer": payload.get("expected_answer") or "",
                    "expected_key_points": expected_key_points,
                    "clarification_question": payload.get("clarification_question") or "",
                    "follow_up_1": payload.get("follow_up_1") or "",
                    "follow_up_2": payload.get("follow_up_2") or "",
                    "experience_level": self._payload_value(payload, ("experience_level", "level")) or "",
                    "is_active": True,
                })
                if len(results) >= count:
                    break
            if len(results) >= count:
                break

        return results


    @staticmethod
    def select_follow_up(
        *,
        main_question: dict[str, Any],
        evaluation: dict[str, Any],
        followup_number: int,
        language: str,
    ) -> dict[str, Any]:
        """Resolve a deterministic follow-up without a second model call."""
        candidates = main_question.get("follow_ups") or []
        text = candidates[followup_number - 1] if followup_number <= len(candidates) else ""
        if not str(text).strip():
            missing = evaluation.get("missing_key_points") or evaluation.get("improvements") or []
            target = str(missing[0]).strip() if missing else "chi tiết này"
            text = (
                f"Bạn có thể giải thích rõ hơn về {target} không?"
                if language.lower() == "vi"
                else f"Could you explain {target} in more detail?"
            )
        return {
            "question_id": f"{main_question.get('question_id', 'main')}:follow-up:{followup_number}",
            "parent_main_question_id": main_question.get("question_id"),
            "interview_type": main_question.get("interview_type", "technical"),
            "question": str(text).strip(),
            "difficulty": main_question.get("difficulty", "Medium"),
            "skill_or_competency": main_question.get("skill_or_competency", ""),
            "target_reason": "deterministic-question-bank-follow-up",
        }

    @staticmethod
    def _postprocess_evaluation(
        evaluation: dict[str, Any], question: dict[str, Any]
    ) -> dict[str, Any]:
        """Validate model key-point claims and flag score/coverage conflicts.

        The LLM supplies semantic judgement.  This method keeps IDs valid and
        exposes consistency warnings without replacing the model's assessment.
        """
        raw_key_points = question.get("expected_key_points") or []
        if isinstance(raw_key_points, str):
            raw_key_points = [item.strip() for item in raw_key_points.split(";") if item.strip()]
        key_points = [
            str(value).strip()
            for value in raw_key_points
            if str(value).strip()
        ]
        catalog = {f"KP-{index}": value for index, value in enumerate(key_points, start=1)}
        valid_ids = set(catalog)
        claimed_covered = evaluation.get("covered_key_point_ids") or []
        covered_ids = [
            str(item) for item in claimed_covered if str(item) in valid_ids
        ]
        # Preserve order while dropping duplicate/unknown IDs returned by the model.
        covered_ids = list(dict.fromkeys(covered_ids))
        missing_ids = [key_id for key_id in catalog if key_id not in covered_ids]

        evaluation["covered_key_point_ids"] = covered_ids
        evaluation["missing_key_point_ids"] = missing_ids
        evaluation["missing_key_points"] = [catalog[key_id] for key_id in missing_ids]
        evaluation["key_point_coverage"] = round(len(covered_ids) / len(catalog), 2) if catalog else None

        warnings: list[str] = []
        if len(claimed_covered) != len(covered_ids):
            warnings.append("Model returned unknown or duplicate covered_key_point_ids.")
        scores = evaluation.get("scores") or {}
        raw_accuracy = scores.get("accuracy") if isinstance(scores, dict) else None
        try:
            accuracy = float(raw_accuracy) if raw_accuracy is not None else None
        except (TypeError, ValueError):
            accuracy = None
        if evaluation["key_point_coverage"] is not None and evaluation["key_point_coverage"] >= 0.75 and accuracy is not None and accuracy <= 1:
            warnings.append("Accuracy score may be inconsistent with key-point coverage.")

        feedback = evaluation.get("criterion_feedback")
        if isinstance(feedback, dict):
            rationales = [str(value).strip().lower() for value in feedback.values() if str(value).strip()]
            if len(rationales) != len(set(rationales)):
                warnings.append("Criterion rationales contain duplicate text and should be reviewed.")
        else:
            warnings.append("criterion_feedback is missing or invalid.")
        evaluation["score_consistency_warnings"] = warnings
        return evaluation

    def generate_questions(
        self,
        cv_profile: dict[str, Any],
        interview_type: str = "technical",
        count: int = 3,
        language: str = "vi",
    ) -> list[dict[str, Any]]:
        interview_type = interview_type.lower()
        language = language.lower()
        if interview_type not in {"technical", "behavioral"}:
            raise ValueError("interview_type must be technical or behavioral")
        if language not in {"vi", "en"}:
            raise ValueError("language must be vi or en")
        if count < 1 or count > 3:
            raise ValueError("count must be between 1 and 3 for the adaptive demo")

        normalized = normalize_cv_profile(cv_profile)
        cv_context = profile_to_context(normalized)
        query = profile_search_query(normalized, interview_type)
        grouped_templates = self._retrieve_templates_by_difficulty(
            query, interview_type, language, count
        )
        if not any(grouped_templates.values()):
            collection = self.settings.collection_for(interview_type)
            raise ValueError(f"No question templates found in collection '{collection}'.")

        template_text = self._template_context(grouped_templates)
        result = self.ollama.chat_json(
            QUESTION_SYSTEM,
            question_prompt(cv_context, template_text, interview_type, count, language),
            temperature=0.2,
        )
        questions = result.get("questions", []) if isinstance(result, dict) else []
        if len(questions) != count:
            raise ValueError(f"LLM returned {len(questions)} questions; expected {count}")

        expected_plan = difficulty_plan(count)
        for index, (question, expected_level) in enumerate(zip(questions, expected_plan), start=1):
            question["question_id"] = f"main-{index}"
            question["interview_type"] = interview_type
            question["difficulty"] = expected_level
        return questions

    def evaluate_answer(
        self,
        cv_profile: dict[str, Any],
        question: dict[str, Any],
        answer: str,
    ) -> dict[str, Any]:
        if not answer.strip():
            raise ValueError("answer cannot be empty")
        interview_type = str(question.get("interview_type", "technical")).lower()
        if interview_type not in {"technical", "behavioral"}:
            raise ValueError("question.interview_type must be technical or behavioral")

        normalized = normalize_cv_profile(cv_profile)
        cv_context = profile_to_context(normalized)
        evaluation = self.ollama.chat_json(
            EVALUATION_SYSTEM,
            evaluation_prompt(interview_type, question, answer, cv_context),
            temperature=0.05,
        )
        if not isinstance(evaluation, dict) or not isinstance(evaluation.get("scores"), dict):
            raise ValueError("Invalid evaluation JSON from LLM")
        evaluation = self._postprocess_evaluation(evaluation, question)
        evaluation["weighted_score"] = self.scorer.weighted_question_score(
            interview_type, evaluation["scores"]
        )
        evaluation["interview_type"] = interview_type
        return evaluation

    def generate_follow_up(
        self,
        *,
        cv_profile: dict[str, Any],
        main_question: dict[str, Any],
        conversation: list[dict[str, Any]],
        evaluation: dict[str, Any],
        followup_number: int,
        language: str,
        mode: str,
    ) -> dict[str, Any]:
        normalized = normalize_cv_profile(cv_profile)
        cv_context = profile_to_context(normalized)
        interview_type = str(main_question.get("interview_type", "technical")).lower()
        result = self.ollama.chat_json(
            FOLLOWUP_SYSTEM,
            followup_prompt(
                interview_type=interview_type,
                language=language,
                mode=mode,
                main_question=main_question,
                conversation=conversation,
                evaluation=evaluation,
                followup_number=followup_number,
                cv_context=cv_context,
            ),
            temperature=0.15,
        )
        if not isinstance(result, dict) or not str(result.get("question", "")).strip():
            candidates = main_question.get("follow_ups") or []
            fallback = candidates[min(followup_number - 1, len(candidates) - 1)] if candidates else None
            if not fallback:
                missing = evaluation.get("missing_key_points") or []
                topic = missing[0] if missing else main_question.get("skill_or_competency", "chi tiết này")
                fallback = f"Bạn có thể giải thích rõ hơn về {topic} không?"
            result = {
                "question": fallback,
                "difficulty": main_question.get("difficulty", "Medium"),
                "skill_or_competency": main_question.get("skill_or_competency", ""),
                "target_reason": mode,
            }
        result["question_id"] = f"{main_question.get('question_id', 'main')}-follow-up-{followup_number}"
        result["interview_type"] = interview_type
        result.setdefault("difficulty", main_question.get("difficulty", "Medium"))
        result.setdefault("skill_or_competency", main_question.get("skill_or_competency", ""))
        return result

    @staticmethod
    def section_average(results: list[dict[str, Any]]) -> float:
        if not results:
            raise ValueError("No evaluation results")
        return round(mean(float(item["weighted_score"]) for item in results), 2)
