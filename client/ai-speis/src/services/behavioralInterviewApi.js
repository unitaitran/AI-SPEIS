import { ENDPOINTS } from '../config/api';
import { BehavioralErrorCode } from '../features/behavioralInterview/behavioralInterview.types';

export class BehavioralInterviewError extends Error {
  constructor(message, { code, status, details } = {}) {
    super(message);
    this.name = 'BehavioralInterviewError';
    this.code = code || BehavioralErrorCode.UNKNOWN_ERROR;
    this.status = status;
    this.details = details;
  }
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) {
    throw new BehavioralInterviewError('Authentication token not found', {
      code: BehavioralErrorCode.SESSION_ACCESS_DENIED,
      status: 401,
    });
  }
  return { Authorization: `Bearer ${token}` };
};

const fallbackCode = (status) => {
  if (status === 401 || status === 403) return BehavioralErrorCode.SESSION_ACCESS_DENIED;
  if (status === 404) return BehavioralErrorCode.SESSION_NOT_FOUND;
  return BehavioralErrorCode.UNKNOWN_ERROR;
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
    if (error instanceof BehavioralInterviewError) throw error;
    if (error?.name === 'AbortError' && timedOut) {
      throw new BehavioralInterviewError('Request timed out', {
        code: BehavioralErrorCode.REQUEST_TIMEOUT,
      });
    }
    if (error?.name === 'AbortError') throw error;
    throw new BehavioralInterviewError('Network request failed', {
      code: BehavioralErrorCode.NETWORK_ERROR,
      details: error,
    });
  } finally {
    window.clearTimeout(timeoutId);
    options.signal?.removeEventListener('abort', abortFromCaller);
  }

  const body = await readJson(response);
  if (!response.ok) {
    throw new BehavioralInterviewError(
      body?.message || body?.Message || body?.detail || `Request failed with status ${response.status}`,
      {
        code: body?.errorCode || body?.ErrorCode || body?.code || body?.Code || fallbackCode(response.status),
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

const behavioralInterviewApi = {
  initialize: (sessionId, requiredSkills, { signal } = {}) => request(
    ENDPOINTS.BEHAVIORAL_INTERVIEW_INITIALIZE(sessionId),
    jsonOptions('POST', requiredSkills?.length ? { requiredSkills } : {}, {}, signal),
  ),

  start: (sessionId, { signal } = {}) => request(
    ENDPOINTS.BEHAVIORAL_INTERVIEW_START(sessionId),
    { method: 'POST', signal },
  ),

  getState: (sessionId, { signal } = {}) => request(
    ENDPOINTS.BEHAVIORAL_INTERVIEW_STATE(sessionId),
    { signal },
  ),

  getCurrentQuestion: (sessionId, { signal } = {}) => request(
    ENDPOINTS.BEHAVIORAL_INTERVIEW_CURRENT_QUESTION(sessionId),
    { signal },
  ),

  submitAnswer: (sessionId, sessionQuestionId, payload, { idempotencyKey, signal } = {}) => request(
    ENDPOINTS.BEHAVIORAL_INTERVIEW_ANSWER(sessionId, sessionQuestionId),
    jsonOptions('POST', payload, idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {}, signal),
  ),

  complete: (sessionId, { signal } = {}) => request(
    ENDPOINTS.BEHAVIORAL_INTERVIEW_COMPLETE(sessionId),
    { method: 'POST', signal },
  ),

  getResult: (sessionId, { signal } = {}) => request(
    ENDPOINTS.BEHAVIORAL_INTERVIEW_RESULT(sessionId),
    { signal },
  ),
};

export default behavioralInterviewApi;
