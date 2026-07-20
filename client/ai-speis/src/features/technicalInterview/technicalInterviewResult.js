import { TechnicalQuestionType } from './technicalInterview.types';

const getQuestionType = (question) => question?.questionType || question?.type;
const firstNonEmptyArray = (...values) => (
  values.find((value) => Array.isArray(value) && value.length > 0) || []
);

const normalizeDimension = (dimension, maxScore) => ({
  ...dimension,
  maxScore: dimension.maxScore ?? maxScore,
  evidence: dimension.evidence || dimension.strengths || [],
  missingEvidence: dimension.missingEvidence || [],
  incorrectClaims: dimension.incorrectClaims || [],
});

const normalizeSubQuestion = (question, maxScore) => ({
  ...question,
  questionId: question.questionId ?? null,
  questionType: getQuestionType(question),
  content: question.content || question.question,
  maxScore: question.maxScore ?? maxScore,
});

const normalizeMainQuestion = (question, maxScore) => ({
  ...question,
  questionType: TechnicalQuestionType.MAIN,
  content: question.content || question.question,
  initialMainScore: question.initialMainScore ?? question.score,
  finalMainScore: question.finalMainScore ?? question.score,
  rubricBreakdown: (question.rubricBreakdown || question.dimensions || [])
    .map((dimension) => normalizeDimension(dimension, maxScore)),
  subQuestionResults: (question.subQuestionResults
    || question.subQuestions
    || question.adaptiveHistory
    || question.attempts
    || []).map((subQuestion) => normalizeSubQuestion(subQuestion, maxScore)),
  suggestions: question.suggestions || question.improvementSuggestions || [],
  maxScore: question.maxScore ?? maxScore,
});

export const normalizeTechnicalInterviewResult = (result) => {
  if (!result) return result;

  const summary = result.summary || {};
  const maxScore = result.maxScore ?? 10;
  const backendMainQuestions = result.mainQuestionResults?.length
    ? result.mainQuestionResults
    : result.mainQuestions || result.questionResults || [];
  return {
    ...result,
    technicalScore: result.technicalScore,
    maxScore,
    summaryFeedback: result.summaryFeedback || summary.summary || '',
    summaryStrengths: firstNonEmptyArray(
      result.summaryStrengths,
      summary.strengths,
      result.strengths,
    ),
    areasForImprovement: firstNonEmptyArray(
      result.areasForImprovement,
      summary.areasForImprovement,
      result.weaknesses,
    ),
    recommendations: firstNonEmptyArray(result.recommendations, summary.recommendedNextSteps),
    skillResults: firstNonEmptyArray(result.skillResults, result.skillScores),
    dimensionResults: (result.dimensionResults || [])
      .map((dimension) => normalizeDimension(dimension, maxScore)),
    questionResults: backendMainQuestions.map((question) => normalizeMainQuestion(question, maxScore)),
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
