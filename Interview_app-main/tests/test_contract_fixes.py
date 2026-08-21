from unittest.mock import MagicMock
from src.cv_parser import normalize_cv_profile, profile_search_query
from src.interview_service import InterviewService
from src.config import Settings


def test_normalize_cv_profile_supports_job_role_and_experience_level():
    data = {
        "job_role": "Frontend",
        "experience_level": "Senior",
        "skills": ["React", "JavaScript"]
    }
    profile = normalize_cv_profile(data)
    assert profile["role_target"] == "Frontend"
    assert profile["experience_level"] == "Senior"
    assert len(profile["skills"]) == 2

    query = profile_search_query(profile, "technical")
    assert "Frontend" in query
    assert "Experience level: Senior" in query


def test_retrieve_questions_supports_string_business_ids_and_normalizes_key_points():
    settings = MagicMock(spec=Settings)
    service = InterviewService.__new__(InterviewService)
    service.settings = settings
    service.store = MagicMock()

    grouped_templates = {
        "Medium": [
            {
                "id": "FE001-001",
                "question_text": "What is the Virtual DOM?",
                "skill": "React",
                "difficulty": "Medium",
                "language": "vi",
                "expected_answer": "A lightweight copy of the DOM",
                "expected_key_points": "Reconciliation; Diffing algorithm",
                "is_active": True
            },
            {
                "id": "BA005-008",
                "question_text": "Explain Requirement Traceability Matrix.",
                "skill": "SRS",
                "difficulty": "Medium",
                "language": "vi",
                "expected_answer": "RTM maps requirements to test cases",
                "expected_key_points": ["Requirements", "Test Cases"],
                "is_active": True
            },
            {
                "id": "",
                "question_text": "Blank ID question",
                "is_active": True
            }
        ]
    }

    service._retrieve_templates_by_difficulty = MagicMock(return_value=grouped_templates)

    cv_profile = {"job_role": "Frontend", "skills": ["React"]}
    results = service.retrieve_questions(cv_profile, "technical", 3, "vi")

    assert len(results) == 2
    assert results[0]["id"] == "FE001-001"
    assert results[0]["expected_key_points"] == ["Reconciliation", "Diffing algorithm"]
    assert results[1]["id"] == "BA005-008"
    assert results[1]["expected_key_points"] == ["Requirements", "Test Cases"]
