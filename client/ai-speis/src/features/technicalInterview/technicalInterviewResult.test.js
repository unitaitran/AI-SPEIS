import { getScorePercentage, groupTechnicalQuestionResults } from './technicalInterviewResult';

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
});

