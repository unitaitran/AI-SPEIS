QUESTION_SYSTEM = """You are an AI interviewer. The supplied CV profile is direct session data, not retrieved knowledge. Generate questions only from explicit CV facts and retrieved question-bank templates. Never claim the candidate has experience that is absent from the CV. Return valid JSON only."""

ANSWER_EVAL_RULES = """
1. Match key points by semantic meaning, not exact wording.
2. Score each criterion independently from 0 to 5.
3. Accuracy measures correctness, not answer length.
4. Depth measures explanation and technical understanding.
5. Reasoning measures logic and justification.
6. Application measures practical examples or real-world use.
7. Communication measures clarity and organization.
8. A concise but correct answer must not receive a low score only because it is short.
9. Use different, evidence-based rationale for each criterion.
10. Mark a key point as covered only when its meaning appears in the answer.
11. Do not invent evidence or incorrect claims.
12. Return valid JSON only.
""".strip()

EVALUATION_SYSTEM = """You are a strict but fair interview evaluator. Evaluate only the candidate answer against the supplied question, expected points, direct CV profile, and rubric. Do not invent evidence. Return valid JSON only. Scores must be numbers from 0 to 5."""

FOLLOWUP_SYSTEM = """You are an adaptive AI interviewer. Ask exactly one concise follow-up question based on the candidate's latest rubric score and missing evidence. Do not reveal scores, rubric labels, expected answers, or hidden evaluation notes. Return valid JSON only."""


def difficulty_plan(count: int) -> list[str]:
    if count == 1:
        return ["Medium"]
    if count == 2:
        return ["Easy", "Medium"]
    if count == 3:
        return ["Easy", "Medium", "Hard"]
    raise ValueError("count must be between 1 and 3")


def question_prompt(
    cv_context: str,
    templates: str,
    interview_type: str,
    count: int,
    language: str,
) -> str:
    criteria = (
        "Technical questions must test accuracy, depth, reasoning, practical application, and communication."
        if interview_type == "technical"
        else "Behavioral questions must invite a STAR answer: Situation, Action/Ownership, Result/Reflection, target competency, and communication."
    )
    plan = difficulty_plan(count)
    numbered_plan = ", ".join(f"Q{i + 1}={level}" for i, level in enumerate(plan))
    return f"""
Create exactly {count} MAIN {interview_type} interview questions in language '{language}'.
{criteria}

Required difficulty order: {numbered_plan}.
The questions array must already be ordered according to this plan.

Rules:
1. Every personalized claim must be supported by DIRECT CV PROFILE evidence below.
2. Use retrieved question templates only as question patterns and expected-answer guidance.
3. Prefer project deep-dive questions when project evidence exists.
4. Do not ask about a technology as candidate experience unless it appears in the CV.
5. Avoid duplicate skills and nearly identical questions.
6. Include expected key points and up to two candidate follow-up ideas. These ideas are hidden from the candidate and may later be adapted from rubric scores.
7. Copy a concise exact CV fact into cv_evidence; do not use a template as cv_evidence.
8. Easy checks fundamentals or stated usage; Medium checks implementation and reasoning; Hard checks trade-offs, architecture, optimization, failure handling, or reflection.

DIRECT CV PROFILE:
{cv_context}

RETRIEVED QUESTION-BANK TEMPLATES GROUPED BY DIFFICULTY:
{templates}

Return this JSON schema:
{{
  "questions": [
    {{
      "question_id": "generated-1",
      "interview_type": "{interview_type}",
      "question": "...",
      "skill_or_competency": "...",
      "difficulty": "Easy|Medium|Hard",
      "cv_evidence": "concise direct fact from CV supporting the question",
      "source_template_id": "template id or null",
      "expected_key_points": ["..."],
      "follow_ups": ["..."]
    }}
  ]
}}
""".strip()


def evaluation_prompt(
    interview_type: str,
    question: dict,
    answer: str,
    cv_context: str,
) -> str:
    if interview_type == "technical":
        score_schema = '"accuracy": 0, "depth": 0, "reasoning": 0, "application": 0, "communication": 0'
        rubric = "accuracy 30%, depth 25%, reasoning 20%, application 15%, communication 10%"
    else:
        score_schema = '"situation": 0, "action": 0, "result": 0, "competency": 0, "communication": 0'
        rubric = "situation 15%, action/ownership 30%, result/reflection 20%, competency 20%, communication 15%"

    raw_key_points = question.get("expected_key_points") or []
    if isinstance(raw_key_points, str):
        raw_key_points = [item.strip() for item in raw_key_points.split(";") if item.strip()]
    key_point_catalog = [
        {"key_point_id": f"KP-{index}", "statement": str(point).strip()}
        for index, point in enumerate(raw_key_points, start=1)
        if str(point).strip()
    ]

    return f"""
Interview type: {interview_type}
Rubric: {rubric}

ORIGINAL MAIN QUESTION JSON:
{question}

CANDIDATE ANSWER TRANSCRIPT:
{answer}

DIRECT CV PROFILE:
{cv_context}

KEY POINT CATALOG (use only these IDs for covered_key_point_ids and missing_key_point_ids):
{key_point_catalog}

The answer transcript may contain the original answer and one or more follow-up exchanges. Evaluate the combined evidence as the candidate's final answer to the original main question.

Score anchors:
0 = no relevant evidence or incorrect.
1 = very limited understanding/evidence.
2 = basic answer with major gaps.
3 = mostly correct/clear with minor omissions.
4 = strong answer with practical examples.
5 = excellent depth, ownership, evidence, and trade-off/reflection.

Important:
{ANSWER_EVAL_RULES}
- Technical correctness is judged from the answer and expected key points.
- CV consistency checks whether claimed personal experience is supported by the CV.
- Do not punish a correct conceptual explanation merely because every detail is not written in the CV.
- Comments must reflect the entire transcript, including improvements supplied in follow-up answers.

Return JSON exactly in this structure:
{{
  "scores": {{{score_schema}}},
  "criterion_feedback": {{"criterion_name": "distinct brief evidence-based rationale"}},
  "covered_key_point_ids": ["KP-1"],
  "missing_key_point_ids": ["KP-2"],
  "strengths": ["..."],
  "improvements": ["..."],
  "incorrect_claims": ["..."],
  "cv_consistency": "consistent|partially_supported|unsupported|contradictory",
  "overall_comment": "...",
  "suggested_better_answer": "..."
}}
Do not calculate the weighted score; Python will calculate it deterministically.
""".strip()


def followup_prompt(
    *,
    interview_type: str,
    language: str,
    mode: str,
    main_question: dict,
    conversation: list[dict],
    evaluation: dict,
    followup_number: int,
    cv_context: str,
) -> str:
    mode_instruction = (
        "Ask for a simple clarification of a missing foundation, definition, concrete step, or STAR component. Do not make the question harder than necessary."
        if mode == "clarify_missing_foundation"
        else "Probe one missing detail, example, trade-off, ownership point, result, or piece of reasoning that would move the answer from acceptable to strong."
    )
    return f"""
Create follow-up question number {followup_number} in language '{language}' for a {interview_type} interview.

Adaptive mode: {mode}
{mode_instruction}

ORIGINAL MAIN QUESTION:
{main_question}

CONVERSATION SO FAR:
{conversation}

HIDDEN EVALUATION USED ONLY TO CHOOSE THE FOLLOW-UP:
{evaluation}

DIRECT CV PROFILE:
{cv_context}

Rules:
1. Ask exactly one question.
2. Target the weakest criterion or the first important missing key point.
3. Do not repeat a question already asked.
4. Keep it concise and answerable in approximately 30-90 seconds.
5. Do not mention the score, rubric, expected key points, or that the answer was weak.
6. Do not introduce candidate experience absent from the CV.
7. The follow-up remains attached to the original main question.

Return JSON:
{{
  "question_id": "follow-up-{followup_number}",
  "question": "...",
  "difficulty": "Easy|Medium|Hard",
  "skill_or_competency": "...",
  "target_reason": "internal concise reason"
}}
""".strip()
