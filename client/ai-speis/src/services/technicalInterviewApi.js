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
  if (!contentType.includes('application/json')) return null;
  return response.json().catch(() => null);
};

const getFallbackErrorCode = (status) => {
  if (status === 401 || status === 403) return TechnicalInterviewErrorCode.SESSION_ACCESS_DENIED;
  if (status === 404) return TechnicalInterviewErrorCode.SESSION_NOT_FOUND;
  return TechnicalInterviewErrorCode.UNKNOWN_ERROR;
};

const request = async (url, options = {}) => {
  let response;
  try {
    response = await fetch(url, {
      ...options,
      headers: {
        ...getAuthHeaders(),
        Accept: 'application/json',
        ...options.headers,
      },
    });
  } catch (error) {
    throw new TechnicalInterviewError('Network request failed', {
      code: TechnicalInterviewErrorCode.NETWORK_ERROR,
      details: error,
    });
  }

  const body = await readResponseBody(response);
  if (!response.ok) {
    throw new TechnicalInterviewError(
      body?.message || body?.Message || body?.detail || `Request failed with status ${response.status}`,
      {
        code: body?.code || body?.errorCode || body?.error?.code || getFallbackErrorCode(response.status),
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
  createSession: (payload) => request(
    ENDPOINTS.TECHNICAL_INTERVIEW_SESSIONS,
    jsonOptions('POST', payload),
  ),

  getSession: (sessionId) => request(ENDPOINTS.TECHNICAL_INTERVIEW_SESSION(sessionId)),

  startSession: (sessionId) => request(
    ENDPOINTS.TECHNICAL_INTERVIEW_START(sessionId),
    jsonOptions('POST'),
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
    jsonOptions('POST'),
  ),

  getResult: (sessionId) => request(ENDPOINTS.TECHNICAL_INTERVIEW_RESULT(sessionId)),
};

export default technicalInterviewApi;
