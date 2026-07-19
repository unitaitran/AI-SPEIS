import React from 'react';
import { render, screen } from '@testing-library/react';
import TechnicalQuestionPanel from './TechnicalQuestionPanel';
import TechnicalInterviewProgress from './TechnicalInterviewProgress';
import TechnicalRubricBreakdown from './TechnicalRubricBreakdown';

const t = (key, options = {}) => {
  const labels = {
    'room.progressAria': 'Main question progress',
    'room.questionProgress': `Question ${options.current} of ${options.total}`,
    'room.mainQuestionsOnly': 'Main questions only',
    'room.interviewerAsks': 'Interviewer asks',
    'room.questionTitle': 'Current question',
    'room.questionTypes.CLARIFICATION': 'Clarification',
    'room.subQuestionContext': 'Continuation of current main question',
    'result.rubricEyebrow': 'Rubric detail',
    'result.rubricDimensions': 'Rubric dimensions',
    'result.notAvailable': 'N/A',
    'result.dimensionScoreAlternative': `${options.name}: ${options.score}/${options.maxScore}`,
    'result.strengths': 'Strengths',
    'result.missingEvidence': 'Missing evidence',
    'result.suggestions': 'Suggestions',
    'result.noDimensions': 'No dimensions',
  };
  return labels[key] || key;
};

describe('technical interview components', () => {
  test('renders a clarification with questionId null and backend-owned main progress', () => {
    render(
      <>
        <TechnicalInterviewProgress current={2} total={5} t={t} />
        <TechnicalQuestionPanel
          t={t}
          question={{
            attemptId: 'clarification-2',
            questionId: null,
            questionType: 'CLARIFICATION',
            content: 'Which browser constraints matter?',
            mainQuestionIndex: 2,
            totalMainQuestions: 5,
          }}
        />
      </>,
    );

    expect(screen.getByText('Question 2 of 5')).toBeInTheDocument();
    expect(screen.getByText('Clarification')).toBeInTheDocument();
    expect(screen.getByText('Which browser constraints matter?')).toBeInTheDocument();
  });

  test('renders every dimension returned by the backend and respects each maxScore', () => {
    const dimensions = [
      { rubricCode: 'A', name: 'Accuracy', score: 4, maxScore: 5 },
      { rubricCode: 'D', name: 'Depth', score: 7, maxScore: 10 },
      { rubricCode: 'C', name: 'Communication', score: 2, maxScore: 4 },
    ];
    render(<TechnicalRubricBreakdown dimensions={dimensions} t={t} />);

    expect(screen.getByText('Accuracy')).toBeInTheDocument();
    expect(screen.getByText('Depth')).toBeInTheDocument();
    expect(screen.getByText('Communication')).toBeInTheDocument();
    expect(screen.getAllByRole('progressbar')).toHaveLength(3);
    expect(screen.getByRole('progressbar', { name: 'Depth: 7/10' })).toHaveAttribute('aria-valuemax', '10');
  });
});

