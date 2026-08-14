from __future__ import annotations


def allowed_followups(score: float, configured_max: int = 2) -> int:
    """Return the maximum number of follow-ups allowed for the current score."""
    value = float(score)
    if value < 0 or value > 5:
        raise ValueError("score must be between 0 and 5")
    if configured_max < 0 or configured_max > 2:
        raise ValueError("configured_max must be between 0 and 2")
    if value >= 4.0:
        policy_limit = 0
    elif value >= 3.0:
        policy_limit = 1
    else:
        policy_limit = 2
    return min(policy_limit, configured_max)


def should_ask_followup(score: float, followups_already_asked: int, configured_max: int = 2) -> bool:
    if followups_already_asked < 0:
        raise ValueError("followups_already_asked cannot be negative")
    return followups_already_asked < allowed_followups(score, configured_max)


def followup_mode(score: float) -> str:
    """Select the purpose of the next follow-up."""
    value = float(score)
    if value < 0 or value > 5:
        raise ValueError("score must be between 0 and 5")
    if value < 3.0:
        return "clarify_missing_foundation"
    if value < 4.0:
        return "probe_depth_or_evidence"
    return "none"
