import { ENDPOINTS } from '../config/api';
import { TechnicalV2ErrorCode } from '../features/technicalInterview/technicalV2Interview.types';

export class TechnicalV2InterviewError extends Error {
  constructor(message, { code, status, details } = {}) {
    super(message);
    this.name = 'TechnicalV2InterviewError';
    this.code = code || TechnicalV2ErrorCode.UNKNOWN_ERROR;
    this.status = status;
    this.details = details;
  }
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) {
    throw new TechnicalV2InterviewError('Authentication token not found', {
      code: TechnicalV2ErrorCode.SESSION_ACCESS_DENIED,
      status: 401,
    });
  }
  return { Authorization: `Bearer ${token}` };
};

const fallbackCode = (status) => {
  if (status === 401 || status === 403) return TechnicalV2ErrorCode.SESSION_ACCESS_DENIED;
  if (status === 404) return TechnicalV2ErrorCode.SESSION_NOT_FOUND;
  return TechnicalV2ErrorCode.UNKNOWN_ERROR;
};

const readJson = async (response) => {
  if (response.status === 204) return null;
  const contentType = response.headers.get('Content-Type') || '';
  if (!contentType.includes('json')) return null;
  return response.json().catch(() => null);
};

const request = async (url, options = {}) => {
  const timeoutController = new AbortController();
  let timedOut = false;
  const abortFromCaller = () => timeoutController.abort();
  options.signal?.addEventListener('abort', abortFromCaller, { once: true });
  const timeoutId = window.setTimeout(() => {
    timedOut = true;
    timeoutController.abort();
  }, 120000);

  let response;
  try {
    response = await fetch(url, {
      ...options,
      signal: timeoutController.signal,
      headers: {
        ...getAuthHeaders(),
        Accept: 'application/json',
        ...options.headers,
      },
    });
  } catch (error) {
    if (error instanceof TechnicalV2InterviewError) throw error;
    if (error?.name === 'AbortError' && timedOut) {
      throw new TechnicalV2InterviewError('Request timed out', {
        code: TechnicalV2ErrorCode.REQUEST_TIMEOUT,
      });
    }
    if (error?.name === 'AbortError') throw error;
    throw new TechnicalV2InterviewError('Network request failed', {
      code: TechnicalV2ErrorCode.NETWORK_ERROR,
      details: error,
    });
  } finally {
    window.clearTimeout(timeoutId);
    options.signal?.removeEventListener('abort', abortFromCaller);
  }

  const body = await readJson(response);
  if (!response.ok) {
    throw new TechnicalV2InterviewError(
      body?.detail || body?.message || body?.Message || `Request failed with status ${response.status}`,
      {
        code: body?.errorCode || body?.ErrorCode || body?.code || body?.Code || body?.title || body?.Title || fallbackCode(response.status),
        status: response.status,
        details: body,
      },
    );
  }
  return body;
};

const jsonOptions = (method, payload, headers = {}, signal) => ({
  method,
  signal,
  headers: {
    'Content-Type': 'application/json',
    ...headers,
  },
  body: payload === undefined ? undefined : JSON.stringify(payload),
});

const technicalV2InterviewApi = {
  initialize: (sessionId, requiredSkills, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_INITIALIZE(sessionId),
    jsonOptions('POST', requiredSkills?.length ? { requiredSkills } : {}, {}, signal),
  ),

  start: (sessionId, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_START(sessionId),
    { method: 'POST', signal },
  ),

  getState: (sessionId, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_STATE(sessionId),
    { signal },
  ),

  getCurrentQuestion: (sessionId, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_CURRENT_QUESTION(sessionId),
    { signal },
  ),

  submitAnswer: (sessionId, sessionQuestionId, payload, { idempotencyKey, signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_ANSWER(sessionId, sessionQuestionId),
    jsonOptions('POST', payload, idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {}, signal),
  ),

  complete: (sessionId, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_COMPLETE(sessionId),
    { method: 'POST', signal },
  ),

  generateFeedback: (sessionId, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_FEEDBACK(sessionId),
    { method: 'POST', signal },
  ),

  getResult: (sessionId, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_V2_INTERVIEW_RESULT(sessionId),
    { signal },
  ),
};

export default technicalV2InterviewApi;
