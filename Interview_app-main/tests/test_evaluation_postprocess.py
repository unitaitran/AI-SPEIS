from src.interview_service import InterviewService


def test_postprocess_maps_valid_key_point_ids_and_warns_on_inconsistent_accuracy():
    evaluation = {
        "scores": {"accuracy": 1, "depth": 3, "reasoning": 3, "application": 3, "communication": 3},
        "covered_key_point_ids": ["KP-1", "KP-2", "KP-3", "KP-404"],
        "criterion_feedback": {
            "accuracy": "Correct core facts.",
            "depth": "Explains one implementation detail.",
        },
    }
    question = {"expected_key_points": ["one", "two", "three", "four"]}

    result = InterviewService._postprocess_evaluation(evaluation, question)

    assert result["covered_key_point_ids"] == ["KP-1", "KP-2", "KP-3"]
    assert result["missing_key_point_ids"] == ["KP-4"]
    assert result["key_point_coverage"] == 0.75
    assert "Accuracy score may be inconsistent with key-point coverage." in result["score_consistency_warnings"]
    assert "Model returned unknown or duplicate covered_key_point_ids." in result["score_consistency_warnings"]


def test_postprocess_flags_duplicate_criterion_rationales():
    evaluation = {
        "scores": {"accuracy": 4},
        "covered_key_point_ids": [],
        "criterion_feedback": {"accuracy": "Evidence is correct.", "depth": "Evidence is correct."},
    }

    result = InterviewService._postprocess_evaluation(evaluation, {"expected_key_points": ["one"]})

    assert "Criterion rationales contain duplicate text and should be reviewed." in result["score_consistency_warnings"]
