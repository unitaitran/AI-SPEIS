import { ENDPOINTS } from '../config/api';

const API_NOT_SUPPORTED_MESSAGE = 'API chưa được Backend hỗ trợ.';

const getToken = () => {
  const token = localStorage.getItem('token');
  if (!token) {
    throw new Error('Authentication token not found');
  }

  return token;
};

const authHeaders = (token) => ({
  Authorization: `Bearer ${token}`,
  Accept: 'application/json',
  'Content-Type': 'application/json',
});

const parseJsonResponse = async (response) => {
  if (response.status === 204) {
    return null;
  }

  const contentType = response.headers.get('Content-Type') || '';
  const body = contentType.includes('application/json')
    ? await response.json()
    : { message: await response.text() };

  if (!response.ok) {
    const message = body?.message || body?.Message || body?.title || body?.Title || 'Request failed';
    const error = new Error(message);
    error.code = body?.code || body?.Code;
    throw error;
  }

  return body;
};

const request = async (url, options = {}) => {
  const token = getToken();
  const response = await fetch(url, {
    ...options,
    headers: {
      ...authHeaders(token),
      ...(options.headers || {}),
    },
  });

  return parseJsonResponse(response);
};

const toPositiveInt = (value, fallback = 0) => {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? Math.floor(number) : fallback;
};

const normalizeBillingCycle = (value) => {
  if (value === 1 || value === '1' || String(value).toLowerCase() === 'monthly') return 1;
  if (value === 2 || value === '2' || String(value).toLowerCase() === 'yearly') return 2;
  return null;
};

const buildPlanPayload = (payload) => ({
  code: String(payload.code || '').trim(),
  name: String(payload.name || '').trim(),
  description: payload.description ? String(payload.description).trim() : null,
  interviewQuota: toPositiveInt(payload.interviewQuota, 0),
  quotaResetDays: payload.isFree ? null : (payload.quotaResetDays === null || payload.quotaResetDays === undefined || payload.quotaResetDays === ''
    ? null
    : toPositiveInt(payload.quotaResetDays, 0)),
  isFree: Boolean(payload.isFree),
  displayOrder: toPositiveInt(payload.displayOrder, 0),
});

const buildPricePayload = (payload) => ({
  billingCycle: normalizeBillingCycle(payload.billingCycle),
  billingCycleCount: toPositiveInt(payload.billingCycleCount, 1),
  amount: Number(payload.amount),
  currency: String(payload.currency || '').trim().toUpperCase(),
  effectiveFrom: payload.effectiveFrom,
  effectiveTo: payload.effectiveTo || null,
});

const subscriptionPlanService = {
  getPlans: async () => request(ENDPOINTS.ADMIN_SUBSCRIPTION_PLANS),

  getPlanById: async (planId) => {
    const plans = await request(ENDPOINTS.ADMIN_SUBSCRIPTION_PLANS);
    const targetPlan = plans.find((plan) => plan.planId === planId);

    if (!targetPlan) {
      throw new Error('Không tìm thấy gói.');
    }

    return targetPlan;
  },

  getMonitoring: async () => request(ENDPOINTS.ADMIN_SUBSCRIPTION_MONITORING),

  createPlan: async (payload) => request(ENDPOINTS.ADMIN_SUBSCRIPTION_PLANS, {
    method: 'POST',
    body: JSON.stringify(buildPlanPayload(payload)),
  }),

  updatePlan: async (planId, payload) => request(ENDPOINTS.ADMIN_SUBSCRIPTION_PLAN_BY_ID(planId), {
    method: 'PUT',
    body: JSON.stringify(buildPlanPayload(payload)),
  }),

  updatePlanStatus: async (planId, isActive) => request(ENDPOINTS.ADMIN_SUBSCRIPTION_PLAN_STATUS(planId), {
    method: 'PATCH',
    body: JSON.stringify({ isActive: Boolean(isActive) }),
  }),

  createPrice: async (planId, payload) => request(ENDPOINTS.ADMIN_SUBSCRIPTION_PLAN_PRICES(planId), {
    method: 'POST',
    body: JSON.stringify(buildPricePayload(payload)),
  }),

  updatePrice: async (priceId, payload) => request(ENDPOINTS.ADMIN_SUBSCRIPTION_PRICE_BY_ID(priceId), {
    method: 'PUT',
    body: JSON.stringify(buildPricePayload(payload)),
  }),

  updatePriceStatus: async (priceId, isActive) => request(ENDPOINTS.ADMIN_SUBSCRIPTION_PRICE_STATUS(priceId), {
    method: 'PATCH',
    body: JSON.stringify({ isActive: Boolean(isActive) }),
  }),

  duplicatePlan: async () => {
    throw new Error(API_NOT_SUPPORTED_MESSAGE);
  },

  deletePlan: async () => {
    throw new Error(API_NOT_SUPPORTED_MESSAGE);
  },

  getPlanPurchaseHistory: async () => {
    throw new Error(API_NOT_SUPPORTED_MESSAGE);
  },
};

export default subscriptionPlanService;
export { API_NOT_SUPPORTED_MESSAGE };
