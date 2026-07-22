import { ENDPOINTS } from '../config/api';
import { TechnicalInterviewError } from '../features/technicalInterview/technicalInterviewErrors';
import { TechnicalInterviewErrorCode } from '../features/technicalInterview/technicalInterview.types';

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) {
    throw new TechnicalInterviewError('Authentication token not found', {
      code: TechnicalInterviewErrorCode.SESSION_ACCESS_DENIED,
      status: 401,
    });
  }
  return { Authorization: `Bearer ${token}` };
};

const readResponseBody = async (response) => {
  if (response.status === 204) return null;
  const contentType = response.headers.get('Content-Type') || '';
  if (!contentType.includes('json')) return null;
  return response.json().catch(() => null);
};

const getFallbackErrorCode = (status) => {
  if (status === 401 || status === 403) return TechnicalInterviewErrorCode.SESSION_ACCESS_DENIED;
  if (status === 404) return TechnicalInterviewErrorCode.SESSION_NOT_FOUND;
  return TechnicalInterviewErrorCode.UNKNOWN_ERROR;
};

const request = async (url, options = {}) => {
  const callerSignal = options.signal;
  const timeoutController = callerSignal ? null : new AbortController();
  let timedOut = false;
  const timeoutId = timeoutController
    ? window.setTimeout(() => {
      timedOut = true;
      timeoutController.abort();
    }, 60000)
    : null;
  let response;
  try {
    response = await fetch(url, {
      ...options,
      signal: callerSignal || timeoutController.signal,
      headers: {
        ...getAuthHeaders(),
        Accept: 'application/json',
        ...options.headers,
      },
    });
  } catch (error) {
    if (error instanceof TechnicalInterviewError) throw error;
    if (error?.name === 'AbortError' && timedOut) {
      throw new TechnicalInterviewError('Request timed out', {
        code: TechnicalInterviewErrorCode.REQUEST_TIMEOUT,
      });
    }
    if (error?.name === 'AbortError') throw error;
    throw new TechnicalInterviewError('Network request failed', {
      code: TechnicalInterviewErrorCode.NETWORK_ERROR,
      details: error,
    });
  } finally {
    window.clearTimeout(timeoutId);
  }

  const body = await readResponseBody(response);
  if (!response.ok) {
    throw new TechnicalInterviewError(
      body?.message || body?.Message || body?.detail || `Request failed with status ${response.status}`,
      {
        code: body?.code
          || body?.errorCode
          || body?.error?.code
          || body?.title
          || body?.Title
          || getFallbackErrorCode(response.status),
        status: response.status,
        details: body,
      },
    );
  }
  return body;
};

const jsonOptions = (method, payload, extraHeaders = {}) => ({
  method,
  headers: {
    'Content-Type': 'application/json',
    ...extraHeaders,
  },
  body: payload === undefined ? undefined : JSON.stringify(payload),
});

const technicalInterviewApi = {
  initializeSession: (interviewSessionId, selectedSkills) => request(
    ENDPOINTS.TECHNICAL_INTERVIEW_SESSIONS,
    jsonOptions('POST', {
      interviewSessionId: Number.isInteger(Number(interviewSessionId))
        ? Number(interviewSessionId)
        : interviewSessionId,
      ...(selectedSkills?.length ? { selectedSkills } : {}),
    }),
  ),

  getSession: (sessionId) => request(ENDPOINTS.TECHNICAL_INTERVIEW_SESSION(sessionId)),

  startSession: (sessionId, { signal } = {}) => request(
    ENDPOINTS.TECHNICAL_INTERVIEW_START(sessionId),
    { method: 'POST', signal },
  ),

  getCurrentQuestion: (sessionId) => request(
    ENDPOINTS.TECHNICAL_INTERVIEW_CURRENT_QUESTION(sessionId),
  ),

  submitAnswer: (sessionId, payload, { idempotencyKey } = {}) => request(
    ENDPOINTS.TECHNICAL_INTERVIEW_ANSWERS(sessionId),
    jsonOptions('POST', payload, idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {}),
  ),

  completeSession: (sessionId) => request(
    ENDPOINTS.TECHNICAL_INTERVIEW_COMPLETE(sessionId),
    { method: 'POST' },
  ),

  getResult: (sessionId) => request(ENDPOINTS.TECHNICAL_INTERVIEW_RESULT(sessionId)),
};

export default technicalInterviewApi;
