// API Configuration
export const API_BASE_URL = 'http://localhost:5274';

export const ENDPOINTS = {
  LOGIN: `${API_BASE_URL}/api/auth/login`,
  REGISTER: `${API_BASE_URL}/api/auth/register`,
  GOOGLE_LOGIN: `${API_BASE_URL}/api/auth/oauth/google`,
  FORGOT_PASSWORD: `${API_BASE_URL}/api/auth/forgot-password`,
  RESET_PASSWORD: `${API_BASE_URL}/api/auth/reset-password`,
  GET_PROFILE: `${API_BASE_URL}/api/users/me`,
  UPDATE_PROFILE: `${API_BASE_URL}/api/users/me/profile`,
  CHANGE_PASSWORD: `${API_BASE_URL}/api/users/me/security`,
  CV_GET_MY: `${API_BASE_URL}/api/CVFile/MyCV`,
  CV_UPLOAD: `${API_BASE_URL}/api/CVFile/upload`,
  CV_DELETE: (id) => `${API_BASE_URL}/api/CVFile/${id}`,
  CV_PARSE: (id) => `${API_BASE_URL}/api/CVFile/${id}/parse`,
  CV_STATUS: (id) => `${API_BASE_URL}/api/CVFile/${id}/status`,
  CV_PARSED_DATA: (id) => `${API_BASE_URL}/api/CVFile/${id}/parsed-data`,
  CV_CONFIRM: (id) => `${API_BASE_URL}/api/CVFile/${id}/confirm`,
  QUESTIONS_GET: `${API_BASE_URL}/api/questions`,
  QUESTIONS_GET_BY_ID: (id) => `${API_BASE_URL}/api/questions/${id}`,
  SAVED_QUESTIONS_GET: `${API_BASE_URL}/api/SavedQuestions`,
  SAVED_QUESTIONS_SAVE: (id) => `${API_BASE_URL}/api/SavedQuestions/${id}`,
  SAVED_QUESTIONS_UNSAVE: (id) => `${API_BASE_URL}/api/SavedQuestions/${id}`,
  ADMIN_USERS: `${API_BASE_URL}/api/admin/users`,
  ADMIN_USER_LOCK: (userId) => `${API_BASE_URL}/api/admin/users/${userId}/lock`,
  ADMIN_USER_UNLOCK: (userId) => `${API_BASE_URL}/api/admin/users/${userId}/unlock`,
};
