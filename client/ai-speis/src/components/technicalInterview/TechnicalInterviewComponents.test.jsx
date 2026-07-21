import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import TechnicalQuestionPanel from './TechnicalQuestionPanel';
import TechnicalInterviewProgress from './TechnicalInterviewProgress';
import TechnicalQuestionBreakdown from './TechnicalQuestionBreakdown';
import TechnicalRubricBreakdown from './TechnicalRubricBreakdown';
import TechnicalTranscriptEditor from './TechnicalTranscriptEditor';
import TechnicalTranscriptPanel from './TechnicalTranscriptPanel';

const t = (key, options = {}) => {
  const labels = {
    'room.progressAria': 'Main question progress',
    'room.questionProgress': `Question ${options.current} of ${options.total}`,
    'room.mainQuestionsOnly': 'Main questions only',
    'room.clarificationProgress': `Clarifying Main Question ${options.current}`,
    'room.followUpProgress': `Follow-up ${options.current} of ${options.total}`,
    'room.interviewerAsks': 'Interviewer asks',
    'room.questionTitle': 'Current question',
    'room.questionTypes.CLARIFICATION': 'Clarification',
    'room.questionTypes.FOLLOW_UP': 'Follow-up',
    'room.subQuestionContext': 'Continuation of current main question',
    'room.clarificationContext': `Clarifying Main Question ${options.current}`,
    'room.followUpContext': `Following up on Main Question ${options.current}`,
    'room.transcriptLabel': 'Answer transcript',
    'room.transcriptPlaceholder': 'Answer here',
    'room.transcriptHelper': 'Editable transcript',
    'room.transcriptReadOnly': 'Read-only transcript',
    'room.transcriptPanelTitle': 'Interview Transcript',
    'room.transcriptPanelDescription': 'Live interview transcript',
    'room.closeTranscript': 'Close transcript',
    'room.transcriptInterviewer': 'Interviewer',
    'room.transcriptCandidate': 'Candidate',
    'room.transcriptEmpty': 'No transcript yet',
    'room.transcriptStatuses.DRAFT': 'Draft',
    'result.rubricEyebrow': 'Rubric detail',
    'result.rubricDimensions': 'Rubric dimensions',
    'result.notAvailable': 'N/A',
    'result.dimensionScoreAlternative': `${options.name}: ${options.score}/${options.maxScore}`,
    'result.strengths': 'Strengths',
    'result.missingEvidence': 'Missing evidence',
    'result.suggestions': 'Suggestions',
    'result.noDimensions': 'No dimensions',
    'result.questionsEyebrow': 'Questions',
    'result.questionBreakdown': 'Question breakdown',
    'result.mainQuestion': `Main question ${options.index}`,
    'result.questionUnavailable': 'Question unavailable',
    'result.initialMainScore': 'Initial main score',
    'result.finalMainScore': 'Final main score',
    'result.followUpBonus': 'Total follow-up bonus',
    'result.candidateAnswer': 'Answer',
    'result.followUpResult': `Follow-up ${options.index}`,
    'result.clarificationResult': 'Clarification result',
    'result.subQuestionScore': `Score ${options.score}/${options.maxScore}`,
    'result.appliedBonus': `Bonus +${options.bonus}`,
    'result.questionRubric': 'Question rubric',
    'result.answerUnavailable': 'No answer',
    'result.missingPoints': 'Missing points',
    'result.incorrectClaims': 'Incorrect claims',
    'result.noQuestions': 'No questions',
    'result.weight': `Weight ${options.weight}`,
  };
  return labels[key] || key;
};

describe('technical interview components', () => {
  test('renders a clarification with questionId null and backend-owned main progress', () => {
    render(
      <>
        <TechnicalInterviewProgress
          question={{
            questionType: 'CLARIFICATION',
            mainQuestionIndex: 2,
            totalMainQuestions: 3,
            subQuestionIndex: 1,
            requiredSubQuestionCount: 1,
            completedSubQuestionCount: 0,
          }}
          t={t}
        />
        <TechnicalQuestionPanel
          t={t}
          question={{
            attemptId: 'clarification-2',
            questionId: null,
            questionType: 'CLARIFICATION',
            content: 'Which browser constraints matter?',
            mainQuestionIndex: 2,
            totalMainQuestions: 3,
            subQuestionIndex: 1,
            requiredSubQuestionCount: 1,
          }}
        />
      </>,
    );

    expect(screen.getByText('Question 2 of 3')).toBeInTheDocument();
    expect(screen.getByText('Clarification')).toBeInTheDocument();
    expect(screen.getAllByText('Clarifying Main Question 2')).toHaveLength(2);
    expect(screen.getByText('Which browser constraints matter?')).toBeInTheDocument();
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: 'Current question' }));
  });

  test('shows backend-provided one- and two-follow-up progress without advancing the main index', () => {
    const { rerender } = render(
      <TechnicalInterviewProgress
        question={{
          questionType: 'FOLLOW_UP',
          mainQuestionIndex: 3,
          totalMainQuestions: 3,
          subQuestionIndex: 1,
          requiredSubQuestionCount: 1,
          completedSubQuestionCount: 0,
        }}
        t={t}
      />,
    );
    expect(screen.getByText('Question 3 of 3')).toBeInTheDocument();
    expect(screen.getByText('Follow-up 1 of 1')).toBeInTheDocument();

    rerender(
      <TechnicalInterviewProgress
        question={{
          questionType: 'FOLLOW_UP',
          mainQuestionIndex: 3,
          totalMainQuestions: 3,
          subQuestionIndex: 1,
          requiredSubQuestionCount: 2,
          completedSubQuestionCount: 0,
        }}
        t={t}
      />,
    );
    expect(screen.getByText('Follow-up 1 of 2')).toBeInTheDocument();
    expect(screen.queryByText('Follow-up 2 of 2')).not.toBeInTheDocument();

    rerender(
      <TechnicalInterviewProgress
        question={{
          questionType: 'FOLLOW_UP',
          mainQuestionIndex: 3,
          totalMainQuestions: 3,
          subQuestionIndex: 2,
          requiredSubQuestionCount: 2,
          completedSubQuestionCount: 1,
        }}
        t={t}
      />,
    );
    expect(screen.getByText('Follow-up 2 of 2')).toBeInTheDocument();
  });

  test('keeps the submitted transcript focusable but read-only during processing', () => {
    render(
      <TechnicalTranscriptEditor
        value="Preserved transcript"
        onChange={jest.fn()}
        disabled
        editable
        t={t}
      />,
    );

    const transcript = screen.getByRole('textbox', { name: 'Answer transcript' });
    expect(transcript).toHaveAttribute('readonly');
    expect(transcript).not.toBeDisabled();
    expect(screen.getByText('Read-only transcript')).toBeInTheDocument();
  });

  test('renders real interviewer/candidate items and supports keyboard closing', () => {
    const onClose = jest.fn();
    render(
      <TechnicalTranscriptPanel
        items={[
          {
            id: 'attempt-1:question',
            attemptId: 'attempt-1',
            role: 'INTERVIEWER',
            content: 'Explain event delegation.',
            status: 'FINAL',
          },
          {
            id: 'attempt-1:answer',
            attemptId: 'attempt-1',
            role: 'CANDIDATE',
            content: 'It uses bubbling.',
            status: 'DRAFT',
          },
        ]}
        recorder={{
          recordingStatus: 'IDLE',
          sttStatus: 'IDLE',
          permissionError: null,
          sttError: null,
          setTranscript: jest.fn(),
        }}
        currentTranscript="It uses bubbling."
        hasActiveAttempt
        transcriptEditable
        disabled={false}
        isOpen
        onClose={onClose}
        t={t}
      />,
    );

    expect(screen.getByRole('log')).toHaveTextContent('Explain event delegation.');
    expect(screen.getByRole('log')).toHaveTextContent('It uses bubbling.');
    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Close transcript' }));

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  test('renders every dimension returned by the backend and respects each maxScore', () => {
    const dimensions = [
      { rubricCode: 'A', name: 'Accuracy', score: 9, maxScore: 10, weight: 0.3 },
      { rubricCode: 'D', name: 'Technical Depth', score: 7, maxScore: 10, weight: 0.25 },
      { rubricCode: 'R', name: 'Reasoning', score: 8, maxScore: 10, weight: 0.2 },
      { rubricCode: 'P', name: 'Application', score: 6, maxScore: 10, weight: 0.15 },
      { rubricCode: 'C', name: 'Communication', score: 7, maxScore: 10, weight: 0.1 },
    ];
    render(<TechnicalRubricBreakdown dimensions={dimensions} t={t} />);

    expect(screen.getByText('Accuracy')).toBeInTheDocument();
    expect(screen.getByText('Technical Depth')).toBeInTheDocument();
    expect(screen.getByText('Reasoning')).toBeInTheDocument();
    expect(screen.getByText('Application')).toBeInTheDocument();
    expect(screen.getByText('Communication')).toBeInTheDocument();
    expect(screen.getAllByRole('progressbar')).toHaveLength(5);
    expect(screen.getByRole('progressbar', { name: 'Technical Depth: 7/10' })).toHaveAttribute('aria-valuemax', '10');
  });

  test('renders exactly three main results with nested clarification and follow-ups', () => {
    const questions = [1, 2, 3].map((mainQuestionIndex) => ({
      attemptId: `main-${mainQuestionIndex}`,
      questionType: 'MAIN',
      mainQuestionIndex,
      content: `Main content ${mainQuestionIndex}`,
      answerTranscript: `Main answer ${mainQuestionIndex}`,
      initialMainScore: 5 + mainQuestionIndex,
      finalMainScore: 6 + mainQuestionIndex,
      cumulativeFollowUpBonus: mainQuestionIndex === 1 ? 1.5 : 0,
      maxScore: 10,
      sourceType: 'CV_INTERNAL_DO_NOT_RENDER',
      subQuestionResults: mainQuestionIndex === 1 ? [
        {
          attemptId: 'clarification-1',
          questionType: 'CLARIFICATION',
          content: 'Clarify the example',
          answerTranscript: 'Clarified answer',
          rawScore: 6,
          maxScore: 10,
          generationReason: 'ADAPTIVE_SCORE_RULE',
        },
        {
          attemptId: 'follow-up-1',
          questionType: 'FOLLOW_UP',
          content: 'First follow-up',
          answerTranscript: 'First follow-up answer',
          rawScore: 7,
          followUpBonus: 0.75,
          maxScore: 10,
        },
        {
          attemptId: 'follow-up-2',
          questionType: 'FOLLOW_UP',
          content: 'Second follow-up',
          answerTranscript: 'Second follow-up answer',
          rawScore: 8,
          followUpBonus: 0.75,
          maxScore: 10,
          generationReason: 'RELIABILITY_MINIMUM',
        },
      ] : [],
    }));

    render(<TechnicalQuestionBreakdown questions={questions} t={t} />);

    expect(screen.getAllByText(/Main question [123]/)).toHaveLength(3);
    expect(screen.getAllByText('Initial main score')).toHaveLength(3);
    expect(screen.getAllByText('Final main score')).toHaveLength(3);
    expect(screen.getByText('Follow-up 1')).toBeInTheDocument();
    expect(screen.getByText('Follow-up 2')).toBeInTheDocument();
    expect(screen.getByText('Clarification result')).toBeInTheDocument();
    expect(screen.queryByText('CV_INTERNAL_DO_NOT_RENDER')).not.toBeInTheDocument();
    expect(screen.queryByText('ADAPTIVE_SCORE_RULE')).not.toBeInTheDocument();
    expect(screen.queryByText('RELIABILITY_MINIMUM')).not.toBeInTheDocument();
  });
});

