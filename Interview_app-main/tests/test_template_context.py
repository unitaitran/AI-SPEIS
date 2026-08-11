from src.config import get_settings
from src.interview_service import InterviewService


def test_template_context_removes_large_payload_metadata():
    service = InterviewService(get_settings())
    context = service._template_context(
        {
            "Medium": [
                {
                    "id": "template-1",
                    "difficulty": "Medium",
                    "skill": "Python",
                    "question_text": "Explain dependency injection.",
                    "expected_answer": "Dependencies are supplied externally.",
                    "expected_key_points": ["testability"],
                    "scoring_rubric": "x" * 10_000,
                    "embedding_text": "x" * 10_000,
                }
            ]
        }
    )

    assert "dependency injection" in context
    assert "scoring_rubric" not in context
    assert "embedding_text" not in context
