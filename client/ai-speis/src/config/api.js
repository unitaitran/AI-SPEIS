// API Configuration
export const API_BASE_URL = 'http://localhost:5274';

export const ENDPOINTS = {
  LOGIN: `${API_BASE_URL}/api/Authentication/login`,
  REGISTER: `${API_BASE_URL}/api/Authentication/register`,
  GOOGLE_LOGIN: `${API_BASE_URL}/api/Authentication/oauth/google`,
};
