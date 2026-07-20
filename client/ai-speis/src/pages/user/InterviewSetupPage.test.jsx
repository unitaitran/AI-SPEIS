import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import InterviewSetupPage from './InterviewSetupPage';
import cvService from '../../services/CVService';
import jdService from '../../services/JDService';
import interviewSessionService from '../../services/InterviewSessionService';
import { navigate } from '../../routes/navigation';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  saveActiveInterviewContext,
  saveInterviewSetupDraft,
} from '../../utils/interviewContext';

jest.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key, options = {}) => options.defaultValue || key,
  }),
}));
jest.mock('../../layouts/user/UserLayout', () => ({ children }) => <>{children}</>);
jest.mock('../../components/user/InterviewProgressStepper/InterviewProgressStepper', () => () => null);
jest.mock('../../services/CVService', () => ({
  __esModule: true,
  default: { getMyCVHistory: jest.fn() },
}));
jest.mock('../../services/JDService', () => ({
  __esModule: true,
  default: { getMyJDHistory: jest.fn(), getParsedData: jest.fn() },
}));
jest.mock('../../services/InterviewSessionService', () => ({
  __esModule: true,
  default: {
    getAvailableTypes: jest.fn(),
    getActiveCampaign: jest.fn(),
    cancelCampaign: jest.fn(),
    completeSession: jest.fn(),
    createSession: jest.fn(),
  },
}));
jest.mock('../../routes/navigation', () => ({ navigate: jest.fn() }));
jest.mock('../../utils/interviewContext', () => ({
  clearActiveInterviewContext: jest.fn(),
  getActiveInterviewContext: jest.fn(),
  getInterviewSetupDraft: jest.fn(),
  notifyInterviewQuotaChanged: jest.fn(),
  saveActiveInterviewContext: jest.fn(),
  saveInterviewSetupDraft: jest.fn(),
}));

const activeCampaign = {
  interviewCampaignId: 8,
  language: 'en',
  mode: 'Practice',
  status: 'Active',
  startedAt: '2026-07-20T10:00:00Z',
  sessions: [{
    interviewSessionId: 17,
    interviewRoundType: 'Technical',
    status: 'Active',
    questionCount: 3,
    completedQuestionCount: 1,
    createdAt: '2026-07-20T10:00:00Z',
  }],
};

describe('InterviewSetupPage active campaign recovery', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    getInterviewSetupDraft.mockReturnValue({
      mode: 'Practice',
      language: 'en',
      practiceRounds: ['Technical'],
      practiceQuestionCounts: { Technical: 3 },
    });
    getActiveInterviewContext.mockReturnValue(null);
    cvService.getMyCVHistory.mockResolvedValue({
      items: [{ cvFileId: 3, fileName: 'cv.pdf', status: 'Confirmed' }],
    });
    jdService.getMyJDHistory.mockResolvedValue({
      items: [{ jdFileId: 4, fileName: 'jd.pdf', status: 'Confirmed' }],
    });
    jdService.getParsedData.mockResolvedValue({
      jobTitle: 'Senior Engineer',
      experienceLevel: 'Senior',
    });
    interviewSessionService.getAvailableTypes.mockResolvedValue({
      availableRounds: ['Technical'],
      hasOptionalCoding: false,
      difficulty: 'Hard',
    });
  });

  test('shows conflict actions and does not create a duplicate campaign', async () => {
    interviewSessionService.getActiveCampaign.mockResolvedValue(activeCampaign);
    render(<InterviewSetupPage />);

    const continueButton = await screen.findByRole('button', { name: 'common.continue' });
    await waitFor(() => expect(continueButton).toBeEnabled());
    fireEvent.click(continueButton);

    expect(await screen.findByRole('dialog', { name: 'activeSession.title' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'activeSession.resume' })).toBeInTheDocument();
    expect(interviewSessionService.createSession).not.toHaveBeenCalled();
  });

  test('closes the old campaign and creates the selected setup without losing it', async () => {
    const createdCampaign = {
      interviewCampaignId: 9,
      language: 'en',
      mode: 'Practice',
      status: 'Pending',
      sessions: [{ interviewSessionId: 18, interviewRoundType: 'Technical', status: 'Pending' }],
    };
    interviewSessionService.getActiveCampaign
      .mockResolvedValueOnce(activeCampaign)
      .mockResolvedValueOnce(activeCampaign)
      .mockResolvedValueOnce(null);
    interviewSessionService.cancelCampaign.mockResolvedValue({ ...activeCampaign, status: 'Cancelled' });
    interviewSessionService.createSession.mockResolvedValue(createdCampaign);
    render(<InterviewSetupPage />);

    const continueButton = await screen.findByRole('button', { name: 'common.continue' });
    await waitFor(() => expect(continueButton).toBeEnabled());
    fireEvent.click(continueButton);
    fireEvent.click(await screen.findByRole('button', { name: 'activeSession.closeCampaign' }));
    fireEvent.click(await screen.findByRole('button', { name: 'activeSession.confirmCloseCampaign' }));

    await waitFor(() => expect(interviewSessionService.createSession).toHaveBeenCalledTimes(1));
    expect(interviewSessionService.createSession).toHaveBeenCalledWith(expect.objectContaining({
      CVFileId: 3,
      JDFileId: 4,
      Language: 'en',
      Mode: 'Practice',
      SelectedRounds: ['Technical'],
      QuestionCounts: { Technical: 3 },
    }));
    expect(saveActiveInterviewContext).toHaveBeenCalledWith(expect.objectContaining({
      campaign: createdCampaign,
      activeSessionId: null,
    }));
    expect(saveInterviewSetupDraft).toHaveBeenCalledWith(expect.objectContaining({
      campaignId: 9,
      practiceRounds: ['Technical'],
      practiceQuestionCounts: { Technical: 3 },
    }));
    expect(navigate).toHaveBeenCalledWith('/user/interview/device-check');
  });

  test('does not send duplicate create requests when Continue is clicked repeatedly', async () => {
    let resolveCreate;
    interviewSessionService.getActiveCampaign.mockResolvedValue(null);
    interviewSessionService.createSession.mockImplementation(() => new Promise((resolve) => {
      resolveCreate = resolve;
    }));
    render(<InterviewSetupPage />);

    const continueButton = await screen.findByRole('button', { name: 'common.continue' });
    await waitFor(() => expect(continueButton).toBeEnabled());
    fireEvent.click(continueButton);
    fireEvent.click(continueButton);

    await waitFor(() => expect(interviewSessionService.createSession).toHaveBeenCalledTimes(1));
    resolveCreate({
      interviewCampaignId: 9,
      status: 'Pending',
      sessions: [],
    });
    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/user/interview/device-check'));
  });
});
