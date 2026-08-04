from __future__ import annotations

import uuid
from copy import deepcopy
from datetime import datetime, timezone
from statistics import mean
from threading import RLock
from typing import TYPE_CHECKING, Any

from .followup_policy import should_ask_followup

if TYPE_CHECKING:
    from .interview_service import InterviewService


class InterviewSessionManager:
    """In-memory interview orchestration for a local/demo deployment."""

    def __init__(self, service: "InterviewService"):
        self.service = service
        self._sessions: dict[str, dict[str, Any]] = {}
        self._lock = RLock()

    def start(
        self,
        cv_profile: dict[str, Any],
        interview_type: str,
        language: str,
        main_question_count: int,
        max_followups_per_question: int,
    ) -> dict[str, Any]:
        if main_question_count < 1 or main_question_count > 3:
            raise ValueError("main_question_count must be between 1 and 3")
        if max_followups_per_question < 0 or max_followups_per_question > 2:
            raise ValueError("max_followups_per_question must be between 0 and 2")

        normalized = self.service.normalize_profile(cv_profile)
        # Main questions are retrieved and cached at session start.  This lets
        # the UI show Q1 immediately and avoids competing Ollama requests while
        # the candidate is answering.
        main_questions = self.service.select_main_questions(
            normalized,
            interview_type=interview_type,
            count=main_question_count,
            language=language,
        )
        session_id = str(uuid.uuid4())
        records = [
            {
                "main_question": question,
                "turns": [],
                "followups_asked": 0,
                "latest_evaluation": None,
                "final_score": None,
                "completed": False,
            }
            for question in main_questions
        ]
        now = datetime.now(timezone.utc).isoformat()
        session = {
            "session_id": session_id,
            "created_at": now,
            "updated_at": now,
            "cv_profile": normalized,
            "interview_type": interview_type,
            "language": language,
            "main_question_count": main_question_count,
            "max_followups_per_question": max_followups_per_question,
            "current_main_index": 0,
            "current_kind": "main",
            "current_question": main_questions[0],
            "records": records,
            "status": "active",
        }
        with self._lock:
            self._sessions[session_id] = session
        return self.public_state(session, include_results=False)

    def answer(self, session_id: str, answer: str) -> dict[str, Any]:
        if not answer.strip():
            raise ValueError("answer cannot be empty")
        with self._lock:
            session = self._sessions.get(session_id)
            if not session:
                raise KeyError("Interview session not found")
            if session["status"] != "active":
                raise ValueError("Interview session is already completed")

            index = int(session["current_main_index"])
            record = session["records"][index]
            current_question = deepcopy(session["current_question"])
            current_kind = str(session["current_kind"])
            record["turns"].append(
                {
                    "kind": current_kind,
                    "question": current_question.get("question", ""),
                    "answer": answer.strip(),
                }
            )

            combined_answer = self._combined_answer(record["turns"])
            evaluation = self.service.evaluate_answer(
                session["cv_profile"],
                record["main_question"],
                combined_answer,
            )
            score = float(evaluation["weighted_score"])
            record["latest_evaluation"] = evaluation

            decision: dict[str, Any]
            if should_ask_followup(
                score,
                int(record["followups_asked"]),
                int(session["max_followups_per_question"]),
            ):
                next_followup = self.service.select_follow_up(
                    main_question=record["main_question"],
                    evaluation=evaluation,
                    followup_number=int(record["followups_asked"]) + 1,
                    language=session["language"],
                )
                record["followups_asked"] += 1
                session["current_kind"] = "follow_up"
                session["current_question"] = next_followup
                decision = {
                    "action": "ask_follow_up",
                    "reason": self._decision_reason(score),
                }
            else:
                record["final_score"] = round(score, 2)
                record["completed"] = True
                if index + 1 < int(session["main_question_count"]):
                    session["current_main_index"] = index + 1
                    session["current_kind"] = "main"
                    session["current_question"] = session["records"][index + 1]["main_question"]
                    decision = {
                        "action": "next_main_question",
                        "reason": self._decision_reason(score),
                    }
                else:
                    session["status"] = "completed"
                    session["current_kind"] = "completed"
                    session["current_question"] = None
                    decision = {
                        "action": "complete_interview",
                        "reason": "All main questions are completed.",
                    }

            session["updated_at"] = datetime.now(timezone.utc).isoformat()
            response = self.public_state(session, include_results=session["status"] == "completed")
            response["latest_evaluation"] = evaluation
            response["decision"] = decision
            return response

    def get(self, session_id: str) -> dict[str, Any]:
        with self._lock:
            session = self._sessions.get(session_id)
            if not session:
                raise KeyError("Interview session not found")
            return self.public_state(session, include_results=True)

    @staticmethod
    def _combined_answer(turns: list[dict[str, str]]) -> str:
        parts: list[str] = []
        for idx, turn in enumerate(turns, start=1):
            label = "MAIN" if turn["kind"] == "main" else f"FOLLOW-UP {idx - 1}"
            parts.append(f"{label} QUESTION: {turn['question']}\n{label} ANSWER: {turn['answer']}")
        return "\n\n".join(parts)

    @staticmethod
    def _decision_reason(score: float) -> str:
        if score >= 4.0:
            return "Score is at least 4.0, so no follow-up is required."
        if score >= 3.0:
            return "Score is between 3.0 and 3.99, so at most one depth/evidence follow-up is used."
        return "Score is below 3.0, so up to two clarification follow-ups may be used."

    @staticmethod
    def _public_question(question: dict[str, Any] | None, kind: str, number: int) -> dict[str, Any] | None:
        if question is None:
            return None
        return {
            "question_id": question.get("question_id"),
            "kind": kind,
            "main_question_number": number,
            "question": question.get("question", ""),
            "difficulty": question.get("difficulty"),
            "skill_or_competency": question.get("skill_or_competency"),
        }

    def public_state(self, session: dict[str, Any], include_results: bool) -> dict[str, Any]:
        index = int(session["current_main_index"])
        completed_main = sum(1 for item in session["records"] if item["completed"])
        payload: dict[str, Any] = {
            "session_id": session["session_id"],
            "status": session["status"],
            "interview_type": session["interview_type"],
            "progress": {
                "completed_main_questions": completed_main,
                "total_main_questions": session["main_question_count"],
                "current_main_question": None if session["status"] == "completed" else index + 1,
            },
            "current_question": self._public_question(
                session["current_question"],
                session["current_kind"],
                index + 1,
            ),
        }
        if include_results:
            results = []
            final_scores: list[float] = []
            for item in session["records"]:
                if item["final_score"] is not None:
                    final_scores.append(float(item["final_score"]))
                results.append(
                    {
                        "main_question": self._public_question(item["main_question"], "main", len(results) + 1),
                        "followups_asked": item["followups_asked"],
                        "turns": deepcopy(item["turns"]),
                        "final_score": item["final_score"],
                        "latest_evaluation": deepcopy(item["latest_evaluation"]),
                        "completed": item["completed"],
                    }
                )
            payload["results"] = results
            payload["section_score"] = round(mean(final_scores), 2) if final_scores else None
        return payload
