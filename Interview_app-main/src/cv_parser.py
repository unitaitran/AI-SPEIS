from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any

import pandas as pd

from .json_utils import parse_loose_json


def clean_scalar(value: Any) -> Any:
    if value is None:
        return None
    try:
        if pd.isna(value):
            return None
    except (TypeError, ValueError):
        pass
    if hasattr(value, "item"):
        try:
            value = value.item()
        except (AttributeError, ValueError):
            pass
    if isinstance(value, float) and math.isnan(value):
        return None
    return value


def _text(value: Any) -> str:
    value = clean_scalar(value)
    return "" if value is None else str(value).strip()


def read_cv_csv(path: Path) -> pd.DataFrame:
    encodings = ("utf-8-sig", "utf-8", "cp1258", "latin1")
    last_error: Exception | None = None
    for encoding in encodings:
        try:
            df = pd.read_csv(path, encoding=encoding)
            break
        except UnicodeDecodeError as exc:
            last_error = exc
    else:
        raise RuntimeError(f"Cannot decode CSV {path}: {last_error}")

    required = {"ExtractedProfileId", "RawAiOutput"}
    missing = required - set(df.columns)
    if missing:
        raise ValueError(f"CVExtract.csv is missing columns: {sorted(missing)}")
    return df


def _normalize_education(items: Any) -> list[dict[str, str]]:
    result: list[dict[str, str]] = []
    if not isinstance(items, list):
        return result
    for item in items:
        if not isinstance(item, dict):
            continue
        normalized = {
            "school": _text(item.get("school") or item.get("School")),
            "major": _text(item.get("major") or item.get("Major")),
            "gpa": _text(item.get("gpa") or item.get("Gpa") or item.get("GPA")),
            "graduation_year": _text(
                item.get("graduationYear") or item.get("GraduationYear") or item.get("graduation_year")
            ),
        }
        if any(normalized.values()):
            result.append(normalized)
    return result


def _normalize_experience(items: Any) -> list[dict[str, str]]:
    result: list[dict[str, str]] = []
    if not isinstance(items, list):
        return result
    for item in items:
        if not isinstance(item, dict):
            continue
        normalized = {
            "company": _text(item.get("company") or item.get("companyName") or item.get("Company")),
            "title": _text(item.get("title") or item.get("position") or item.get("role")),
            "duration": _text(item.get("duration")),
            "description": _text(item.get("description") or item.get("summary")),
        }
        if any(normalized.values()):
            result.append(normalized)
    return result


def _normalize_projects(items: Any) -> list[dict[str, str]]:
    result: list[dict[str, str]] = []
    if not isinstance(items, list):
        return result
    for item in items:
        if not isinstance(item, dict):
            continue
        normalized = {
            "name": _text(item.get("projectName") or item.get("name")),
            "role": _text(item.get("roleDescription") or item.get("role")),
            "technology_stack": _text(
                item.get("technologyStack") or item.get("technology_stack") or item.get("technologies")
            ),
            "summary": _text(item.get("projectSummary") or item.get("summary") or item.get("description")),
            "duration": _text(item.get("duration")),
        }
        if any(normalized.values()):
            result.append(normalized)
    return result


def _normalize_skills(items: Any) -> list[dict[str, str]]:
    result: list[dict[str, str]] = []
    seen: set[tuple[str, str]] = set()
    if isinstance(items, dict):
        items = [
            {"skillName": skill, "category": category}
            for category, skills in items.items()
            for skill in (skills if isinstance(skills, list) else [skills])
        ]
    if not isinstance(items, list):
        return result
    for item in items:
        if isinstance(item, str):
            name, category, source = item, "Other", "CV"
        elif isinstance(item, dict):
            name = _text(item.get("skillName") or item.get("name") or item.get("skill"))
            category = _text(item.get("category")) or "Other"
            source = _text(item.get("source")) or "CV"
        else:
            continue
        name = _text(name)
        key = (name.casefold(), category.casefold())
        if name and key not in seen:
            result.append({"name": name, "category": category, "source": source})
            seen.add(key)
    return result


def normalize_cv_profile(data: dict[str, Any]) -> dict[str, Any]:
    """Normalize either a CV database row or the extracted RawAiOutput JSON.

    The returned profile is sent directly to the LLM. It is not chunked or stored in Qdrant.
    """
    if not isinstance(data, dict):
        raise ValueError("cv_profile must be a JSON object")

    raw_value = data.get("RawAiOutput")
    raw = parse_loose_json(raw_value, {}) if raw_value is not None else data
    if not isinstance(raw, dict):
        raw = {}

    education_source = raw.get("education")
    if not education_source:
        education_source = parse_loose_json(data.get("Education"), [])

    experience_source = raw.get("experience")
    if not experience_source:
        experience_source = parse_loose_json(data.get("Experience"), [])

    projects_source = raw.get("projects") or data.get("projects") or []
    skills_source = raw.get("skills") or data.get("skills") or []

    profile = {
        "candidate_id": _text(
            data.get("candidate_id") or data.get("ExtractedProfileId") or raw.get("candidateId")
        ),
        "cv_file_id": _text(data.get("cv_file_id") or data.get("CVFileId")),
        "role_target": _text(
            data.get("role_target")
            or data.get("RoleTarget")
            or raw.get("roleTarget")
            or data.get("job_role")
            or data.get("JobRole")
            or raw.get("jobRole")
        ),
        "experience_level": _text(
            data.get("experience_level")
            or data.get("ExperienceLevel")
            or raw.get("experienceLevel")
        ),
        "education": _normalize_education(education_source),
        "experience": _normalize_experience(experience_source),
        "projects": _normalize_projects(projects_source),
        "skills": _normalize_skills(skills_source),
        "overall_assessment": _text(
            data.get("overall_assessment")
            or data.get("OverallAssessment")
            or raw.get("overallAssessment")
        ),
        "strengths": _text(data.get("strengths") or data.get("Strengths") or raw.get("strengths")),
        "weaknesses": _text(
            data.get("weaknesses") or data.get("Weaknesses") or raw.get("weaknesses")
        ),
    }

    if not any(
        [profile["role_target"], profile["education"], profile["experience"], profile["projects"], profile["skills"]]
    ):
        raise ValueError("CV profile does not contain role, education, experience, projects, or skills")
    return profile


def get_cv_profile_from_csv(path: Path, candidate_id: str) -> dict[str, Any]:
    df = read_cv_csv(path)
    ids = df["ExtractedProfileId"].astype(str).str.replace(r"\.0$", "", regex=True)
    wanted = str(candidate_id).removesuffix(".0")
    rows = df.loc[ids == wanted]
    if rows.empty:
        raise ValueError(f"Candidate {candidate_id} was not found in {path}")
    row = {key: clean_scalar(value) for key, value in rows.iloc[0].to_dict().items()}
    return normalize_cv_profile(row)


def profile_to_context(profile: dict[str, Any], max_chars: int = 18000) -> str:
    """Render one structured CV as compact evidence for the LLM."""
    normalized = normalize_cv_profile(profile)
    lines: list[str] = []
    if normalized["candidate_id"]:
        lines.append(f"Candidate ID: {normalized['candidate_id']}")
    if normalized["role_target"]:
        lines.append(f"Target role: {normalized['role_target']}")

    if normalized["skills"]:
        grouped: dict[str, list[str]] = {}
        for skill in normalized["skills"]:
            grouped.setdefault(skill["category"], []).append(skill["name"])
        for category, names in grouped.items():
            lines.append(f"Skills - {category}: {', '.join(names)}")

    for index, project in enumerate(normalized["projects"], start=1):
        lines.append(
            "Project {idx}: {name}; role: {role}; technologies: {tech}; summary: {summary}; duration: {duration}".format(
                idx=index,
                name=project["name"],
                role=project["role"],
                tech=project["technology_stack"],
                summary=project["summary"],
                duration=project["duration"],
            )
        )

    for index, item in enumerate(normalized["experience"], start=1):
        lines.append(
            "Experience {idx}: {title} at {company}; duration: {duration}; details: {description}".format(
                idx=index,
                title=item["title"],
                company=item["company"],
                duration=item["duration"],
                description=item["description"],
            )
        )

    for index, item in enumerate(normalized["education"], start=1):
        lines.append(
            "Education {idx}: {major} at {school}; GPA: {gpa}; graduation year: {year}".format(
                idx=index,
                major=item["major"],
                school=item["school"],
                gpa=item["gpa"],
                year=item["graduation_year"],
            )
        )

    for label, key in (
        ("Overall assessment", "overall_assessment"),
        ("Strengths", "strengths"),
        ("Weaknesses", "weaknesses"),
    ):
        if normalized[key]:
            lines.append(f"{label}: {normalized[key]}")

    return "\n".join(lines)[:max_chars]


def profile_search_query(profile: dict[str, Any], interview_type: str) -> str:
    normalized = normalize_cv_profile(profile)
    skills = [item["name"] for item in normalized["skills"]]
    project_tech = [item["technology_stack"] for item in normalized["projects"] if item["technology_stack"]]
    project_names = [item["name"] for item in normalized["projects"] if item["name"]]
    parts = [
        normalized["role_target"],
        f"Experience level: {normalized['experience_level']}" if normalized.get("experience_level") else "",
        "technical interview" if interview_type == "technical" else "behavioral STAR interview",
        "Skills: " + ", ".join(skills[:30]) if skills else "",
        "Project technologies: " + ", ".join(project_tech[:10]) if project_tech else "",
        "Projects: " + ", ".join(project_names[:10]) if project_names else "",
    ]
    return ". ".join(part for part in parts if part)


def profile_to_json(profile: dict[str, Any]) -> str:
    return json.dumps(normalize_cv_profile(profile), ensure_ascii=False, indent=2)
