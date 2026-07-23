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

  getAdminQuestionFilters: async () => {
    const response = await fetch(ENDPOINTS.ADMIN_QUESTIONS_FILTERS, {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    return parseJsonResponse(response);
  },

  getAdminQuestions: async (params = {}) => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('PageNumber', params.pageNumber);
    if (params.pageSize) queryParams.append('PageSize', params.pageSize);
    if (params.keyword) queryParams.append('Keyword', params.keyword);
    if (params.major && params.major !== 'all') queryParams.append('Major', params.major);
    if (params.roleTarget && params.roleTarget !== 'all') queryParams.append('RoleTarget', params.roleTarget);
    if (params.techStack && params.techStack !== 'all') queryParams.append('TechStack', params.techStack);
    if (params.interviewType && params.interviewType !== 'all') queryParams.append('InterviewType', params.interviewType);
    if (params.tags && params.tags !== 'all') queryParams.append('Tags', params.tags);
    if (params.status && params.status !== 'all') queryParams.append('Status', params.status);
    if (params.difficulty && params.difficulty !== 'all') queryParams.append('Difficulty', params.difficulty);
    if (params.includeDeleted) queryParams.append('IncludeDeleted', params.includeDeleted);
    if (params.sortBy) queryParams.append('SortBy', params.sortBy);
    if (params.sortDirection) queryParams.append('SortDirection', params.sortDirection);

    const queryString = queryParams.toString();
    const url = queryString ? `${ENDPOINTS.ADMIN_QUESTIONS}?${queryString}` : ENDPOINTS.ADMIN_QUESTIONS;

    const response = await fetch(url, {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    return parseJsonResponse(response);
  },

  createAdminQuestion: async (questionData) => {
    const response = await fetch(ENDPOINTS.ADMIN_QUESTIONS, {
      method: 'POST',
      headers: getAuthHeaders(true),
      body: JSON.stringify(questionData),
    });

    return parseJsonResponse(response);
  },

  updateAdminQuestion: async (questionId, questionData) => {
    if (!questionId) {
      throw new Error('Missing question ID');
    }

    const response = await fetch(ENDPOINTS.ADMIN_QUESTIONS_BY_ID(questionId), {
      method: 'PUT',
      headers: getAuthHeaders(true),
      body: JSON.stringify(questionData),
    });

    return parseJsonResponse(response);
  },

  deleteAdminQuestion: async (questionId) => {
    if (!questionId) {
      throw new Error('Missing question ID');
    }

    const response = await fetch(ENDPOINTS.ADMIN_QUESTIONS_BY_ID(questionId), {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (response.status === 204) return true;
    return parseJsonResponse(response);
  },

  importQuestions: async (file) => {
    if (!file) {
      throw new Error('No file provided');
    }

    const formData = new FormData();
    formData.append('file', file);

    const headers = getAuthHeaders();
    // Remove Content-Type so browser can set it with the boundary for FormData
    delete headers['Content-Type'];

    const response = await fetch(ENDPOINTS.ADMIN_QUESTIONS_IMPORT, {
      method: 'POST',
      headers: headers,
      body: formData,
    });

    return parseJsonResponse(response);
  },
};

export default questionService;
