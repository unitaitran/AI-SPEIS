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
  QUESTIONS_GET: `${API_BASE_URL}/api/questions`,
  QUESTIONS_GET_BY_ID: (id) => `${API_BASE_URL}/api/questions/${id}`,
  SAVED_QUESTIONS_GET: `${API_BASE_URL}/api/SavedQuestions`,
  SAVED_QUESTIONS_SAVE: (id) => `${API_BASE_URL}/api/SavedQuestions/${id}`,
  SAVED_QUESTIONS_UNSAVE: (id) => `${API_BASE_URL}/api/SavedQuestions/${id}`,
};
