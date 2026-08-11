import {
  TECHNICAL_V2_CRITERION_CODES,
  normalizeTechnicalV2Review,
} from './technicalV2InterviewResult';

test('normalizes the V2 result without recalculating the server score', () => {
  const result = normalizeTechnicalV2Review({
    overallScore: 7.35,
    finalFeedbackStatus: 'COMPLETED',
    summary: { recommendationsForImprovement: ['Practice trade-offs'] },
    mainQuestions: [{
      sessionQuestionId: 10,
      questionOrder: 1,
      question: 'Explain caching.',
      answerTranscript: 'Use a bounded cache.',
      score: 7.35,
      dimensions: [
        { rubricCode: 'COMMUNICATION', name: 'Communication', score: 7, weight: 0.10 },
        { rubricCode: 'APPLICATION', name: 'Application', score: 7, weight: 0.15 },
        { rubricCode: 'REASONING', name: 'Reasoning', score: 7, weight: 0.20 },
        { rubricCode: 'TECHNICAL_DEPTH', name: 'Technical Depth', score: 8, weight: 0.25 },
        { rubricCode: 'ACCURACY', name: 'Accuracy', score: 7, weight: 0.30 },
        { rubricCode: 'UNSUPPORTED_DASHBOARD_INDICATOR', name: 'Unsupported', score: 10, weight: 1 },
      ],
    }],
  });

  expect(result.overallScore).toBe(7.35);
  expect(result.runtimeVersion).toBe('V2');
  expect(result.questions[0].dimensions).toHaveLength(5);
  expect(result.questions[0].dimensions.map((dimension) => dimension.rubricCode)).toEqual(TECHNICAL_V2_CRITERION_CODES);
  expect(result.questions[0].transcript).toBe('Use a bounded cache.');
  expect(result.questions[0].suggestions).toEqual(['Practice trade-offs']);
});
