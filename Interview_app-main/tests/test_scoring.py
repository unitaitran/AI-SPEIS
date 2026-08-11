from pathlib import Path

from src.scoring import RubricScorer


ROOT = Path(__file__).resolve().parents[1]
SCORER = RubricScorer(ROOT / "rubric.json")


def test_technical_weighted_score():
    score = SCORER.weighted_question_score(
        "technical",
        {"accuracy": 5, "depth": 4, "reasoning": 3, "application": 2, "communication": 1},
    )
    assert score == 3.5


def test_behavioral_weighted_score():
    score = SCORER.weighted_question_score(
        "behavioral",
        {"situation": 4, "action": 5, "result": 3, "competency": 4, "communication": 3},
    )
    assert score == 3.95


def test_coding_bands():
    assert SCORER.coding_score(100) == 5
    assert SCORER.coding_score(99.9) == 4
    assert SCORER.coding_score(80) == 4
    assert SCORER.coding_score(60) == 3
    assert SCORER.coding_score(40) == 2
    assert SCORER.coding_score(20) == 1
    assert SCORER.coding_score(19.9) == 0


def test_final_score():
    result = SCORER.final_score(4, 3, 5)
    assert result == {"score": 3.8, "classification": "Tốt"}
