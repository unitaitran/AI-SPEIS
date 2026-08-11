import React from 'react';
import { render, screen } from '@testing-library/react';
import TechnicalV2ResultView from './TechnicalV2ResultView';

const labels = {
  'result.overallScore': 'Overall score',
  'result.rubricEyebrow': 'Evaluation rubric',
  'result.rubricDimensions': 'Technical criteria',
  'result.questionsEyebrow': 'Question evidence',
  'result.questionBreakdown': 'Question breakdown',
  'result.questionCriteria': 'Criterion evaluation',
  'result.mainQuestion': ({ index }) => `Question ${index}`,
  'result.weight': ({ weight }) => `Weight: ${weight}`,
  'result.evidence': 'Evidence',
  'result.strengths': 'Strengths',
  'result.gaps': 'Gaps',
  'result.recommendations': 'Recommendations',
  'result.feedbackPending': 'Feedback pending',
  'result.noSummary': 'No summary',
  'result.levelAssessment': 'Level assessment',
  'result.finalFeedbackEyebrow': 'Final feedback',
  'result.finalFeedback': 'Final feedback',
  'result.noQuestions': 'No questions',
  'result.noDimensions': 'No criteria',
  'rubric.ACCURACY': 'Accuracy',
  'rubric.TECHNICAL_DEPTH': 'Technical Depth',
  'rubric.REASONING': 'Reasoning',
  'rubric.APPLICATION': 'Application',
  'rubric.COMMUNICATION': 'Communication',
};

const t = (key, options = {}) => {
  const value = labels[key];
  return typeof value === 'function' ? value(options) : value || options.defaultValue || key;
};

const result = {
  overallScore: 8.1,
  performanceBand: 'GOOD',
  finalFeedbackStatus: 'COMPLETED',
  mainQuestions: [{
    sessionQuestionId: 10,
    questionOrder: 1,
    question: 'Explain dependency injection.',
    answerTranscript: 'It separates construction from use.',
    score: 8.1,
    evaluationStatus: 'COMPLETED',
    dimensions: [
      { rubricCode: 'ACCURACY', score: 7.9, weight: 0.30, evidence: ['It separates construction from use.'], strengths: ['Correct concept.'], gaps: [] },
      { rubricCode: 'TECHNICAL_DEPTH', score: 8.5, weight: 0.25, evidence: [], strengths: [], gaps: [] },
      { rubricCode: 'REASONING', score: 8, weight: 0.20, evidence: [], strengths: [], gaps: [] },
      { rubricCode: 'APPLICATION', score: 7.5, weight: 0.15, evidence: [], strengths: [], gaps: [] },
      { rubricCode: 'COMMUNICATION', score: 8.5, weight: 0.10, evidence: [], strengths: [], gaps: [] },
    ],
    strengths: ['Clear answer.'],
    missingPoints: [],
  }],
  summary: {
    overallTechnicalAssessment: 'Strong technical answer.',
    strengths: ['Clear answer.'],
    knowledgeGaps: [],
    recommendationsForImprovement: [],
  },
};

test('renders the five Technical V2 criteria and excludes dashboard labels', () => {
  render(<TechnicalV2ResultView result={result} t={t} />);

  ['Accuracy', 'Technical Depth', 'Reasoning', 'Application', 'Communication'].forEach((label) => {
    expect(screen.getAllByText(label).length).toBeGreaterThan(0);
  });
  ['Weight: 30%', 'Weight: 25%', 'Weight: 20%', 'Weight: 15%', 'Weight: 10%'].forEach((weight) => {
    expect(screen.getAllByText(weight).length).toBeGreaterThan(0);
  });
  expect(screen.getByText('Overall score')).toBeInTheDocument();
  expect(screen.getAllByText('It separates construction from use.').length).toBeGreaterThan(0);
  expect(screen.queryByText('Professional Knowledge')).not.toBeInTheDocument();
  expect(screen.queryByText('Technical Accuracy')).not.toBeInTheDocument();
  expect(screen.queryByText('Problem Solving & Reasoning')).not.toBeInTheDocument();
  expect(screen.queryByText('Communication & Explanation')).not.toBeInTheDocument();
});

test('renders zero score for a criterion with no evidence', () => {
  const noApplicationEvidenceResult = {
    ...result,
    mainQuestions: [{
      ...result.mainQuestions[0],
      dimensions: result.mainQuestions[0].dimensions.map((dimension) => (
        dimension.rubricCode === 'APPLICATION'
          ? { ...dimension, score: 0, evidence: [], gaps: ['No concrete real-world application example was provided.'] }
          : dimension
      )),
    }],
  };

  render(<TechnicalV2ResultView result={noApplicationEvidenceResult} t={t} />);

  expect(screen.getAllByText('0.00/10').length).toBeGreaterThan(0);
});
