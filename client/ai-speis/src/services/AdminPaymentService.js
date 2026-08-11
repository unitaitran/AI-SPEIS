import { ENDPOINTS } from '../config/api';

/**
 * Helper to build auth headers.
 */
function authHeaders(token) {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };
}

/**
 * Fetches paginated payment list for Admin.
 */
export async function fetchAdminPayments(token, params = {}) {
  const queryParams = new URLSearchParams();
  if (params.page) queryParams.append('page', params.page);
  if (params.pageSize) queryParams.append('pageSize', params.pageSize);
  if (params.status != null && params.status !== '') queryParams.append('status', params.status);
  if (params.planId != null && params.planId !== '') queryParams.append('planId', params.planId);
  if (params.dateFrom) queryParams.append('dateFrom', params.dateFrom);
  if (params.dateTo) queryParams.append('dateTo', params.dateTo);
  if (params.search) queryParams.append('search', params.search);
  if (params.sortBy) queryParams.append('sortBy', params.sortBy);

  const url = `${ENDPOINTS.ADMIN_PAYMENTS}?${queryParams.toString()}`;
  const response = await fetch(url, {
    method: 'GET',
    headers: authHeaders(token),
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({}));
    throw new Error(err.message || err.detail || `Failed to fetch payments (${response.status})`);
  }

  return response.json();
}

/**
 * Fetches payment statistics & revenue metrics.
 */
export async function fetchAdminPaymentStatistics(token) {
  const response = await fetch(ENDPOINTS.ADMIN_PAYMENTS_STATISTICS, {
    method: 'GET',
    headers: authHeaders(token),
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({}));
    throw new Error(err.message || err.detail || `Failed to fetch payment statistics (${response.status})`);
  }

  return response.json();
}

/**
 * Fetches detailed info for a single payment transaction.
 */
export async function fetchAdminPaymentDetail(token, id) {
  const response = await fetch(ENDPOINTS.ADMIN_PAYMENT_DETAIL(id), {
    method: 'GET',
    headers: authHeaders(token),
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({}));
    throw new Error(err.message || err.detail || `Failed to fetch payment detail (${response.status})`);
  }

  return response.json();
}

/**
 * Re-verifies payment transaction with MoMo API.
 */
export async function verifyAdminPayment(token, id) {
  const response = await fetch(ENDPOINTS.ADMIN_PAYMENTS_VERIFY(id), {
    method: 'POST',
    headers: authHeaders(token),
  });

  const data = await response.json().catch(() => ({}));

  if (!response.ok) {
    throw new Error(data.message || data.Message || `Verification failed (${response.status})`);
  }

  return data;
}

/**
 * Downloads Excel/CSV export of payment records.
 */
export async function downloadAdminPaymentsExport(token, params = {}) {
  const queryParams = new URLSearchParams();
  if (params.status != null && params.status !== '') queryParams.append('status', params.status);
  if (params.planId != null && params.planId !== '') queryParams.append('planId', params.planId);
  if (params.dateFrom) queryParams.append('dateFrom', params.dateFrom);
  if (params.dateTo) queryParams.append('dateTo', params.dateTo);
  if (params.search) queryParams.append('search', params.search);
  if (params.sortBy) queryParams.append('sortBy', params.sortBy);

  const url = `${ENDPOINTS.ADMIN_PAYMENTS_EXPORT}?${queryParams.toString()}`;
  const response = await fetch(url, {
    method: 'GET',
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new Error(`Export failed (${response.status})`);
  }

  const blob = await response.blob();
  const downloadUrl = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = downloadUrl;
  a.download = `MoMo_Payments_${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(downloadUrl);
}
