import { TechnicalQuestionType } from './technicalInterview.types';

const getQuestionType = (question) => question?.questionType || question?.type;

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
        ...(question.subQuestionResults || question.subQuestions || question.attempts || []),
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

    mains.push({
      ...question,
      _displayOrder: order,
      isOrphanSubQuestion: true,
      subQuestionResults: [],
    });
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

