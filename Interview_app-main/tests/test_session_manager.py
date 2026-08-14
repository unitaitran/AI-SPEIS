from src.session_manager import InterviewSessionManager


class FakeService:
    def __init__(self, scores):
        self.scores = iter(scores)
        self.followup_calls = 0

    def normalize_profile(self, profile):
        return profile

    def select_main_questions(self, profile, interview_type, count, language):
        return [
            {
                "question_id": f"main-{i+1}",
                "interview_type": interview_type,
                "question": f"Question {i+1}",
                "difficulty": ["Easy", "Medium", "Hard"][i],
                "skill_or_competency": "Python",
                "expected_key_points": ["point"],
                "follow_ups": ["Explain more"],
            }
            for i in range(count)
        ]

    def evaluate_answer(self, profile, question, answer):
        score = next(self.scores)
        return {
            "scores": {"accuracy": score, "depth": score, "reasoning": score, "application": score, "communication": score},
            "weighted_score": score,
            "strengths": [],
            "improvements": [],
        }

    def select_follow_up(self, **kwargs):
        self.followup_calls += 1
        return {
            "question_id": f"follow-{self.followup_calls}",
            "question": f"Follow-up {self.followup_calls}",
            "difficulty": "Medium",
            "skill_or_competency": "Python",
            "interview_type": "technical",
        }


def test_high_score_advances_without_followup():
    manager = InterviewSessionManager(FakeService([4.2, 4.1]))
    started = manager.start({}, "technical", "vi", 2, 2)
    first = manager.answer(started["session_id"], "answer")
    assert first["decision"]["action"] == "next_main_question"
    assert first["current_question"]["kind"] == "main"
    second = manager.answer(started["session_id"], "answer")
    assert second["status"] == "completed"
    assert second["section_score"] == 4.15


def test_low_score_uses_two_followups_then_completes():
    manager = InterviewSessionManager(FakeService([2.0, 2.5, 2.8]))
    started = manager.start({}, "technical", "vi", 1, 2)
    first = manager.answer(started["session_id"], "initial")
    assert first["decision"]["action"] == "ask_follow_up"
    second = manager.answer(started["session_id"], "follow one")
    assert second["decision"]["action"] == "ask_follow_up"
    third = manager.answer(started["session_id"], "follow two")
    assert third["status"] == "completed"
    assert third["results"][0]["followups_asked"] == 2
    assert third["section_score"] == 2.8


def test_medium_score_uses_only_one_followup():
    manager = InterviewSessionManager(FakeService([3.2, 3.3]))
    started = manager.start({}, "technical", "vi", 1, 2)
    first = manager.answer(started["session_id"], "initial")
    assert first["decision"]["action"] == "ask_follow_up"
    second = manager.answer(started["session_id"], "follow")
    assert second["status"] == "completed"
    assert second["results"][0]["followups_asked"] == 1
