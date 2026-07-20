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

  test('initializes and loads the session from the Technical Interview contract', async () => {
    global.fetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      headers: { get: () => 'application/json' },
      json: async () => ({ sessionId: 17, status: 'CREATED' }),
    }).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => ({
        sessionId: 17,
        status: 'CREATED',
      }),
    });

    await expect(technicalInterviewApi.initializeSession(17)).resolves.toMatchObject({
      sessionId: 17,
      status: 'CREATED',
    });
    await expect(technicalInterviewApi.getSession(17)).resolves.toMatchObject({
      sessionId: 17,
      status: 'CREATED',
    });
    expect(global.fetch).toHaveBeenCalledTimes(2);
    expect(global.fetch.mock.calls[0][0]).toContain('/api/technical-interviews/sessions');
    expect(JSON.parse(global.fetch.mock.calls[0][1].body)).toEqual({ interviewSessionId: 17 });
    expect(global.fetch.mock.calls[1][0]).toContain('/api/technical-interviews/17');
  });

  test('starts and completes through Technical Interview endpoints', async () => {
    global.fetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => ({ sessionStatus: 'QUESTION_READY' }),
    });

    await technicalInterviewApi.startSession(17);
    await technicalInterviewApi.completeSession(17);

    expect(global.fetch.mock.calls[0][0]).toContain('/api/technical-interviews/17/start');
    expect(global.fetch.mock.calls[0][1].method).toBe('POST');
    expect(global.fetch.mock.calls[1][0]).toContain('/api/technical-interviews/17/complete');
    expect(global.fetch.mock.calls[1][1].method).toBe('POST');
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

  test('reads the error code from ASP.NET ProblemDetails title', async () => {
    global.fetch.mockResolvedValue({
      ok: false,
      status: 400,
      headers: { get: () => 'application/problem+json' },
      json: async () => ({
        title: 'TECHNICAL_SESSION_NOT_INITIALIZED',
        detail: 'Initialize the Technical session first.',
      }),
    });

    await expect(technicalInterviewApi.startSession(17)).rejects.toMatchObject({
      code: 'TECHNICAL_SESSION_NOT_INITIALIZED',
      message: 'Initialize the Technical session first.',
      status: 400,
    });
  });

  test('runs one complete Technical Interview API flow with backend-shaped responses', async () => {
    const firstQuestion = {
      attemptId: '11111111-1111-1111-1111-111111111111',
      questionType: 'MAIN',
      content: 'Explain dependency inversion.',
      mainQuestionIndex: 1,
      totalMainQuestions: 1,
      sessionStatus: 'QUESTION_READY',
    };
    const followUp = {
      attemptId: '22222222-2222-2222-2222-222222222222',
      questionType: 'FOLLOW_UP',
      content: 'Give a concrete example.',
      mainQuestionIndex: 1,
      totalMainQuestions: 1,
      sessionStatus: 'QUESTION_READY',
    };
    const result = {
      sessionId: 17,
      overallScore: 4.2,
      performanceBand: 'Strong',
      mainQuestions: [],
      skillScores: [],
      summary: { summary: 'Strong foundation.', recommendedNextSteps: [] },
    };
    const calls = [];
    const responses = [
      { sessionId: 17, status: 'CREATED' },
      { sessionId: 17, status: 'CREATED' },
      firstQuestion,
      { attemptId: firstQuestion.attemptId, evaluation: { decision: 'FOLLOW_UP' }, nextQuestion: followUp, sessionStatus: 'QUESTION_READY' },
      { attemptId: followUp.attemptId, evaluation: { decision: 'END_INTERVIEW' }, nextQuestion: null, sessionStatus: 'COMPLETED' },
      result,
    ];
    global.fetch.mockImplementation(async (url, options = {}) => {
      calls.push({ url, options });
      const body = responses.shift();
      return {
        ok: true,
        status: calls.length === 1 ? 201 : 200,
        headers: { get: () => 'application/json' },
        json: async () => body,
      };
    });

    await technicalInterviewApi.initializeSession(17);
    const session = await technicalInterviewApi.getSession(17);
    const current = await technicalInterviewApi.startSession(17);
    const firstResponse = await technicalInterviewApi.submitAnswer(17, {
      attemptId: current.attemptId,
      transcript: 'Interfaces invert source-code dependencies.',
    }, { idempotencyKey: 'flow-main-answer' });
    const finalResponse = await technicalInterviewApi.submitAnswer(17, {
      attemptId: firstResponse.nextQuestion.attemptId,
      transcript: 'A policy owns an interface implemented by an adapter.',
    }, { idempotencyKey: 'flow-follow-up-answer' });
    const finalResult = await technicalInterviewApi.getResult(17);

    expect(session.status).toBe('CREATED');
    expect(firstResponse.nextQuestion).toEqual(followUp);
    expect(finalResponse.sessionStatus).toBe('COMPLETED');
    expect(finalResult).toEqual(result);
    expect(calls.map(({ url }) => url.replace(/^.*\/api/, '/api'))).toEqual([
      '/api/technical-interviews/sessions',
      '/api/technical-interviews/17',
      '/api/technical-interviews/17/start',
      '/api/technical-interviews/17/answers',
      '/api/technical-interviews/17/answers',
      '/api/technical-interviews/17/result',
    ]);
    expect(calls[3].options.headers['Idempotency-Key']).toBe('flow-main-answer');
    expect(calls[4].options.headers['Idempotency-Key']).toBe('flow-follow-up-answer');
  });
});
