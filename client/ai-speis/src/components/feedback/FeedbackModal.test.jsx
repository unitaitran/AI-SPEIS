import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import FeedbackModal, { FEEDBACK_REASONS } from './FeedbackModal';

const labels = {
  'feedback.title': 'Report AI evaluation',
  'feedback.subtitle': 'Send this evaluation to an admin for review.',
  'feedback.selectedEvaluation': 'Selected evaluation',
  'feedback.roundSelectorLabel': 'Interview round',
  'feedback.roundSelectorPlaceholder': 'Select a round',
  'feedback.reasonLabel': 'Reason',
  'feedback.reasonPlaceholder': 'Select a reason',
  'feedback.reasonHelper': 'This reason is the feedback title.',
  'feedback.explanationLabel': 'Explanation',
  'feedback.explanationPlaceholder': 'Describe the issue',
  'feedback.cancel': 'Cancel',
  'feedback.submit': 'Submit feedback',
  'feedback.submitting': 'Submitting',
  'feedback.validation.roundRequired': 'Select a round',
  'feedback.validation.reasonRequired': 'Select a reason',
  'feedback.validation.explanationLength': 'Enter at least 10 characters',
};

const t = (key) => labels[key] || key;

describe('FeedbackModal', () => {
  test('uses one reason dropdown as the title and explanation as detail', async () => {
    const onSubmit = jest.fn().mockResolvedValue(undefined);
    render(
      <FeedbackModal
        isOpen
        onClose={jest.fn()}
        onSubmit={onSubmit}
        isSubmitting={false}
        interviewSessionId={17}
        evaluationType="Technical"
        evaluationLabel="Technical"
        t={t}
      />,
    );

    const reasonSelect = screen.getByRole('combobox', { name: 'Reason' });
    expect(screen.getAllByRole('combobox')).toHaveLength(1);
    expect(reasonSelect.options).toHaveLength(FEEDBACK_REASONS.length + 1);

    fireEvent.change(reasonSelect, { target: { value: 'INCORRECT_SCORE' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Explanation' }), {
      target: { value: 'The score conflicts with the rubric evidence.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Submit feedback' }));

    expect(onSubmit).toHaveBeenCalledWith({
      interviewSessionId: 17,
      evaluationType: 'Technical',
      reason: 'INCORRECT_SCORE',
      explanation: 'The score conflicts with the rubric evidence.',
    });
  });

  test('does not reset entered feedback when the parent callback changes', () => {
    const props = {
      isOpen: true,
      onSubmit: jest.fn(),
      isSubmitting: false,
      interviewSessionId: 17,
      t,
    };
    const { rerender } = render(<FeedbackModal {...props} onClose={() => {}} />);
    fireEvent.change(screen.getByRole('combobox', { name: 'Reason' }), { target: { value: 'MISSING_CONTEXT' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Explanation' }), { target: { value: 'Important context is missing.' } });

    rerender(<FeedbackModal {...props} onClose={() => {}} />);

    expect(screen.getByRole('combobox', { name: 'Reason' })).toHaveValue('MISSING_CONTEXT');
    expect(screen.getByRole('textbox', { name: 'Explanation' })).toHaveValue('Important context is missing.');
  });

  test('selects a completed round before submitting real test feedback', async () => {
    const onSubmit = jest.fn().mockResolvedValue(undefined);
    render(
      <FeedbackModal
        isOpen
        onClose={jest.fn()}
        onSubmit={onSubmit}
        isSubmitting={false}
        roundOptions={[
          { interviewSessionId: 17, evaluationType: 'Technical', label: 'Technical' },
          { interviewSessionId: 18, evaluationType: 'Behavioral', label: 'Behavioral' },
        ]}
        t={t}
      />,
    );

    expect(screen.getAllByRole('combobox')).toHaveLength(2);
    fireEvent.change(screen.getByRole('combobox', { name: 'Interview round' }), { target: { value: '18' } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Reason' }), { target: { value: 'INACCURATE_FEEDBACK' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Explanation' }), {
      target: { value: 'The behavioral summary misses important evidence.' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Submit feedback' }));

    expect(onSubmit).toHaveBeenCalledWith({
      interviewSessionId: 18,
      evaluationType: 'Behavioral',
      reason: 'INACCURATE_FEEDBACK',
      explanation: 'The behavioral summary misses important evidence.',
    });
  });
});
