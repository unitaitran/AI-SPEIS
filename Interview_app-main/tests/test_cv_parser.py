from src.cv_parser import normalize_cv_profile, profile_search_query, profile_to_context


def sample_row():
    return {
        "ExtractedProfileId": 1,
        "RoleTarget": "Backend Developer",
        "RawAiOutput": {
            "projects": [
                {
                    "projectName": "Interview AI",
                    "roleDescription": "Backend",
                    "technologyStack": "FastAPI, Qdrant",
                    "projectSummary": "Generated interview questions",
                    "duration": "3 months",
                }
            ],
            "skills": [
                {"skillName": "Python", "category": "Language", "source": "CV"},
                {"skillName": "FastAPI", "category": "Framework", "source": "CV"},
            ],
        },
    }


def test_normalize_profile_is_one_object_not_chunks():
    profile = normalize_cv_profile(sample_row())
    assert profile["candidate_id"] == "1"
    assert profile["role_target"] == "Backend Developer"
    assert profile["projects"][0]["name"] == "Interview AI"
    assert profile["skills"][1]["name"] == "FastAPI"
    assert "chunks" not in profile


def test_profile_context_and_query():
    profile = normalize_cv_profile(sample_row())
    context = profile_to_context(profile)
    query = profile_search_query(profile, "technical")
    assert "Interview AI" in context
    assert "FastAPI" in context
    assert "Backend Developer" in query
    assert "Qdrant" in query
