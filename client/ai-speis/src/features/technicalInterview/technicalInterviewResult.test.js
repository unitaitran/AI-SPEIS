import {
  formatTechnicalWeight,
  getScorePercentage,
  groupTechnicalQuestionResults,
  normalizeTechnicalInterviewResult,
} from './technicalInterviewResult';

describe('technical interview result helpers', () => {
  test('groups nullable-id clarification and follow-up attempts under the backend main index', () => {
    const result = groupTechnicalQuestionResults([
      {
        attemptId: 'main-1',
        questionId: 'question-1',
        questionType: 'MAIN',
        mainQuestionIndex: 1,
        content: 'Explain event delegation.',
      },
      {
        attemptId: 'clarification-1',
        questionId: null,
        questionType: 'CLARIFICATION',
        mainQuestionIndex: 1,
        content: 'Can you give a practical example?',
      },
      {
        attemptId: 'follow-up-1',
        questionId: null,
        questionType: 'FOLLOW_UP',
        mainQuestionIndex: 1,
        content: 'What are the trade-offs?',
      },
    ]);

    expect(result).toHaveLength(1);
    expect(result[0].mainQuestionIndex).toBe(1);
    expect(result[0].subQuestionResults).toHaveLength(2);
    expect(result[0].subQuestionResults[0].questionId).toBeNull();
  });

  test('uses the API maxScore instead of assuming a fixed scale', () => {
    expect(getScorePercentage(3, 5)).toBe(60);
    expect(getScorePercentage(7.5, 10)).toBe(75);
    expect(getScorePercentage(undefined, 5)).toBeNull();
  });

  test('maps the backend result DTO to the result components without recalculating scores', () => {
    const result = normalizeTechnicalInterviewResult({
      sessionId: 17,
      overallScore: 6.33,
      technicalScore: 6.33,
      maxScore: 10,
      performanceBand: 'Strong',
      mainQuestionResults: [
        {
          attemptId: 'attempt-1',
          mainQuestionIndex: 1,
          question: 'Explain dependency inversion.',
          skill: 'Architecture',
          initialMainScore: 4,
          finalMainScore: 5,
          cumulativeFollowUpBonus: 1,
          dimensions: [{ rubricCode: 'accuracy', name: 'Accuracy', score: 4.5, weight: 0.3 }],
          adaptiveHistory: [{
            attemptId: 'follow-up-1',
            questionType: 'FOLLOW_UP',
            question: 'Give an example.',
            rawScore: 7,
            followUpBonus: 1,
          }],
          improvementSuggestions: ['Add a trade-off.'],
        },
        { attemptId: 'attempt-2', mainQuestionIndex: 2, question: 'Question 2', finalMainScore: 8 },
        { attemptId: 'attempt-3', mainQuestionIndex: 3, question: 'Question 3', finalMainScore: 6 },
      ],
      skillScores: [{ skill: 'Architecture', mainQuestionCount: 1, score: 4.25 }],
      summary: {
        summary: 'Strong technical foundation.',
        strengths: ['Clear reasoning'],
        areasForImprovement: ['Discuss trade-offs'],
        recommendedNextSteps: ['Practice system design'],
      },
    });

    expect(result.technicalScore).toBe(6.33);
    expect(result.summaryFeedback).toBe('Strong technical foundation.');
    expect(result.skillResults).toHaveLength(1);
    expect(result.recommendations).toEqual(['Practice system design']);
    expect(result.questionResults[0]).toMatchObject({
      questionType: 'MAIN',
      content: 'Explain dependency inversion.',
      initialMainScore: 4,
      finalMainScore: 5,
      cumulativeFollowUpBonus: 1,
    });
    expect(result.questionResults).toHaveLength(3);
    expect(result.questionResults[0].subQuestionResults[0]).toMatchObject({
      attemptId: 'follow-up-1',
      questionId: null,
      followUpBonus: 1,
    });
    expect(result.questionResults[0].rubricBreakdown[0].rubricCode).toBe('accuracy');
    expect(result.questionResults[0].rubricBreakdown[0].maxScore).toBe(10);
  });

  test('does not substitute overallScore when the backend omits technicalScore', () => {
    const result = normalizeTechnicalInterviewResult({
      overallScore: 8.5,
      maxScore: 10,
      mainQuestionResults: [],
    });

    expect(result.technicalScore).toBeUndefined();
  });

  test('formats API dimension weights for display without calculating a score', () => {
    expect(formatTechnicalWeight(0.3)).toBe('30%');
    expect(formatTechnicalWeight(25)).toBe('25%');
    expect(formatTechnicalWeight(undefined)).toBeNull();
  });

  test('never promotes an orphan sub-question into a main result', () => {
    const grouped = groupTechnicalQuestionResults([{
      attemptId: 'orphan-follow-up',
      questionType: 'FOLLOW_UP',
      mainQuestionIndex: 1,
      content: 'Orphaned follow-up',
    }]);

    expect(grouped).toEqual([]);
  });
});
