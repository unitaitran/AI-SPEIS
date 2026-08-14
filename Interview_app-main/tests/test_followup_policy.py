import pytest

from src.followup_policy import allowed_followups, followup_mode, should_ask_followup


def test_followup_thresholds():
    assert allowed_followups(4.0, 2) == 0
    assert allowed_followups(3.5, 2) == 1
    assert allowed_followups(2.9, 2) == 2


def test_followup_respects_configured_max():
    assert allowed_followups(1.0, 1) == 1
    assert allowed_followups(3.2, 0) == 0


def test_should_ask_followup():
    assert should_ask_followup(2.0, 0, 2)
    assert should_ask_followup(2.0, 1, 2)
    assert not should_ask_followup(2.0, 2, 2)
    assert not should_ask_followup(4.2, 0, 2)


def test_followup_modes():
    assert followup_mode(2.0) == "clarify_missing_foundation"
    assert followup_mode(3.5) == "probe_depth_or_evidence"
    assert followup_mode(4.0) == "none"


def test_invalid_score():
    with pytest.raises(ValueError):
        allowed_followups(5.1)
