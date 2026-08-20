import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import AiFeedbackReviewPage from './AiFeedbackReviewPage';
import { getAdminFeedback, getAdminFeedbackDetail } from '../../../services/aiEvaluationFeedbackApi';

jest.mock('react-i18next', () => ({
  useTranslation: () => ({ i18n: { language: 'en', resolvedLanguage: 'en' } }),
}));

jest.mock('../../../services/aiEvaluationFeedbackApi', () => ({
  getAdminFeedback: jest.fn(),
  getAdminFeedbackDetail: jest.fn(),
}));

jest.mock('../../../utils/notification', () => ({
  __esModule: true,
  default: { success: jest.fn(), error: jest.fn() },
}));

const feedback = {
  feedbackId: 9,
  userName: 'Test User',
  userEmail: 'user@example.com',
  interviewSessionId: 17,
  evaluationType: 'Technical',
  reason: 'INCORRECT_SCORE',
  explanation: 'The score conflicts with the rubric evidence.',
  aiExecutiveSummary: 'The candidate demonstrates sound dependency injection knowledge.',
  aiStrengths: ['Explains inversion of control clearly.'],
  aiGaps: ['Needs a stronger example of service lifetimes.'],
  createdAt: '2026-08-20T08:00:00Z',
};

describe('AiFeedbackReviewPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    getAdminFeedback.mockResolvedValue({ items: [feedback], totalItems: 1, totalPages: 1 });
    getAdminFeedbackDetail.mockResolvedValue(feedback);
  });

  test('only displays feedback list and round-level detail', async () => {
    render(<AiFeedbackReviewPage />);

    expect(await screen.findByText('Incorrect score')).toBeInTheDocument();
    expect(getAdminFeedback).toHaveBeenCalled();

    fireEvent.click(screen.getByText('Incorrect score'));
    expect(await screen.findByRole('dialog', { name: 'Feedback detail' })).toBeInTheDocument();
    expect(await screen.findByText('The candidate demonstrates sound dependency injection knowledge.')).toBeInTheDocument();
    expect(screen.getByText('Explains inversion of control clearly.')).toBeInTheDocument();
    expect(screen.getByText('Needs a stronger example of service lifetimes.')).toBeInTheDocument();
    expect(screen.queryByText('Dependency injection supplies dependencies externally.')).not.toBeInTheDocument();

    expect(screen.queryByText('Pending')).not.toBeInTheDocument();
    expect(screen.queryByText('Status')).not.toBeInTheDocument();
  });
});
