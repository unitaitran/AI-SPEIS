from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING, Any

import pandas as pd

from .config import Settings
from .cv_parser import clean_scalar

if TYPE_CHECKING:
    from .vector_store import VectorStore


def _load_sheet(path: Path, sheet_name: str, default_language: str) -> pd.DataFrame:
    df = pd.read_excel(path, sheet_name=sheet_name)
    df.columns = [str(column).strip() for column in df.columns]
    if "Question ID" in df.columns and "id" not in df.columns:
        df = df.rename(columns={"Question ID": "id"})
    if "language" not in df.columns:
        df["language"] = default_language
    df["language"] = df["language"].fillna(default_language)
    df["source_file"] = path.name
    return df


def _question_text(row: dict[str, Any]) -> str:
    embedded = clean_scalar(row.get("embedding_text"))
    if embedded:
        return str(embedded).strip()

    fields = [
        ("language", "Language"),
        ("job_role", "Job role"),
        ("skill", "Skill"),
        ("subskill", "Subskill"),
        ("difficulty", "Difficulty"),
        ("experience_level", "Experience level"),
        ("question_type", "Question type"),
        ("question_text", "Question"),
        ("expected_key_points", "Expected key points"),
        ("keywords", "Keywords"),
    ]
    lines: list[str] = []
    for field, label in fields:
        value = clean_scalar(row.get(field))
        if value not in (None, ""):
            lines.append(f"{label}: {value}")
    return "\n".join(lines)


def _records_for_frame(
    store: VectorStore,
    collection: str,
    df: pd.DataFrame,
) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for row_number, row_series in df.iterrows():
        row = {key: clean_scalar(value) for key, value in row_series.to_dict().items()}
        if row.get("is_active") is not None and str(row["is_active"]).strip().lower() in {
            "0",
            "false",
            "no",
        }:
            continue
        text = _question_text(row)
        if not text:
            continue
        source_id = str(row.get("id") or f"row-{row_number}")
        language = str(row.get("language") or "unknown").lower()
        source_file = str(row.get("source_file") or "unknown")
        point_key = f"{source_file}:{source_id}:{language}:{collection}"
        payload = {key: value for key, value in row.items() if value is not None}
        payload["id"] = source_id
        payload["language"] = language
        payload["source_file"] = source_file
        for key in ("level_tags", "keyword_tags"):
            if isinstance(payload.get(key), str):
                payload[key] = [item.strip() for item in payload[key].split(",") if item.strip()]
        records.append(
            {
                "id": store.point_id(collection, point_key),
                "text": text,
                "payload": payload,
            }
        )
    return records


def ingest_question_bank(
    store: VectorStore,
    settings: Settings,
    vi_file: Path | None,
    en_file: Path | None,
    recreate: bool = False,
) -> dict[str, int]:
    """Upload Technical and Behavioral sheets into separate Qdrant collections."""
    frames: dict[str, list[pd.DataFrame]] = {"technical": [], "behavioral": []}
    for path, language in ((vi_file, "vi"), (en_file, "en")):
        if path is None:
            continue
        for sheet, interview_type in (("Technical", "technical"), ("Behavioral", "behavioral")):
            try:
                frames[interview_type].append(_load_sheet(path, sheet, language))
            except ValueError:
                continue

    if not any(frames.values()):
        raise ValueError("No Technical or Behavioral sheets were found")

    totals: dict[str, int] = {}
    for interview_type, dataframes in frames.items():
        if not dataframes:
            continue
        collection = settings.collection_for(interview_type)
        store.ensure_collection(collection, recreate=recreate)
        store.ensure_keyword_indexes(
            collection,
            [
                "id",
                "language",
                "job_role",
                "skill",
                "difficulty",
                "experience_level",
                "question_type",
                "source_file",
            ],
        )
        records: list[dict[str, Any]] = []
        for df in dataframes:
            records.extend(_records_for_frame(store, collection, df))
        totals[collection] = store.upsert(collection, records)
    return totals
