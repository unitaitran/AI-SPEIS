import technicalInterviewApi from './technicalInterviewApi';

describe('technicalInterviewApi', () => {
  beforeEach(() => {
    localStorage.setItem('token', 'test-token');
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.restoreAllMocks();
    localStorage.clear();
  });

  test('submits by attemptId with an idempotency key', async () => {
    global.fetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => ({ sessionStatus: 'EVALUATING' }),
    });

    await technicalInterviewApi.submitAnswer(
      'session-7',
      { attemptId: 'attempt-9', transcript: 'My answer' },
      { idempotencyKey: 'stable-key' },
    );

    expect(global.fetch).toHaveBeenCalledTimes(1);
    const [url, options] = global.fetch.mock.calls[0];
    expect(url).toContain('/api/technical-interviews/session-7/answers');
    expect(options.headers['Idempotency-Key']).toBe('stable-key');
    expect(JSON.parse(options.body)).toEqual({ attemptId: 'attempt-9', transcript: 'My answer' });
  });

  test('maps backend error codes without exposing the raw response to UI code', async () => {
    global.fetch.mockResolvedValue({
      ok: false,
      status: 409,
      headers: { get: () => 'application/json' },
      json: async () => ({ code: 'ANSWER_ALREADY_SUBMITTED', message: 'duplicate' }),
    });

    await expect(technicalInterviewApi.submitAnswer(
      'session-7',
      { attemptId: 'attempt-9', transcript: 'My answer' },
    )).rejects.toMatchObject({ code: 'ANSWER_ALREADY_SUBMITTED', status: 409 });
  });
});

