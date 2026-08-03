import { ENDPOINTS } from '../config/api';

/**
 * Fetches unified Admin Dashboard aggregated data.
 * @param {string} token - JWT bearer token
 * @returns {Promise<Object>} Dashboard aggregated response
 */
export async function fetchAdminDashboard(token) {
  const response = await fetch(ENDPOINTS.ADMIN_DASHBOARD, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({}));
    throw new Error(err.message || err.detail || `Lỗi tải dữ liệu Dashboard (${response.status})`);
  }

  return response.json();
}
