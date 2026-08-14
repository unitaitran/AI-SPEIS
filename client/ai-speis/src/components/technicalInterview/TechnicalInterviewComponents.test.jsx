import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import TechnicalQuestionPanel from './TechnicalQuestionPanel';
import TechnicalInterviewProgress from './TechnicalInterviewProgress';
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

});

