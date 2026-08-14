import { ENDPOINTS } from '../../config/api';

function getToken() {
  return localStorage.getItem('token');
}

async function request(url, options = {}) {
  const token = getToken();
  if (!token) {
    const error = new Error('Authentication is required.');
    error.code = 'UNAUTHENTICATED';
    throw error;
  }

  const response = await fetch(url, {
    ...options,
    headers: {
      Authorization: `Bearer ${token}`,
      ...options.headers,
    },
  });

  if (response.status === 401 || response.status === 403) {
    const error = new Error('Your session is no longer authorized.');
    error.code = 'UNAUTHORIZED';
    throw error;
  }

  if (!response.ok) {
    throw new Error('Notifications could not be updated.');
  }

  if (response.status === 204) return null;
  return response.json();
}

function toQuery(filters, page, pageSize) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (filters?.readStatus && filters.readStatus !== 'ALL') query.set('readStatus', filters.readStatus);
  if (filters?.category && filters.category !== 'ALL') query.set('category', filters.category);
  return query.toString();
}

export const notificationService = {
  getNotifications(filters = {}, page = 1, pageSize = 20, signal) {
    return request(`${ENDPOINTS.NOTIFICATIONS}?${toQuery(filters, page, pageSize)}`, { signal });
  },
  getUnreadCount(signal) {
    return request(ENDPOINTS.NOTIFICATION_UNREAD_COUNT, { signal });
  },
  markAsRead(id) {
    return request(ENDPOINTS.NOTIFICATION_MARK_READ(id), { method: 'PATCH' });
  },
  markAllAsRead() {
    return request(ENDPOINTS.NOTIFICATION_MARK_ALL_READ, { method: 'PATCH' });
  },
  archive(id) {
    return request(ENDPOINTS.NOTIFICATION_ARCHIVE(id), { method: 'PATCH' });
  },
};

