import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import BehavioralRecorderControls from './BehavioralRecorderControls';

const t = (key) => ({
  answerReview: 'Answer review',
  answerReady: 'Answer ready',
  reviewBeforeSubmit: 'Review and edit before submitting',
  yourTranscript: 'Your transcript',
  transcriptPlaceholder: 'Edit your answer',
  transcriptHelper: 'Transcript can be edited',
  recordAgain: 'Record again',
  retryTranscription: 'Retry transcription',
  transcriptionFailed: 'Transcription failed',
  audioPreserved: 'Audio preserved',
  submitAnswer: 'Submit answer',
}[key] || key);

test('allows a restored speech transcript to be edited without an audio blob', () => {
  const setTranscript = jest.fn();
  const onSubmit = jest.fn();

  render(
    <BehavioralRecorderControls
      recorder={{
        audioBlob: null,
        elapsedSeconds: 12,
        permissionError: null,
        recordingStatus: 'IDLE',
        reset: jest.fn(),
        setTranscript,
        sttStatus: 'COMPLETED',
        transcript: 'Original speech transcript',
      }}
      disabled={false}
      isSubmitting={false}
      onSubmit={onSubmit}
      t={t}
    />,
  );

  const transcript = screen.getByRole('textbox', { name: 'Your transcript' });
  expect(transcript).toHaveValue('Original speech transcript');
  expect(transcript).not.toHaveAttribute('readonly');

  fireEvent.change(transcript, { target: { value: 'Edited transcript' } });
  expect(setTranscript).toHaveBeenCalledWith('Edited transcript');

  fireEvent.click(screen.getByRole('button', { name: 'Submit answer' }));
  expect(onSubmit).toHaveBeenCalledTimes(1);
});

test('shows a recoverable STT error instead of an endless submitting state when a preview exists', () => {
  render(
    <BehavioralRecorderControls
      recorder={{
        audioBlob: null,
        elapsedSeconds: 12,
        permissionError: null,
        recordingStatus: 'ERROR',
        reset: jest.fn(),
        setTranscript: jest.fn(),
        startRecording: jest.fn(),
        sttStatus: 'FAILED',
        transcript: 'Unverified browser preview',
      }}
      disabled={false}
      isSubmitting={false}
      onSubmit={jest.fn()}
      t={t}
    />,
  );

  expect(screen.getByRole('alert')).toHaveTextContent('Transcription failed');
  expect(screen.queryByText('submitting')).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Submit answer' })).not.toBeInTheDocument();
});
