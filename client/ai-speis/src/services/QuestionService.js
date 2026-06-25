import { ENDPOINTS } from '../config/api';

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

  const token = localStorage.getItem('token');
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  return headers;
};

export const questionService = {
  getQuestions: async () => {
    const response = await fetch(ENDPOINTS.QUESTIONS_GET, {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    return parseJsonResponse(response);
  },

  getQuestionById: async (questionId) => {
    if (!questionId) {
      throw new Error('Missing question ID');
    }

    const response = await fetch(ENDPOINTS.QUESTIONS_GET_BY_ID(questionId), {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    return parseJsonResponse(response);
  },

  createQuestion: async (questionData) => {
    const response = await fetch(ENDPOINTS.QUESTIONS_GET, {
      method: 'POST',
      headers: getAuthHeaders(true),
      body: JSON.stringify(questionData),
    });

    return parseJsonResponse(response);
  },

  updateQuestion: async (questionId, questionData) => {
    if (!questionId) {
      throw new Error('Missing question ID');
    }

    const response = await fetch(ENDPOINTS.QUESTIONS_GET_BY_ID(questionId), {
      method: 'PUT',
      headers: getAuthHeaders(true),
      body: JSON.stringify(questionData),
    });

    return parseJsonResponse(response);
  },

  deleteQuestion: async (questionId) => {
    if (!questionId) {
      throw new Error('Missing question ID');
    }

    const response = await fetch(ENDPOINTS.QUESTIONS_GET_BY_ID(questionId), {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    return parseJsonResponse(response);
  },

  importExcel: async () => {
    throw new Error('Import Excel is not supported by the current backend API');
  },
};

export default questionService;
