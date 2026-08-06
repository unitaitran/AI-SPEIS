export const PUBLIC_ROUTES = {
  HOME: '/',
  LOGIN: '/#login',
};

export const USER_ROUTES = {
  ROOT: '/user',
  DASHBOARD: '/user/dashboard',
  PROFILE: '/user/profile',
  PACKAGES: '/user/packages',
  CV: '/user/cv-management',
  CV_DETAIL: '/user/cv',
  QUESTIONS: '/user/questions',
  INTERVIEW_HISTORY: '/user/interview-history',
  INTERVIEW_REVIEW: '/user/interview-history',
  INTERVIEW_MODE: '/user/interview/mode',
  INTERVIEW_SETUP: '/user/interview/setup',
  DEVICE_CHECK: '/user/interview/device-check',
  INTERVIEW_ROOM: '/user/interview/room',
  INTERVIEW_RESULT: '/user/interview/result',
  CAMPAIGN_RESULT: '/user/interview/campaign-result',
  CODING_INTERVIEW_ROOM: '/user/coding-interview',
  PAYMENT_RESULT: '/user/packages/payment-result',
  NOTIFICATIONS: '/user/notifications',
};

export const getInterviewRoomPath = (sessionId) => (
  sessionId ? `${USER_ROUTES.INTERVIEW_ROOM}/${encodeURIComponent(sessionId)}` : USER_ROUTES.INTERVIEW_ROOM
);

export const getInterviewResultPath = (sessionId) => (
  sessionId ? `${USER_ROUTES.INTERVIEW_RESULT}/${encodeURIComponent(sessionId)}` : USER_ROUTES.INTERVIEW_RESULT
);

export const getInterviewReviewPath = (sessionId) => (
  sessionId
    ? `${USER_ROUTES.INTERVIEW_REVIEW}/${encodeURIComponent(sessionId)}/review`
    : USER_ROUTES.INTERVIEW_HISTORY
);

export const getCampaignResultPath = (campaignId) => (
  campaignId ? `${USER_ROUTES.CAMPAIGN_RESULT}/${encodeURIComponent(campaignId)}` : USER_ROUTES.CAMPAIGN_RESULT
);

export const getCodingInterviewRoomPath = (sessionId) => (
  sessionId ? `${USER_ROUTES.CODING_INTERVIEW_ROOM}/${encodeURIComponent(sessionId)}` : USER_ROUTES.CODING_INTERVIEW_ROOM
);

export const AUTHENTICATED_ADMIN_ROUTES = {
  ROOT: '/admin',
  DASHBOARD: '/admin/dashboard',
  USERS: '/admin/users',
  QUESTIONS: '/admin/questions',
  SUBSCRIPTION: '/admin/subscription',
  PAYMENTS: '/admin/payments',
  AI_USAGE: '/admin/ai-usage',
  NOTIFICATIONS: '/admin/notifications',
};
