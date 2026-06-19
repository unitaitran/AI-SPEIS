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
};
