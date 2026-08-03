import { ENDPOINTS } from '../config/api';

/**
 * Fetches Google Cloud quota overview from the backend.
 * Requires admin JWT token.
 *
 * @param {string} token - JWT token for admin authorization.
 * @returns {Promise<Object>} GoogleQuotaResponseDto
 */
export async function fetchGoogleQuota(token) {
  const response = await fetch(ENDPOINTS.ADMIN_GOOGLE_QUOTA, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const errorBody = await response.json().catch(() => null);
    throw new Error(
      errorBody?.detail || `Failed to fetch Google quota (HTTP ${response.status})`
    );
  }

  return response.json();
}

/**
 * Fetches unified AI Usage & Google Cloud Resource Dashboard (Usage + Billing Cost).
 * Requires admin JWT token.
 *
 * @param {string} token - JWT token for admin authorization.
 * @returns {Promise<Object>} GoogleDashboardResponseDto
 */
export async function fetchGoogleDashboard(token) {
  const response = await fetch(ENDPOINTS.ADMIN_GOOGLE_DASHBOARD, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const errorBody = await response.json().catch(() => null);
    throw new Error(
      errorBody?.detail || `Failed to fetch Google Dashboard (HTTP ${response.status})`
    );
  }

  return response.json();
}
