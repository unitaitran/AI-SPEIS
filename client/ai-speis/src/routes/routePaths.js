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
  INTERVIEW_MODE: '/user/interview/mode',
  INTERVIEW_SETUP: '/user/interview/setup',
  DEVICE_CHECK: '/user/interview/device-check',
  INTERVIEW_ROOM: '/user/interview/room',
  INTERVIEW_RESULT: '/user/interview/result',
  PAYMENT_RESULT: '/user/packages/payment-result',
};

export const getInterviewRoomPath = (sessionId) => (
  sessionId ? `${USER_ROUTES.INTERVIEW_ROOM}/${encodeURIComponent(sessionId)}` : USER_ROUTES.INTERVIEW_ROOM
);

export const getInterviewResultPath = (sessionId) => (
  sessionId ? `${USER_ROUTES.INTERVIEW_RESULT}/${encodeURIComponent(sessionId)}` : USER_ROUTES.INTERVIEW_RESULT
);

export const AUTHENTICATED_ADMIN_ROUTES = {
  ROOT: '/admin',
  DASHBOARD: '/admin/dashboard',
};
