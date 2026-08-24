from __future__ import annotations

from unittest.mock import MagicMock

from src.config import Settings
from src.question_sync_service import (
    QuestionSyncService,
    build_embedding_text,
    build_payload,
    normalize_sync_dict,
)


def test_normalize_sync_dict_camel_and_snake() -> None:
    camel_data = {
        "questionId": 40,
        "questionContent": "What is SQL Index?",
        "roleTarget": "Backend Developer",
        "difficulty": "Medium",
        "questionType": "Technical",
        "expectedKeyPoints": "Clustered, Non-clustered",
        "levelTags": ["Middle", "Senior"],
    }
    norm = normalize_sync_dict(camel_data)
    assert norm["question_id"] == 40
    assert norm["question_content"] == "What is SQL Index?"
    assert norm["role_target"] == "Backend Developer"
    assert norm["difficulty"] == "Medium"
    assert norm["question_type"] == "Technical"


def test_build_embedding_text() -> None:
    data = {
        "language": "vi",
        "role_target": "Backend Developer",
        "skill": "Database",
        "difficulty": "Medium",
        "question_content": "Explain B-Tree Index",
        "expected_key_points": "Root, leaf, node",
        "keyword_tags": "sql, index",
    }
    text = build_embedding_text(data)
    assert "Job role: Backend Developer" in text
    assert "Skill: Database" in text
    assert "Difficulty: Medium" in text
    assert "Question: Explain B-Tree Index" in text


def test_build_payload() -> None:
    data = {
        "question_id": 40,
        "language": "vi",
        "role_target": "Backend Developer",
        "skill": "Database",
        "difficulty": "Medium",
        "question_type": "technical",
        "question_content": "Explain B-Tree Index",
        "suggested_answer": "A balanced tree structure...",
        "expected_key_points": "Root, leaf, node",
        "follow_up_1": "How does write amplification occur?",
        "level_tags": "Junior, Middle",
    }
    payload = build_payload(data)
    assert payload["id"] == "40"
    assert payload["job_role"] == "Backend Developer"
    assert payload["question_text"] == "Explain B-Tree Index"
    assert payload["expected_key_points"] == ["Root", "leaf", "node"]
    assert payload["level_tags"] == ["Junior", "Middle"]
    assert "How does write amplification occur?" in payload["follow_ups"]


def test_question_sync_service_sync_and_delete() -> None:
    mock_store = MagicMock()
    mock_store.upsert.return_value = 1
    mock_store.delete_point.return_value = True

    settings = MagicMock(spec=Settings)
    settings.technical_collection = "technical_questions"
    settings.behavioral_collection = "behavioral_questions"
    settings.collection_for.side_effect = lambda t: "behavioral_questions" if t == "behavioral" else "technical_questions"

    service = QuestionSyncService(mock_store, settings)

    # Test sync
    sync_res = service.sync_question(
        {
            "question_id": 40,
            "question_type": "Technical",
            "question_content": "Explain Index",
            "role_target": "Backend Developer",
        }
    )
    assert sync_res["status"] == "synced"
    assert sync_res["question_id"] == 40
    mock_store.delete_point.assert_called_with("behavioral_questions", 40)
    mock_store.upsert.assert_called_once()

    # Test delete
    del_res = service.delete_question(40)
    assert del_res["status"] == "deleted"
    assert del_res["question_id"] == 40
