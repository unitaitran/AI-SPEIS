import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import ActiveSessionDialog from './ActiveSessionDialog';
import EndSessionConfirmDialog from './EndSessionConfirmDialog';

const labels = {
  'activeSession.eyebrow': 'Unfinished interview',
  'activeSession.title': 'You already have an unfinished interview',
  'activeSession.description': 'Choose a recovery action.',
  'activeSession.campaign': 'Campaign',
  'activeSession.interviewType': 'Interview type',
  'activeSession.status': 'Current status',
  'activeSession.startedAt': 'Started',
  'activeSession.completedQuestions': 'Questions completed',
  'activeSession.updatedAt': 'Last updated',
  'activeSession.notAvailable': 'Not available',
  'activeSession.resume': 'Continue current interview',
  'activeSession.endSession': 'End current round',
  'activeSession.closeCampaign': 'Close campaign',
  'activeSession.back': 'Go back',
  'activeSession.endConfirmTitle': 'End the current interview round?',
  'activeSession.endConfirmDescription': 'The active round will be marked completed.',
  'activeSession.answerWarning': 'Existing answers may not count.',
  'activeSession.keepSession': 'Keep current interview',
  'activeSession.confirmEnd': 'End round',
  'common.unknown': 'Unknown',
};
const t = (key, options = {}) => labels[key] || options.defaultValue || key;

const conflict = {
  campaignId: 8,
  sessionId: 17,
  canResume: true,
  canEnd: true,
  canCloseCampaign: true,
  campaign: {
    interviewCampaignId: 8,
    status: 'Active',
    startedAt: '2026-07-20T10:00:00Z',
    sessions: [{
      interviewSessionId: 17,
      interviewRoundType: 'Technical',
      status: 'Active',
      completedQuestionCount: 2,
      updatedAt: '2026-07-20T10:10:00Z',
    }],
  },
};

describe('active interview dialogs', () => {
  test('renders backend state and makes resume the primary available action', () => {
    const onResume = jest.fn();
    render(
      <ActiveSessionDialog
        conflict={conflict}
        language="en"
        onResume={onResume}
        onEndSession={jest.fn()}
        onCloseCampaign={jest.fn()}
        onBack={jest.fn()}
        t={t}
      />,
    );

    expect(screen.getByRole('dialog', { name: 'You already have an unfinished interview' })).toBeInTheDocument();
    expect(screen.getByText('#8')).toBeInTheDocument();
    expect(screen.getByText('Technical')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Continue current interview' }));
    expect(onResume).toHaveBeenCalledTimes(1);
  });

  test('disables destructive confirmation while the request is running', () => {
    render(
      <EndSessionConfirmDialog
        action="session"
        isSubmitting
        onConfirm={jest.fn()}
        onCancel={jest.fn()}
        t={t}
      />,
    );

    expect(screen.getByRole('alertdialog', { name: 'End the current interview round?' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'End round' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Keep current interview' })).toBeDisabled();
  });
});
