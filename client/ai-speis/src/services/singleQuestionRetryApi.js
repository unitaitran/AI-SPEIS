import { ENDPOINTS } from '../config/api';

const authHeaders = () => {
  const token = localStorage.getItem('token');
  return token ? { Authorization: `Bearer ${token}` } : {};
};

const request = async (url, options = {}) => {
  const response = await fetch(url, {
    ...options,
    headers: { ...authHeaders(), Accept: 'application/json', ...options.headers },
  });
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    const error = new Error(body?.detail || body?.title || `Request failed (${response.status})`);
    error.status = response.status;
    error.code = body?.title;
    throw error;
  }
  return body;
};

const singleQuestionRetryApi = {
  retryQuestion: (payload, { signal } = {}) => request(ENDPOINTS.QUESTION_RETRY, {
    method: 'POST', signal,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  }),
  evaluateSingleQuestionInterview: (payload, { signal } = {}) => request(ENDPOINTS.SINGLE_QUESTION_INTERVIEW_EVALUATE, {
    method: 'POST', signal,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  }),
  getRetryHistory: (questionId, { signal } = {}) => request(ENDPOINTS.QUESTION_RETRY_HISTORY(questionId), { signal }),
};

export default singleQuestionRetryApi;
