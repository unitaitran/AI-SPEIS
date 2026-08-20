import { API_BASE_URL } from '../config/api';

const AI_FEEDBACK_ENDPOINT = `${API_BASE_URL}/api/ai-feedback`;
const ADMIN_AI_FEEDBACK_ENDPOINT = `${API_BASE_URL}/api/admin/ai-feedback`;

class AiEvaluationFeedbackError extends Error {
  constructor(message, { status, details } = {}) {
    super(message);
    this.name = 'AiEvaluationFeedbackError';
    this.status = status;
    this.details = details;
  }
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) {
    throw new AiEvaluationFeedbackError('Authentication token not found', { status: 401 });
  }
  return { Authorization: `Bearer ${token}` };
};

const readResponseBody = async (response) => {
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
    if (error instanceof AiEvaluationFeedbackError) throw error;
    if (error?.name === 'AbortError' && timedOut) {
      throw new AiEvaluationFeedbackError('Request timed out');
    }
    if (error?.name === 'AbortError') throw error;
    throw new AiEvaluationFeedbackError('Network request failed', { details: error });
  } finally {
    window.clearTimeout(timeoutId);
    options.signal?.removeEventListener('abort', abortFromCaller);
  }

  const body = await readResponseBody(response);
  if (!response.ok) {
    throw new AiEvaluationFeedbackError(
      body?.message || body?.Message || body?.detail || `Request failed with status ${response.status}`,
      {
        status: response.status,
        details: body,
      },
    );
  }

  return body;
};

const submitEvaluationFeedback = (payload, { signal } = {}) => request(
  AI_FEEDBACK_ENDPOINT,
  {
    method: 'POST',
    signal,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  },
);

const getMyFeedback = ({ signal } = {}) => request(
  `${AI_FEEDBACK_ENDPOINT}/me`,
  { signal },
);

const getAdminFeedback = ({ search = '', pageNumber = 1, pageSize = 20, signal } = {}) => {
  const params = new URLSearchParams({ search, pageNumber: String(pageNumber), pageSize: String(pageSize) });
  return request(`${ADMIN_AI_FEEDBACK_ENDPOINT}?${params.toString()}`, { signal });
};

const getAdminFeedbackDetail = (feedbackId, { signal } = {}) => request(
  `${ADMIN_AI_FEEDBACK_ENDPOINT}/${feedbackId}`,
  { signal },
);

export { submitEvaluationFeedback, getMyFeedback, getAdminFeedback, getAdminFeedbackDetail, AiEvaluationFeedbackError };

const aiEvaluationFeedbackApi = {
  submitEvaluationFeedback,
  getMyFeedback,
  getAdminFeedback,
  getAdminFeedbackDetail,
};

export default aiEvaluationFeedbackApi;
