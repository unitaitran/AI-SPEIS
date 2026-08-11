import technicalV2InterviewApi from './technicalV2InterviewApi';

const jsonResponse = (body, status = 200) => ({
  ok: status >= 200 && status < 300,
  status,
  headers: { get: () => 'application/json' },
  json: async () => body,
});

describe('technicalV2InterviewApi', () => {
  beforeEach(() => {
    localStorage.setItem('token', 'test-token');
    global.fetch = jest.fn().mockResolvedValue(jsonResponse({ ok: true }));
  });

  afterEach(() => {
    localStorage.clear();
    jest.restoreAllMocks();
  });

  test('uses the V2 lifecycle routes and auth header', async () => {
    await technicalV2InterviewApi.initialize(17, ['React']);
    expect(fetch).toHaveBeenLastCalledWith(
      expect.stringContaining('/api/interviews/17/technical/initialize'),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ Authorization: 'Bearer test-token', Accept: 'application/json' }),
        body: JSON.stringify({ requiredSkills: ['React'] }),
      }),
    );

    await technicalV2InterviewApi.start(17);
    expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/api/interviews/17/technical/start'), expect.objectContaining({ method: 'POST' }));
    await technicalV2InterviewApi.getState(17);
    expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/api/interviews/17/technical/state'), expect.any(Object));
    await technicalV2InterviewApi.getCurrentQuestion(17);
    expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/api/interviews/17/technical/current-question'), expect.any(Object));
    await technicalV2InterviewApi.complete(17);
    expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/api/interviews/17/technical/complete'), expect.objectContaining({ method: 'POST' }));
    await technicalV2InterviewApi.generateFeedback(17);
    expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/api/interviews/17/technical/feedback'), expect.objectContaining({ method: 'POST' }));
    await technicalV2InterviewApi.getResult(17);
    expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/api/interviews/17/technical/result'), expect.any(Object));
  });

  test('sends a stable idempotency header on answer submission', async () => {
    await technicalV2InterviewApi.submitAnswer(17, 101, { transcript: 'answer' }, { idempotencyKey: 'technical-v2-key' });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/interviews/17/technical/questions/101/answers'),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ 'Idempotency-Key': 'technical-v2-key' }),
        body: JSON.stringify({ transcript: 'answer' }),
      }),
    );
  });

  test('preserves backend ProblemDetails error codes', async () => {
    global.fetch.mockResolvedValueOnce(jsonResponse({ title: 'LEGACY_SESSION', detail: 'Legacy session' }, 409));
    await expect(technicalV2InterviewApi.getResult(17)).rejects.toMatchObject({ code: 'LEGACY_SESSION', status: 409 });
  });
});

