export const TECHNICAL_V2_CRITERION_CODES = Object.freeze([
  'ACCURACY',
  'TECHNICAL_DEPTH',
  'REASONING',
  'APPLICATION',
  'COMMUNICATION',
]);

export const orderTechnicalV2Dimensions = (dimensions) => {
  const byCode = new Map(
    (Array.isArray(dimensions) ? dimensions : [])
      .filter((dimension) => dimension?.rubricCode)
      .map((dimension) => [String(dimension.rubricCode).toUpperCase(), dimension]),
  );

  return TECHNICAL_V2_CRITERION_CODES
    .map((code) => byCode.get(code))
    .filter(Boolean);
};

export const getTechnicalV2CriterionDefinitions = (questions) => (
  orderTechnicalV2Dimensions(
    (Array.isArray(questions) ? questions : []).flatMap((question) => question?.dimensions || []),
  )
);

export const normalizeTechnicalV2Review = (result) => {
  if (!result) return null;
  const summary = result.summary || {};
  return {
    runtimeVersion: 'V2',
    overallScore: Number.isFinite(Number(result.overallScore)) ? Number(result.overallScore) : 0,
    maxScore: 10,
    finalFeedbackStatus: result.finalFeedbackStatus || 'NOT_STARTED',
    questions: (result.mainQuestions || []).map((question, index) => {
      const subQuestions = (question.subQuestions || []).map((subQ, subIndex) => ({
        id: subQ.sessionQuestionId || subQ.questionId || subIndex,
        attemptId: subQ.sessionQuestionId || subIndex,
        questionOrder: subQ.questionOrder,
        question: subQ.question || '',
        questionType: subQ.questionType || 'SUB',
        skill: subQ.skill || question.skill || '',
        score: Number.isFinite(Number(subQ.score)) ? Number(subQ.score) : 0,
        maxScore: 10,
        dimensions: orderTechnicalV2Dimensions(subQ.dimensions).map((dimension) => ({
          ...dimension,
          maxScore: 10,
          level: dimension.rubricCode,
        })),
        strengths: subQ.strengths || [],
        missingPoints: subQ.missingPoints || [],
        transcript: subQ.answerTranscript || '',
        answerTranscript: subQ.answerTranscript || '',
      }));

      return {
        id: question.sessionQuestionId || question.questionId || index,
        questionId: question.questionId || null,
        order: question.questionOrder || index + 1,
        question: question.question || '',
        questionType: 'MAIN',
        skill: question.skill || '',
        score: Number.isFinite(Number(question.score)) ? Number(question.score) : 0,
        maxScore: 10,
        dimensions: orderTechnicalV2Dimensions(question.dimensions).map((dimension) => ({
          ...dimension,
          maxScore: 10,
          level: dimension.rubricCode,
        })),
        strengths: question.strengths || [],
        missingPoints: question.missingPoints || [],
        transcript: question.answerTranscript || '',
        feedbackSummary: '',
        suggestions: summary.recommendationsForImprovement || [],
        subQuestions,
        adaptiveHistory: subQuestions,
      };
    }),
  };
};
