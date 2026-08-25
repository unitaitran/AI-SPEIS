from __future__ import annotations

import logging
from typing import Any

from .config import Settings
from .cv_parser import clean_scalar
from .vector_store import VectorStore

logger = logging.getLogger(__name__)


def _clean_list(val: Any) -> list[str]:
    if val is None:
        return []
    if isinstance(val, list):
        return [str(x).strip() for x in val if x is not None and str(x).strip() != ""]
    if isinstance(val, str):
        # Support comma and semicolon split
        parts = [p.strip() for p in val.replace(";", ",").split(",") if p.strip()]
        return parts
    return [str(val).strip()]


def normalize_sync_dict(data: dict[str, Any]) -> dict[str, Any]:
    """Normalize input dictionary whether keys are in camelCase or snake_case."""
    aliases = {
        "question_id": ("question_id", "questionId", "id", "QuestionId"),
        "question_content": ("question_content", "questionContent", "question_text", "questionText", "question", "QuestionContent"),
        "suggested_answer": ("suggested_answer", "suggestedAnswer", "expected_answer", "expectedAnswer", "SuggestedAnswer"),
        "difficulty": ("difficulty", "Difficulty", "difficulty_level", "level"),
        "role_target": ("role_target", "roleTarget", "job_role", "jobRole", "RoleTarget"),
        "major": ("major", "Major"),
        "question_type": ("question_type", "questionType", "QuestionType"),
        "language": ("language", "Language"),
        "skill": ("skill", "Skill"),
        "subskill": ("subskill", "Subskill"),
        "experience_level": ("experience_level", "experienceLevel", "ExperienceLevel"),
        "level_tags": ("level_tags", "levelTags", "LevelTags"),
        "company_category": ("company_category", "companyCategory", "CompanyCategory"),
        "company_subcategory": ("company_subcategory", "companySubcategory", "CompanySubcategory"),
        "expected_key_points": ("expected_key_points", "expectedKeyPoints", "ExpectedKeyPoints"),
        "scoring_rubric": ("scoring_rubric", "scoringRubric", "ScoringRubric"),
        "clarification_question": ("clarification_question", "clarificationQuestion", "ClarificationQuestion"),
        "follow_up_1": ("follow_up_1", "followUp1", "FollowUp1"),
        "follow_up_2": ("follow_up_2", "followUp2", "FollowUp2"),
        "time_limit_seconds": ("time_limit_seconds", "timeLimitSeconds", "TimeLimitSeconds"),
        "keyword_tags": ("keyword_tags", "keywordTags", "KeywordTags"),
    }

    normalized: dict[str, Any] = {}
    for standard_key, candidates in aliases.items():
        for candidate in candidates:
            if candidate in data and data[candidate] is not None:
                normalized[standard_key] = data[candidate]
                break

    # Also retain any extra keys
    for k, v in data.items():
        if k not in normalized and v is not None:
            normalized[k] = v

    return normalized


def build_embedding_text(data: dict[str, Any]) -> str:
    """Build canonical text representation for vector embedding."""
    fields = [
        ("language", "Language"),
        ("role_target", "Job role"),
        ("skill", "Skill"),
        ("subskill", "Subskill"),
        ("difficulty", "Difficulty"),
        ("experience_level", "Experience level"),
        ("question_type", "Question type"),
        ("question_content", "Question"),
        ("expected_key_points", "Expected key points"),
        ("keyword_tags", "Keywords"),
    ]

    lines: list[str] = []
    for field, label in fields:
        val = clean_scalar(data.get(field))
        if val is None:
            raw_val = data.get(field)
            if isinstance(raw_val, list):
                val = ", ".join(str(x) for x in raw_val if x is not None and str(x).strip())
        if val not in (None, ""):
            lines.append(f"{label}: {str(val).strip()}")

    return "\n".join(lines)


def build_payload(data: dict[str, Any]) -> dict[str, Any]:
    """Build Qdrant payload dictionary conforming to question bank format."""
    question_id = int(data.get("question_id", 0))
    language = str(data.get("language") or "vi").lower()
    difficulty = str(data.get("difficulty") or "Medium")
    question_type = str(data.get("question_type") or "Technical").lower()
    role_target = str(data.get("role_target") or "")
    skill = str(data.get("skill") or "")
    question_content = str(data.get("question_content") or "")
    suggested_answer = str(data.get("suggested_answer") or "")

    expected_key_points = _clean_list(data.get("expected_key_points"))
    level_tags = _clean_list(data.get("level_tags"))
    keyword_tags = _clean_list(data.get("keyword_tags"))

    follow_ups: list[str] = []
    for k in ("follow_up_1", "follow_up_2", "clarification_question"):
        v = data.get(k)
        if v and str(v).strip():
            follow_ups.append(str(v).strip())

    payload: dict[str, Any] = {
        "id": str(question_id),
        "language": language,
        "job_role": role_target,
        "skill": skill,
        "subskill": str(data.get("subskill") or ""),
        "difficulty": difficulty,
        "experience_level": str(data.get("experience_level") or ""),
        "question_type": question_type,
        "question_text": question_content,
        "expected_answer": suggested_answer,
        "expected_key_points": expected_key_points,
        "scoring_rubric": str(data.get("scoring_rubric") or ""),
        "clarification_question": str(data.get("clarification_question") or ""),
        "follow_up_1": str(data.get("follow_up_1") or ""),
        "follow_up_2": str(data.get("follow_up_2") or ""),
        "follow_ups": follow_ups,
        "time_limit_seconds": int(data.get("time_limit_seconds") or 120),
        "level_tags": level_tags,
        "keyword_tags": keyword_tags,
        "company_category": str(data.get("company_category") or ""),
        "company_subcategory": str(data.get("company_subcategory") or ""),
        "source": "sql_admin_sync",
    }

    return payload


class QuestionSyncService:
    """Service to synchronize SQL Question entities into Qdrant collections."""

    def __init__(self, store: VectorStore, settings: Settings):
        self.store = store
        self.settings = settings

    def sync_question(self, raw_data: dict[str, Any]) -> dict[str, Any]:
        """Synchronize a single question into Qdrant."""
        data = normalize_sync_dict(raw_data)
        question_id = int(data["question_id"])

        q_type = str(data.get("question_type", "technical")).lower()
        interview_type = "behavioral" if "behavioral" in q_type else "technical"
        target_collection = self.settings.collection_for(interview_type)
        other_collection = (
            self.settings.behavioral_collection
            if target_collection == self.settings.technical_collection
            else self.settings.technical_collection
        )

        # Idempotent cleanup: remove from the other collection if question type was changed
        self.store.delete_point(other_collection, question_id)

        # Ensure target collection and keyword indexes exist
        self.store.ensure_collection(target_collection)
        self.store.ensure_keyword_indexes(
            target_collection,
            [
                "id",
                "language",
                "job_role",
                "skill",
                "difficulty",
                "experience_level",
                "question_type",
            ],
        )

        # Build embedding text and payload
        text = build_embedding_text(data)
        payload = build_payload(data)

        records = [
            {
                "id": question_id,
                "text": text,
                "payload": payload,
            }
        ]

        count = self.store.upsert(target_collection, records)
        logger.info(
            "Synchronized Question #%d to collection '%s' (records=%d)",
            question_id,
            target_collection,
            count,
        )

        return {
            "status": "synced",
            "question_id": question_id,
            "collection": target_collection,
            "upserted_count": count,
        }

    def delete_question(self, question_id: int) -> dict[str, Any]:
        """Delete a question from all Qdrant question collections."""
        t_deleted = self.store.delete_point(self.settings.technical_collection, question_id)
        b_deleted = self.store.delete_point(self.settings.behavioral_collection, question_id)

        logger.info(
            "Deleted Question #%d from Qdrant (technical=%s, behavioral=%s)",
            question_id,
            t_deleted,
            b_deleted,
        )

        return {
            "status": "deleted",
            "question_id": question_id,
            "technical_deleted": t_deleted,
            "behavioral_deleted": b_deleted,
        }

    def sync_batch(self, raw_items: list[dict[str, Any]]) -> dict[str, Any]:
        """Synchronize a batch of questions to Qdrant."""
        grouped: dict[str, list[dict[str, Any]]] = {
            self.settings.technical_collection: [],
            self.settings.behavioral_collection: [],
        }

        for item in raw_items:
            data = normalize_sync_dict(item)
            q_id = int(data.get("question_id", 0))
            if q_id <= 0:
                continue

            q_type = str(data.get("question_type", "technical")).lower()
            interview_type = "behavioral" if "behavioral" in q_type else "technical"
            target_coll = self.settings.collection_for(interview_type)

            text = build_embedding_text(data)
            payload = build_payload(data)

            grouped[target_coll].append(
                {
                    "id": q_id,
                    "text": text,
                    "payload": payload,
                }
            )

        results: dict[str, int] = {}
        for coll, records in grouped.items():
            if not records:
                continue
            self.store.ensure_collection(coll)
            self.store.ensure_keyword_indexes(
                coll,
                [
                    "id",
                    "language",
                    "job_role",
                    "skill",
                    "difficulty",
                    "experience_level",
                    "question_type",
                ],
            )
            results[coll] = self.store.upsert(coll, records, batch_size=64)

        return {
            "status": "synced_batch",
            "total_items": len(raw_items),
            "collection_counts": results,
        }
