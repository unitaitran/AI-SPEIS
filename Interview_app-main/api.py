from __future__ import annotations

from pathlib import Path
from typing import Any, Literal

from fastapi import FastAPI, HTTPException
from fastapi.responses import HTMLResponse
from pydantic import BaseModel, Field

from src.config import get_settings
from src.cv_parser import normalize_cv_profile
from src.interview_service import InterviewService
from src.scoring import RubricScorer
from src.session_manager import InterviewSessionManager

app = FastAPI(title="CV Interview RAG", version="4.0.0")
settings = get_settings()
service = InterviewService(settings)
scorer = RubricScorer(settings.rubric_path)
sessions = InterviewSessionManager(service)
ROOT = Path(__file__).resolve().parent
warmup_status = {"status": "starting", "detail": "Preparing local models"}


@app.on_event("startup")
def warm_up_dependencies() -> None:
    """Move cold-start work ahead of the first demo interaction.

    A failed warm-up must not prevent the API from starting: endpoints retain
    their detailed error messages for unavailable local/external services.
    """
    try:
        service.store.collection_exists(settings.technical_collection)
        service.store.collection_exists(settings.behavioral_collection)
        service.ollama.warm_up()
        warmup_status.update({"status": "ready", "detail": "Qdrant and Ollama are warmed up"})
    except Exception as exc:
        warmup_status.update({"status": "degraded", "detail": str(exc)})


class NormalizeProfileRequest(BaseModel):
    cv_profile: dict[str, Any]


class GenerateRequest(BaseModel):
    cv_profile: dict[str, Any]
    interview_type: Literal["technical", "behavioral"] = "technical"
    count: int = Field(default=3, ge=1, le=3)
    language: Literal["vi", "en"] = "vi"


class EvaluateRequest(BaseModel):
    cv_profile: dict[str, Any]
    question: dict[str, Any]
    answer: str = Field(min_length=1)


class StartInterviewRequest(BaseModel):
    cv_profile: dict[str, Any]
    interview_type: Literal["technical", "behavioral"] = "technical"
    language: Literal["vi", "en"] = "vi"
    main_question_count: int = Field(default=3, ge=1, le=3)
    max_followups_per_question: int = Field(default=2, ge=0, le=2)


class InterviewAnswerRequest(BaseModel):
    answer: str = Field(min_length=1)


class CodingScoreRequest(BaseModel):
    pass_rate_percent: float = Field(ge=0, le=100)


class FinalScoreRequest(BaseModel):
    technical: float = Field(ge=0, le=5)
    coding: float = Field(ge=0, le=5)
    behavioral: float = Field(ge=0, le=5)


@app.get("/", response_class=HTMLResponse)
def home() -> str:
    return '<h2>CV Interview RAG v4</h2><p><a href="/demo">Open adaptive interview demo</a></p><p><a href="/docs">Open API docs</a></p>'


@app.get("/demo", response_class=HTMLResponse)
def demo_page() -> str:
    return (ROOT / "demo" / "index.html").read_text(encoding="utf-8")


@app.get("/health")
def health() -> dict[str, str]:
    return {
        "status": warmup_status["status"],
        "detail": warmup_status["detail"],
        "cv_storage": "direct-request-only",
        "qdrant_usage": "two-question-bank-collections",
        "technical_collection": settings.technical_collection,
        "behavioral_collection": settings.behavioral_collection,
        "interview_mode": "adaptive-max-3-main-questions",
        "session_storage": "in-memory-demo",
    }


@app.post("/profiles/normalize")
def normalize_profile(body: NormalizeProfileRequest) -> dict[str, Any]:
    try:
        return normalize_cv_profile(body.cv_profile)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/questions/generate")
def generate_questions(body: GenerateRequest) -> dict[str, Any]:
    try:
        profile = normalize_cv_profile(body.cv_profile)
        questions = service.generate_questions(
            profile, body.interview_type, body.count, body.language
        )
        return {"candidate_id": profile.get("candidate_id"), "questions": questions}
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/answers/evaluate")
def evaluate_answer(body: EvaluateRequest) -> dict[str, Any]:
    try:
        return service.evaluate_answer(body.cv_profile, body.question, body.answer)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/interviews/start")
def start_interview(body: StartInterviewRequest) -> dict[str, Any]:
    """Start a stateful adaptive demo with at most three main questions."""
    try:
        return sessions.start(
            cv_profile=body.cv_profile,
            interview_type=body.interview_type,
            language=body.language,
            main_question_count=body.main_question_count,
            max_followups_per_question=body.max_followups_per_question,
        )
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/interviews/{session_id}/answer")
def answer_interview(session_id: str, body: InterviewAnswerRequest) -> dict[str, Any]:
    """Score the current answer, then ask a follow-up or advance automatically."""
    try:
        return sessions.answer(session_id, body.answer)
    except KeyError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.get("/interviews/{session_id}")
def interview_status(session_id: str) -> dict[str, Any]:
    try:
        return sessions.get(session_id)
    except KeyError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/scores/coding")
def coding_score(body: CodingScoreRequest) -> dict[str, float]:
    try:
        return {"coding_score": scorer.coding_score(body.pass_rate_percent)}
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/scores/final")
def final_score(body: FinalScoreRequest) -> dict[str, Any]:
    try:
        return scorer.final_score(body.technical, body.coding, body.behavioral)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
