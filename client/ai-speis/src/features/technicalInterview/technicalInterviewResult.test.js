import {
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
      overallScore: 4.25,
      performanceBand: 'Strong',
      mainQuestions: [{
        attemptId: 'attempt-1',
        mainQuestionIndex: 1,
        question: 'Explain dependency inversion.',
        skill: 'Architecture',
        score: 4.25,
        dimensions: [{ rubricCode: 'accuracy', name: 'Accuracy', score: 4.5 }],
        improvementSuggestions: ['Add a trade-off.'],
      }],
      skillScores: [{ skill: 'Architecture', mainQuestionCount: 1, score: 4.25 }],
      summary: {
        summary: 'Strong technical foundation.',
        strengths: ['Clear reasoning'],
        areasForImprovement: ['Discuss trade-offs'],
        recommendedNextSteps: ['Practice system design'],
      },
    });

    expect(result.overallScore).toBe(4.25);
    expect(result.summaryFeedback).toBe('Strong technical foundation.');
    expect(result.skillResults).toHaveLength(1);
    expect(result.recommendations).toEqual(['Practice system design']);
    expect(result.questionResults[0]).toMatchObject({
      questionType: 'MAIN',
      content: 'Explain dependency inversion.',
      score: 4.25,
    });
    expect(result.questionResults[0].rubricBreakdown[0].rubricCode).toBe('accuracy');
  });
});
