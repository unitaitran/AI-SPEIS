import { act, renderHook, waitFor } from '@testing-library/react';
import audioService from '../../services/AudioService';
import useQuestionAudio, { QuestionAudioStatus } from './useQuestionAudio';

jest.mock('../../services/AudioService', () => ({
  __esModule: true,
  default: {
    synthesizeSpeech: jest.fn(),
  },
}));

const createAudio = () => ({
  currentTime: 0,
  play: jest.fn().mockResolvedValue(undefined),
  pause: jest.fn(),
  load: jest.fn(),
  removeAttribute: jest.fn(),
  onplay: null,
  onpause: null,
  onended: null,
  onerror: null,
});

describe('useQuestionAudio', () => {
  let audioInstances;

  beforeEach(() => {
    jest.clearAllMocks();
    localStorage.clear();
    audioInstances = [];
    global.URL.createObjectURL = jest.fn((blob) => `blob:question-${blob.size}-${audioInstances.length}`);
    global.URL.revokeObjectURL = jest.fn();
    global.Audio = jest.fn(() => {
      const audio = createAudio();
      audioInstances.push(audio);
      return audio;
    });
    audioService.synthesizeSpeech.mockResolvedValue(new Blob(['question-audio'], { type: 'audio/mpeg' }));
  });

  afterEach(() => {
    localStorage.clear();
  });

  test('generates and auto-plays audio for the current question without blocking its text', async () => {
    const question = {
      attemptId: 'attempt-1',
      questionId: 7,
      content: 'Explain the event loop.',
    };
    const { result } = renderHook(() => useQuestionAudio({ question, sessionId: 17, language: 'en' }));

    await waitFor(() => expect(result.current.status).toBe(QuestionAudioStatus.READY));
    expect(audioService.synthesizeSpeech).toHaveBeenCalledWith(expect.objectContaining({
      text: question.content,
      languageCode: 'en-US',
      sessionId: 17,
      questionId: 7,
      attemptId: 'attempt-1',
    }), expect.objectContaining({ signal: expect.any(AbortSignal) }));
    expect(audioInstances[0].play).toHaveBeenCalledTimes(1);
  });

  test('stops old audio, revokes its URL, and synthesizes only the new question', async () => {
    const firstQuestion = { attemptId: 'attempt-1', content: 'First question' };
    const secondQuestion = { attemptId: 'attempt-2', content: 'Second question' };
    const { result, rerender } = renderHook(
      ({ question }) => useQuestionAudio({ question, sessionId: 17, language: 'vi' }),
      { initialProps: { question: firstQuestion } },
    );
    await waitFor(() => expect(result.current.status).toBe(QuestionAudioStatus.READY));
    const firstAudio = audioInstances[0];

    rerender({ question: secondQuestion });
    await waitFor(() => expect(audioService.synthesizeSpeech).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(result.current.status).toBe(QuestionAudioStatus.READY));

    expect(firstAudio.pause).toHaveBeenCalled();
    expect(firstAudio.removeAttribute).toHaveBeenCalledWith('src');
    expect(global.URL.revokeObjectURL).toHaveBeenCalledTimes(1);
    expect(audioService.synthesizeSpeech.mock.calls[1][0].text).toBe('Second question');
  });

  test('keeps TTS failure recoverable and retries only when requested', async () => {
    audioService.synthesizeSpeech
      .mockRejectedValueOnce({ code: 'TTS_GENERATION_FAILED' })
      .mockResolvedValueOnce(new Blob(['retry-audio'], { type: 'audio/mpeg' }));
    const question = { attemptId: 'attempt-1', content: 'Retryable question' };
    const { result } = renderHook(() => useQuestionAudio({ question, sessionId: 17, language: 'vi' }));

    await waitFor(() => expect(result.current.status).toBe(QuestionAudioStatus.ERROR));
    expect(audioService.synthesizeSpeech).toHaveBeenCalledTimes(1);

    await act(async () => {
      await result.current.retry();
    });
    expect(result.current.status).toBe(QuestionAudioStatus.READY);
    expect(audioService.synthesizeSpeech).toHaveBeenCalledTimes(2);
  });

  test('aborts an unfinished TTS request and releases audio on unmount', async () => {
    let capturedSignal;
    audioService.synthesizeSpeech.mockImplementation((payload, { signal }) => {
      capturedSignal = signal;
      return new Promise(() => {});
    });
    const question = { attemptId: 'attempt-1', content: 'Pending question' };
    const { unmount } = renderHook(() => useQuestionAudio({ question, sessionId: 17, language: 'vi' }));

    await waitFor(() => expect(capturedSignal).toBeDefined());
    unmount();
    expect(capturedSignal.aborted).toBe(true);
  });
});
