from __future__ import annotations

import json
from pathlib import Path
from typing import Any


class RubricScorer:
    def __init__(self, rubric_path: Path):
        self.rubric = json.loads(rubric_path.read_text(encoding="utf-8"))

    @staticmethod
    def _score(value: Any) -> float:
        score = float(value)
        if score < 0 or score > 5:
            raise ValueError(f"Criterion score must be between 0 and 5, got {score}")
        return score

    def weighted_question_score(self, interview_type: str, scores: dict[str, Any]) -> float:
        interview_type = interview_type.lower()
        if interview_type not in {"technical", "behavioral"}:
            raise ValueError("interview_type must be technical or behavioral")
        weights = self.rubric[interview_type]
        missing = set(weights) - set(scores)
        if missing:
            raise ValueError(f"Missing rubric criteria: {sorted(missing)}")
        result = sum(self._score(scores[key]) * weight for key, weight in weights.items())
        return round(result, 2)

    @staticmethod
    def coding_score(pass_rate_percent: float) -> float:
        rate = float(pass_rate_percent)
        if rate < 0 or rate > 100:
            raise ValueError("pass_rate_percent must be from 0 to 100")
        if rate == 100:
            return 5.0
        if rate >= 80:
            return 4.0
        if rate >= 60:
            return 3.0
        if rate >= 40:
            return 2.0
        if rate >= 20:
            return 1.0
        return 0.0

    def final_score(self, technical: float, coding: float, behavioral: float) -> dict[str, Any]:
        for name, value in {"technical": technical, "coding": coding, "behavioral": behavioral}.items():
            self._score(value)
        weights = self.rubric["final"]
        total = round(
            technical * weights["technical"]
            + coding * weights["coding"]
            + behavioral * weights["behavioral"],
            2,
        )
        return {"score": total, "classification": self.classify(total)}

    def classify(self, score: float) -> str:
        score = round(float(score), 2)
        for band in self.rubric["classification"]:
            # Rounded score makes the documented non-overlapping bands practical.
            if band["min"] <= score <= band["max"]:
                return band["label"]
        if 4.49 < score < 4.50:
            return "Tốt"
        if 3.49 < score < 3.50:
            return "Khá"
        if 2.49 < score < 2.50:
            return "Yếu"
        if 1.49 < score < 1.50:
            return "Kém"
        raise ValueError(f"Score outside classification range: {score}")
