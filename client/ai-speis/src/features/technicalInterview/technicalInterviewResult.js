import { TechnicalQuestionType } from './technicalInterview.types';

const getQuestionType = (question) => question?.questionType || question?.type;
const firstNonEmptyArray = (...values) => (
  values.find((value) => Array.isArray(value) && value.length > 0) || []
);

const scaleValueTo10 = (score, maxScore) => {
  const numericScore = Number(score);
  const numericMaxScore = Number(maxScore);
  if (!Number.isFinite(numericScore)) return null;
  if (!Number.isFinite(numericMaxScore) || numericMaxScore <= 0) return numericScore;

  if (numericMaxScore === 10) return Number(numericScore.toFixed(1));

  const scaled = (numericScore / numericMaxScore) * 10;
  return Number(Math.min(10, Math.max(0, scaled)).toFixed(1));
};

const normalizeDimension = (dimension, parentMaxScore) => {
  const itemMax = dimension.maxScore ?? parentMaxScore ?? 5;
  const scaledScore = scaleValueTo10(dimension.score, itemMax);
  return {
    ...dimension,
    score: scaledScore,
    maxScore: 10,
    evidence: dimension.evidence || dimension.strengths || [],
    missingEvidence: dimension.missingEvidence || [],
    incorrectClaims: dimension.incorrectClaims || [],
  };
};

const normalizeSubQuestion = (question, parentMaxScore) => {
  const itemMax = question.maxScore ?? parentMaxScore ?? 5;
  const rawScore = question.rawScore ?? question.score;
  const scaledScore = scaleValueTo10(rawScore, itemMax);
  return {
    ...question,
    questionId: question.questionId ?? null,
    questionType: getQuestionType(question),
    content: question.content || question.question,
    rawScore: scaledScore,
    score: scaledScore,
    maxScore: 10,
  };
};

const normalizeMainQuestion = (question, parentMaxScore) => {
  const itemMax = question.maxScore ?? parentMaxScore ?? 5;
  const initialScore = scaleValueTo10(question.initialMainScore ?? question.score, itemMax);
  const finalScore = scaleValueTo10(question.finalMainScore ?? question.score, itemMax);
  return {
    ...question,
    questionType: TechnicalQuestionType.MAIN,
    content: question.content || question.question,
    initialMainScore: initialScore,
    finalMainScore: finalScore,
    score: finalScore,
    rubricBreakdown: (question.rubricBreakdown || question.dimensions || [])
      .map((dimension) => normalizeDimension(dimension, itemMax)),
    subQuestionResults: (question.subQuestionResults
      || question.subQuestions
      || question.adaptiveHistory
      || question.attempts
      || []).map((subQuestion) => normalizeSubQuestion(subQuestion, itemMax)),
    suggestions: question.suggestions || question.improvementSuggestions || [],
    maxScore: 10,
  };
};

const normalizeSkill = (skill, parentMaxScore) => {
  const itemMax = skill.maxScore ?? parentMaxScore ?? 5;
  const scaledScore = scaleValueTo10(skill.score, itemMax);
  return {
    ...skill,
    score: scaledScore,
    maxScore: 10,
  };
};

export const normalizeTechnicalInterviewResult = (result) => {
  if (!result) return result;

  const summary = result.summary || {};
  const rawMaxScore = result.maxScore ?? 5;
  const technicalScore = scaleValueTo10(result.technicalScore ?? result.overallScore, rawMaxScore);
  const backendMainQuestions = result.mainQuestionResults?.length
    ? result.mainQuestionResults
    : result.mainQuestions || result.questionResults || [];
  const rawSkills = firstNonEmptyArray(result.skillResults, result.skillScores);

  return {
    ...result,
    technicalScore,
    maxScore: 10,
    summaryFeedback: result.summaryFeedback
      || summary.overallTechnicalAssessment
      || summary.summary
      || '',
    summaryStrengths: firstNonEmptyArray(
      result.summaryStrengths,
      summary.strengths,
      result.strengths,
    ),
    areasForImprovement: firstNonEmptyArray(
      result.areasForImprovement,
      summary.knowledgeGaps,
      summary.areasForImprovement,
      result.weaknesses,
    ),
    recommendations: firstNonEmptyArray(
      result.recommendations,
      summary.recommendationsForImprovement,
      summary.recommendedNextSteps,
    ),
    skillResults: rawSkills.map((skill) => normalizeSkill(skill, rawMaxScore)),
    dimensionResults: (result.dimensionResults || [])
      .map((dimension) => normalizeDimension(dimension, rawMaxScore)),
    questionResults: backendMainQuestions.map((question) => normalizeMainQuestion(question, rawMaxScore)),
  };
};

export const formatTechnicalWeight = (weight) => {
  const numericWeight = Number(weight);
  if (!Number.isFinite(numericWeight)) return null;
  const percentage = numericWeight <= 1 ? numericWeight * 100 : numericWeight;
  return `${Number(percentage.toFixed(2))}%`;
};

export const getScorePercentage = (score, maxScore) => {
  const numericScore = Number(score);
  const numericMaxScore = Number(maxScore);
  if (!Number.isFinite(numericScore) || !Number.isFinite(numericMaxScore) || numericMaxScore <= 0) {
    return null;
  }
  return Math.min(100, Math.max(0, (numericScore / numericMaxScore) * 100));
};

export const groupTechnicalQuestionResults = (questionResults = []) => {
  if (!Array.isArray(questionResults)) return [];

  const mains = [];
  const byAttemptId = new Map();
  const byMainIndex = new Map();

  questionResults.forEach((question, order) => {
    if (getQuestionType(question) !== TechnicalQuestionType.MAIN) return;
    const normalized = {
      ...question,
      _displayOrder: order,
      subQuestionResults: [
        ...(question.subQuestionResults
          || question.subQuestions
          || question.adaptiveHistory
          || question.attempts
          || []),
      ],
    };
    mains.push(normalized);
    if (question.attemptId) byAttemptId.set(String(question.attemptId), normalized);
    if (question.mainQuestionIndex != null) byMainIndex.set(String(question.mainQuestionIndex), normalized);
  });

  questionResults.forEach((question, order) => {
    if (getQuestionType(question) === TechnicalQuestionType.MAIN) return;
    const parentKey = question.parentAttemptId || question.mainAttemptId || question.mainQuestionAttemptId;
    const parent = (parentKey != null && byAttemptId.get(String(parentKey)))
      || (question.mainQuestionIndex != null && byMainIndex.get(String(question.mainQuestionIndex)));

    if (parent) {
      parent.subQuestionResults.push(question);
      return;
    }

    // Public results must never promote a sub-question to a main-question card.
  });

  return mains.sort((left, right) => {
    const leftIndex = Number(left.mainQuestionIndex);
    const rightIndex = Number(right.mainQuestionIndex);
    if (Number.isFinite(leftIndex) && Number.isFinite(rightIndex) && leftIndex !== rightIndex) {
      return leftIndex - rightIndex;
    }
    return left._displayOrder - right._displayOrder;
  });
};
