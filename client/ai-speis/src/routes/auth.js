import {
  AUTHENTICATED_ADMIN_ROUTES,
  PUBLIC_ROUTES,
  USER_ROUTES,
} from './routePaths';

export const ROLES = {
  ADMIN: 'admin',
  USER: 'user',
};

export function getStoredSession() {
  const token = localStorage.getItem('token');
  const userValue = localStorage.getItem('user');

  if (!token || !userValue) {
    return null;
  }

  try {
    const user = JSON.parse(userValue);
    const role = String(user?.role || '').toLowerCase();

    if (!Object.values(ROLES).includes(role)) {
      return null;
    }

    return {
      token,
      user: {
        ...user,
        role,
      },
    };
  } catch {
    return null;
  }
}

export function getDefaultRouteForRole(role) {
  const normalizedRole = String(role || '').toLowerCase();

  if (normalizedRole === ROLES.ADMIN) {
    return AUTHENTICATED_ADMIN_ROUTES.DASHBOARD;
  }

  if (normalizedRole === ROLES.USER) {
    return USER_ROUTES.DASHBOARD;
  }

  return PUBLIC_ROUTES.LOGIN;
}
