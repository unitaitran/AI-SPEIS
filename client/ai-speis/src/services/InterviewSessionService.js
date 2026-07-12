import { ENDPOINTS } from '../config/api';

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) throw new Error('Authentication token not found');
  return { Authorization: `Bearer ${token}` };
};

const handleResponse = async (response) => {
  if (!response.ok) {
    const contentType = response.headers.get('Content-Type') || '';
    let message = `Request failed with status ${response.status}`;

    if (contentType.includes('application/json')) {
      const body = await response.json().catch(() => ({}));
      const validationErrors = Object.values(body?.errors || body || {})
        .flatMap((value) => (Array.isArray(value) ? value : []))
        .filter((value) => typeof value === 'string');
      message = body?.message
        || body?.Message
        || body?.detail
        || validationErrors[0]
        || message;
    }

    throw new Error(message);
  }

  return response.json();
};

const interviewSessionService = {
  /** GET /api/InterviewSession/jd/{jdId}/available-types */
  getAvailableTypes: async (jdId) => {
    const response = await fetch(ENDPOINTS.INTERVIEW_AVAILABLE_TYPES(jdId), {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** POST /api/InterviewSession */
  createSession: async (setup) => {
    const response = await fetch(ENDPOINTS.INTERVIEW_SESSIONS, {
      method: 'POST',
      headers: {
        ...getAuthHeaders(),
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(setup),
    });
    return handleResponse(response);
  },

  /** GET /api/InterviewSession/campaign/{campaignId} */
  getCampaign: async (campaignId) => {
    const response = await fetch(ENDPOINTS.INTERVIEW_CAMPAIGN(campaignId), {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** GET /api/InterviewSession/{id} */
  getSession: async (sessionId) => {
    const response = await fetch(ENDPOINTS.INTERVIEW_SESSION(sessionId), {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** POST /api/InterviewSession/{id}/start */
  startSession: async (sessionId) => {
    const response = await fetch(ENDPOINTS.INTERVIEW_SESSION_START(sessionId), {
      method: 'POST',
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },
};

export default interviewSessionService;
