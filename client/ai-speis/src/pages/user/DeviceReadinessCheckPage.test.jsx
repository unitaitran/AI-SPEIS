import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import DeviceReadinessCheckPage from './DeviceReadinessCheckPage';
import interviewSessionService from '../../services/InterviewSessionService';
import { navigate } from '../../routes/navigation';
import {
  clearActiveInterviewContext,
  getActiveInterviewContext,
  notifyInterviewQuotaChanged,
} from '../../utils/interviewContext';

const tMock = (key, options = {}) => options.defaultValue || key;

jest.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: tMock,
  }),
}));

jest.mock('../../layouts/user/UserLayout', () => ({ children }) => <>{children}</>);
jest.mock('../../components/user/InterviewProgressStepper/InterviewProgressStepper', () => () => null);

jest.mock('../../services/InterviewSessionService', () => ({
  __esModule: true,
  default: {
    getCampaign: jest.fn(),
    cancelCampaign: jest.fn(),
    startSession: jest.fn(),
  },
}));

jest.mock('../../services/AudioService', () => ({
  __esModule: true,
  default: {
    checkSpeechToText: jest.fn(),
  },
}));

jest.mock('../../services/behavioralInterviewApi', () => ({
  __esModule: true,
  default: {
    start: jest.fn(),
  },
}));

jest.mock('../../routes/navigation', () => ({ navigate: jest.fn() }));

jest.mock('../../utils/interviewContext', () => ({
  clearActiveInterviewContext: jest.fn(),
  getActiveInterviewContext: jest.fn(),
  getInterviewSetupDraft: jest.fn(),
  getNextPendingSession: jest.fn(),
  notifyInterviewQuotaChanged: jest.fn(),
  saveActiveInterviewContext: jest.fn(),
}));

const mockCampaign = {
  interviewCampaignId: 10,
  language: 'vi',
  status: 'Pending',
  sessions: [
    { interviewSessionId: 20, interviewRoundType: 'Behavior', status: 'Pending' },
  ],
};

describe('DeviceReadinessCheckPage', () => {
  beforeAll(() => {
    Object.defineProperty(global.navigator, 'mediaDevices', {
      value: {
        getUserMedia: jest.fn().mockRejectedValue(new Error('No media device')),
      },
      writable: true,
    });
  });

  beforeEach(() => {
    jest.clearAllMocks();
    getActiveInterviewContext.mockReturnValue({
      campaign: mockCampaign,
      activeSessionId: null,
    });
    interviewSessionService.getCampaign.mockResolvedValue(mockCampaign);
  });

  test('cancels campaign, notifies quota change, clears context and navigates when Back button is clicked', async () => {
    const cancelledCampaignResponse = {
      ...mockCampaign,
      status: 'Cancelled',
      remainingInterviewQuota: 5,
      maxInterviewQuota: 10,
      planName: 'Free',
    };
    interviewSessionService.cancelCampaign.mockResolvedValue(cancelledCampaignResponse);

    render(<DeviceReadinessCheckPage />);

    const backButton = await screen.findByRole('button', { name: /common.back/i });

    await act(async () => {
      fireEvent.click(backButton);
    });

    await waitFor(() => {
      expect(interviewSessionService.cancelCampaign).toHaveBeenCalledWith(10);
    });

    expect(notifyInterviewQuotaChanged).toHaveBeenCalledWith(cancelledCampaignResponse);
    expect(clearActiveInterviewContext).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/user/interview/setup');
  });
});
