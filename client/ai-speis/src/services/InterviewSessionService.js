import { ENDPOINTS } from '../config/api';

export class InterviewSessionError extends Error {
  constructor(message, { code, status, data, details } = {}) {
    super(message);
    this.name = 'InterviewSessionError';
    this.code = code || 'INTERVIEW_REQUEST_FAILED';
    this.status = status;
    this.data = data;
    this.details = details;
  }
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) throw new InterviewSessionError('Authentication token not found', {
    code: 'SESSION_ACCESS_DENIED',
    status: 401,
  });
  return { Authorization: `Bearer ${token}` };
};

const handleResponse = async (response) => {
  if (response.status === 204) return null;

  if (!response.ok) {
    const contentType = response.headers.get('Content-Type') || '';
    let message = `Request failed with status ${response.status}`;
    let body = null;

    if (contentType.includes('json')) {
      body = await response.json().catch(() => ({}));
      const validationErrors = Object.values(body?.errors || body || {})
        .flatMap((value) => (Array.isArray(value) ? value : []))
        .filter((value) => typeof value === 'string');
      message = body?.message
        || body?.Message
        || body?.detail
        || validationErrors[0]
        || message;
    }

    throw new InterviewSessionError(message, {
      code: body?.code || body?.Code || body?.errorCode,
      status: response.status,
      data: body?.data || body?.Data,
      details: body,
    });
  }

  return response.json();
};

const request = async (url, options = {}) => {
  const { timeout = 120000, ...fetchOptions } = options;
  const controller = new AbortController();
  const timeoutId = window.setTimeout(() => controller.abort(), timeout);
  try {
    const response = await fetch(url, { ...fetchOptions, signal: controller.signal });
    return await handleResponse(response);
  } catch (error) {
    if (error instanceof InterviewSessionError) throw error;
    if (error?.name === 'AbortError') {
      throw new InterviewSessionError('Interview request timed out', {
        code: 'REQUEST_TIMEOUT',
      });
    }
    throw new InterviewSessionError('Network request failed', {
      code: 'NETWORK_ERROR',
      details: error,
    });
  } finally {
    window.clearTimeout(timeoutId);
  }
};

const interviewSessionService = {
  /** GET /api/InterviewSession/jd/{jdId}/available-types */
  getAvailableTypes: (jdId) => request(ENDPOINTS.INTERVIEW_AVAILABLE_TYPES(jdId), {
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** POST /api/InterviewSession */
  createSession: (setup) => request(ENDPOINTS.INTERVIEW_SESSIONS, {
    method: 'POST',
    headers: {
      ...getAuthHeaders(),
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    body: JSON.stringify(setup),
  }),

  /** GET /api/InterviewSession/campaign/{campaignId} */
  getCampaign: (campaignId) => request(ENDPOINTS.INTERVIEW_CAMPAIGN(campaignId), {
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** GET /api/InterviewSession/campaign/{campaignId}/result */
  getCampaignResult: (campaignId) => request(ENDPOINTS.INTERVIEW_CAMPAIGN_RESULT(campaignId), {
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** GET /api/InterviewSession/active */
  getActiveCampaign: async () => {
    try {
      return await request(ENDPOINTS.INTERVIEW_ACTIVE_CAMPAIGN, {
        headers: { ...getAuthHeaders(), Accept: 'application/json' },
      });
    } catch (error) {
      if (error?.status === 404 || error?.status === 204) {
        return null;
      }
      throw error;
    }
  },

  /** GET /api/InterviewSession/{id} */
  getSession: (sessionId) => request(ENDPOINTS.INTERVIEW_SESSION(sessionId), {
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** POST /api/InterviewSession/{id}/start */
  startSession: (sessionId) => request(ENDPOINTS.INTERVIEW_SESSION_START(sessionId), {
    method: 'POST',
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** POST /api/InterviewSession/{id}/complete */
  completeSession: (sessionId) => request(ENDPOINTS.INTERVIEW_SESSION_COMPLETE(sessionId), {
    method: 'POST',
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** POST /api/InterviewSession/campaign/{campaignId}/cancel */
  cancelCampaign: (campaignId) => request(ENDPOINTS.INTERVIEW_CAMPAIGN_CANCEL(campaignId), {
    method: 'POST',
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** POST /api/InterviewSession/campaign/{campaignId}/expire */
  expireCampaign: (campaignId) => request(ENDPOINTS.INTERVIEW_CAMPAIGN_EXPIRE(campaignId), {
    method: 'POST',
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** GET /api/InterviewSession/quota */
  getQuota: () => request(ENDPOINTS.INTERVIEW_QUOTA, {
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),

  /** GET /api/InterviewSession/capabilities */
  getUserCapabilities: () => request(ENDPOINTS.INTERVIEW_CAPABILITIES, {
    headers: { ...getAuthHeaders(), Accept: 'application/json' },
  }),
};

export default interviewSessionService;
