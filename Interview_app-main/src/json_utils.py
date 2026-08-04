from __future__ import annotations

import ast
import json
import re
from typing import Any


def parse_loose_json(value: Any, default: Any) -> Any:
    """Parse JSON stored inside CSV cells, tolerating Python literals and empty values."""
    if value is None:
        return default
    if isinstance(value, (dict, list)):
        return value
    text = str(value).strip()
    if not text or text.lower() in {"nan", "none", "null"}:
        return default
    for parser in (json.loads, ast.literal_eval):
        try:
            return parser(text)
        except (json.JSONDecodeError, SyntaxError, ValueError, TypeError):
            continue
    return default


def extract_json(text: str) -> Any:
    """Extract a JSON object/array from an LLM response, including fenced output."""
    cleaned = text.strip()
    cleaned = re.sub(r"^```(?:json)?\s*", "", cleaned, flags=re.IGNORECASE)
    cleaned = re.sub(r"\s*```$", "", cleaned)
    try:
        return json.loads(cleaned)
    except json.JSONDecodeError:
        pass

    starts = [i for i in (cleaned.find("{"), cleaned.find("[")) if i >= 0]
    if not starts:
        raise ValueError(f"LLM did not return JSON: {text[:300]}")
    start = min(starts)
    end_obj = cleaned.rfind("}")
    end_arr = cleaned.rfind("]")
    end = max(end_obj, end_arr)
    if end < start:
        raise ValueError(f"Incomplete JSON from LLM: {text[:300]}")
    return json.loads(cleaned[start : end + 1])
