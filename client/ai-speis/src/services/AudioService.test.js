import audioService from './AudioService';

describe('AudioService text-to-speech', () => {
  beforeEach(() => {
    localStorage.setItem('token', 'test-token');
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.restoreAllMocks();
    localStorage.clear();
  });

  test('requests an authenticated audio blob with Technical question metadata', async () => {
    const audioBlob = new Blob(['audio'], { type: 'audio/mpeg' });
    global.fetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: { get: () => 'audio/mpeg' },
      blob: async () => audioBlob,
    });
    const controller = new AbortController();

    await expect(audioService.synthesizeSpeech({
      text: 'Explain dependency inversion.',
      languageCode: 'en-US',
      sessionId: 17,
      questionId: 9,
      attemptId: '11111111-1111-1111-1111-111111111111',
    }, { signal: controller.signal })).resolves.toBe(audioBlob);

    const [url, options] = global.fetch.mock.calls[0];
    expect(url).toContain('/api/Audio/text-to-speech');
    expect(options.headers.Authorization).toBe('Bearer test-token');
    expect(options.signal).toBe(controller.signal);
    expect(JSON.parse(options.body)).toMatchObject({
      sessionId: 17,
      questionId: 9,
      languageCode: 'en-US',
    });
  });

  test('keeps the backend TTS error code for recoverable UI handling', async () => {
    global.fetch.mockResolvedValue({
      ok: false,
      status: 504,
      headers: { get: () => 'application/json' },
      json: async () => ({ code: 'TTS_GENERATION_TIMEOUT', message: 'Timed out.' }),
    });

    await expect(audioService.synthesizeSpeech({ text: 'Question', sessionId: 17 }))
      .rejects.toMatchObject({
        code: 'TTS_GENERATION_TIMEOUT',
        status: 504,
      });
  });

  test('omits QuestionId for generated clarification or follow-up audio', async () => {
    global.fetch.mockResolvedValue({
      ok: true,
      status: 200,
      headers: { get: () => 'audio/mpeg' },
      blob: async () => new Blob(['audio'], { type: 'audio/mpeg' }),
    });

    await audioService.synthesizeSpeech({
      text: 'Could you explain that trade-off in more detail?',
      languageCode: 'en-US',
      sessionId: 17,
      questionId: null,
      attemptId: '22222222-2222-2222-2222-222222222222',
    });

    const payload = JSON.parse(global.fetch.mock.calls[0][1].body);
    expect(payload).toMatchObject({
      sessionId: 17,
      attemptId: '22222222-2222-2222-2222-222222222222',
    });
    expect(payload).not.toHaveProperty('questionId');
  });
});
