import { ENDPOINTS } from '../config/api';

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) throw new Error('Authentication token not found');
  return { Authorization: `Bearer ${token}` };
};

export class AudioServiceError extends Error {
  constructor(message, { code, status, details } = {}) {
    super(message);
    this.name = 'AudioServiceError';
    this.code = code || 'AUDIO_REQUEST_FAILED';
    this.status = status;
    this.details = details;
  }
}

const readAudioError = async (response) => {
  const contentType = response.headers.get('Content-Type') || '';
  const body = contentType.includes('json')
    ? await response.json().catch(() => ({}))
    : null;
  throw new AudioServiceError(
    body?.message || body?.Message || `Request failed with status ${response.status}`,
    {
      code: body?.code || body?.Code,
      status: response.status,
      details: body,
    },
  );
};

const audioService = {
  checkSpeechToText: async (audioBlob, languageCode = 'vi-VN', { signal, timeoutMs = 90000 } = {}) => {
    const formData = new FormData();
    formData.append('audioFile', audioBlob, 'record.webm');
    formData.append('languageCode', languageCode);

    const timeoutController = signal ? null : new AbortController();
    let timedOut = false;
    const timeoutId = timeoutController
      ? window.setTimeout(() => {
        timedOut = true;
        timeoutController.abort();
      }, timeoutMs)
      : null;
    let response;
    try {
      response = await fetch(ENDPOINTS.AUDIO_SPEECH_TO_TEXT, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: formData,
        signal: signal || timeoutController.signal,
      });
    } catch (error) {
      if (error?.name === 'AbortError' && timedOut) {
        throw new AudioServiceError('Speech-to-text timed out', { code: 'STT_TIMEOUT' });
      }
      throw error;
    } finally {
      window.clearTimeout(timeoutId);
    }

    if (!response.ok) {
      let errorMessage = `Request failed with status ${response.status}`;
      try {
        const errorData = await response.json();
        errorMessage = errorData.Message || errorData.message || errorMessage;
      } catch (e) {}
      throw new Error(errorMessage);
    }
    return response.json();
  },

  synthesizeSpeech: async ({
    text,
    languageCode = 'vi-VN',
    voiceName,
    speakingRate = 1,
    pitch = 0,
    sessionId,
    questionId,
    attemptId,
  }, { signal } = {}) => {
    const normalizedSessionId = Number(sessionId);
    const normalizedQuestionId = Number(questionId);
    const response = await fetch(ENDPOINTS.AUDIO_TEXT_TO_SPEECH, {
      method: 'POST',
      headers: {
        ...getAuthHeaders(),
        'Content-Type': 'application/json',
        Accept: 'audio/mpeg, application/json',
      },
      signal,
      body: JSON.stringify({
        text,
        languageCode,
        voiceName,
        speakingRate,
        pitch,
        sessionId: Number.isInteger(normalizedSessionId) && normalizedSessionId > 0
          ? normalizedSessionId
          : undefined,
        // Clarification and Follow-up questions are generated inside the session and
        // intentionally have no Question Bank id. Omit the nullable field instead of
        // coercing null to zero, which violates the backend Range validation.
        questionId: Number.isInteger(normalizedQuestionId) && normalizedQuestionId > 0
          ? normalizedQuestionId
          : undefined,
        attemptId: attemptId || undefined,
      }),
    });

    if (!response.ok) await readAudioError(response);
    return response.blob();
  },
};

export default audioService;
