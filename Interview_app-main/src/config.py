from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv


@dataclass(frozen=True)
class Settings:
    qdrant_url: str
    qdrant_api_key: str | None
    ollama_base_url: str
    ollama_model: str
    embedding_model: str
    technical_collection: str
    behavioral_collection: str
    top_k_questions: int
    templates_per_difficulty: int
    template_context_chars: int
    ollama_keep_alive: str
    rubric_path: Path

    def collection_for(self, interview_type: str) -> str:
        normalized = interview_type.strip().lower()
        if normalized == "technical":
            return self.technical_collection
        if normalized == "behavioral":
            return self.behavioral_collection
        raise ValueError(f"Unsupported interview type: {interview_type}")


def get_settings() -> Settings:
    load_dotenv()
    root = Path(__file__).resolve().parents[1]
    return Settings(
        qdrant_url=os.getenv("QDRANT_URL", "http://localhost:6333"),
        qdrant_api_key=os.getenv("QDRANT_API_KEY") or None,
        ollama_base_url=os.getenv("OLLAMA_BASE_URL", "http://localhost:11434").rstrip("/"),
        ollama_model=os.getenv("OLLAMA_MODEL", "qwen2.5:7b"),
        embedding_model=os.getenv(
            "EMBEDDING_MODEL",
            "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2",
        ),
        technical_collection=os.getenv("TECHNICAL_COLLECTION", "technical_questions"),
        behavioral_collection=os.getenv("BEHAVIORAL_COLLECTION", "behavioral_questions"),
        # Question generation only needs a few high-quality patterns.  Sending a
        # large collection payload to a local LLM is much slower than retrieval.
        top_k_questions=int(os.getenv("TOP_K_QUESTIONS", "6")),
        templates_per_difficulty=int(os.getenv("TEMPLATES_PER_DIFFICULTY", "2")),
        template_context_chars=int(os.getenv("TEMPLATE_CONTEXT_CHARS", "9000")),
        ollama_keep_alive=os.getenv("OLLAMA_KEEP_ALIVE", "30m"),
        rubric_path=root / "rubric.json",
    )
