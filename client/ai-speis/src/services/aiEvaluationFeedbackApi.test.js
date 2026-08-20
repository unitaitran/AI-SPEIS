import {
  getAdminFeedback,
  getAdminFeedbackDetail,
  submitEvaluationFeedback,
} from './aiEvaluationFeedbackApi';

const jsonResponse = (body, status = 200) => ({
  ok: status >= 200 && status < 300,
  status,
  headers: { get: () => 'application/json' },
  json: async () => body,
});

describe('aiEvaluationFeedbackApi', () => {
  beforeEach(() => {
    localStorage.setItem('token', 'test-token');
    global.fetch = jest.fn().mockResolvedValue(jsonResponse({ feedbackId: 9 }));
  });

  afterEach(() => {
    localStorage.clear();
    jest.restoreAllMocks();
  });

  test('submits reason as the feedback title and explanation as detail', async () => {
    const payload = {
      interviewSessionId: 17,
      evaluationType: 'Behavioral',
      reason: 'INACCURATE_FEEDBACK',
      explanation: 'The feedback does not match the recorded answer.',
    };

    await submitEvaluationFeedback(payload);

    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/ai-feedback'),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ Authorization: 'Bearer test-token', 'Content-Type': 'application/json' }),
        body: JSON.stringify(payload),
      }),
    );
  });

  test('supports read-only admin list and detail requests', async () => {
    await getAdminFeedback({ search: 'score', pageNumber: 2, pageSize: 20 });
    expect(fetch.mock.calls[0][0]).toContain('/api/admin/ai-feedback?');
    expect(fetch.mock.calls[0][0]).toContain('search=score');
    expect(fetch.mock.calls[0][0]).toContain('pageNumber=2');

    await getAdminFeedbackDetail(9);
    expect(fetch).toHaveBeenLastCalledWith(
      expect.stringContaining('/api/admin/ai-feedback/9'),
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
      }),
    );
  });
});
