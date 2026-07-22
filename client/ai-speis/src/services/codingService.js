import { ENDPOINTS } from '../config/api';
import { getStoredSession } from '../routes/auth';

const parseJsonResponse = async (response) => {
  if (!response.ok) {
    const contentType = response.headers.get('Content-Type') || '';
    const body = contentType.includes('application/json')
      ? await response.json()
      : { message: await response.text() };
    const message = body?.message || body?.title || body?.Title || JSON.stringify(body);
    throw new Error(message || `Request failed with status ${response.status}`);
  }

  return response.json();
};

const getAuthHeaders = (includeJson = false) => {
  const headers = {
    Accept: 'application/json',
  };

  if (includeJson) {
    headers['Content-Type'] = 'application/json';
  }

  const session = getStoredSession();
  if (session?.token) {
    headers.Authorization = `Bearer ${session.token}`;
  }

  return headers;
};

export const codingService = {
  importCodingQuestions: async (file) => {
    if (!file) {
      throw new Error('No file provided');
    }

    const formData = new FormData();
    formData.append('file', file);

    const headers = getAuthHeaders();
    delete headers['Content-Type'];

    const response = await fetch(ENDPOINTS.CODING_ADMIN_IMPORT, {
      method: 'POST',
      headers: headers,
      body: formData,
    });

    return parseJsonResponse(response);
  },
  
  getQuestions: async (sessionId) => {
    const response = await fetch(ENDPOINTS.CODING_GET_QUESTIONS(sessionId), {
      method: 'GET',
      headers: getAuthHeaders(),
    });
    return parseJsonResponse(response);
  },

  submitCode: async (requestData) => {
    const response = await fetch(ENDPOINTS.CODING_SUBMIT, {
      method: 'POST',
      headers: getAuthHeaders(true),
      body: JSON.stringify(requestData)
    });
    return parseJsonResponse(response);
  },

  getSubmissionHistory: async (sessionId, questionId) => {
    const response = await fetch(ENDPOINTS.CODING_SUBMISSION_HISTORY(sessionId, questionId), {
      method: 'GET',
      headers: getAuthHeaders(),
    });
    return parseJsonResponse(response);
  },
  
  getLanguages: async () => {
    const response = await fetch(ENDPOINTS.CODING_LANGUAGES, {
      method: 'GET',
      headers: getAuthHeaders(),
    });
    return parseJsonResponse(response);
  }
};

export default codingService;
