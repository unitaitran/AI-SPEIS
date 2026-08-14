import interviewSessionService, { InterviewSessionError } from './InterviewSessionService';

describe('InterviewSessionService', () => {
  beforeEach(() => {
    localStorage.setItem('token', 'test-token');
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.restoreAllMocks();
    localStorage.clear();
  });

  test('returns null when the backend confirms there is no active campaign', async () => {
    global.fetch.mockResolvedValue({
      ok: true,
      status: 204,
      headers: { get: () => '' },
    });

    await expect(interviewSessionService.getActiveCampaign()).resolves.toBeNull();
    expect(global.fetch.mock.calls[0][0]).toContain('/api/InterviewSession/active');
  });

  test('exposes conflict code and backend data without parsing the message string', async () => {
    const data = {
      campaignId: 8,
      sessionId: 17,
      canResume: true,
      campaign: { interviewCampaignId: 8, status: 'Active' },
    };
    global.fetch.mockResolvedValue({
      ok: false,
      status: 409,
      headers: { get: () => 'application/json' },
      json: async () => ({
        code: 'ACTIVE_INTERVIEW_SESSION_EXISTS',
        message: 'This wording may change.',
        data,
      }),
    });

    await expect(interviewSessionService.createSession({ Mode: 'Practice' }))
      .rejects.toEqual(expect.objectContaining({
        code: 'ACTIVE_INTERVIEW_SESSION_EXISTS',
        status: 409,
        data,
      }));
    await expect(interviewSessionService.createSession({ Mode: 'Practice' }))
      .rejects.toBeInstanceOf(InterviewSessionError);
  });
});
