import React, { useEffect, useState } from 'react';
import App from '../App';
import AdminRoutes from './AdminRoutes';
import UserRoutes from './UserRoutes';
import { getDefaultRouteForRole, getStoredSession, ROLES } from './auth';
import { navigate, NAVIGATION_EVENT } from './navigation';
import {
  AUTHENTICATED_ADMIN_ROUTES,
  PUBLIC_ROUTES,
  USER_ROUTES,
} from './routePaths';

function RouteRedirect({ to }) {
  useEffect(() => {
    navigate(to, { replace: true });
  }, [to]);

  return null;
}

function AppRoutes() {
  const [pathname, setPathname] = useState(window.location.pathname);

  useEffect(() => {
    const syncPathname = () => setPathname(window.location.pathname);

    window.addEventListener('popstate', syncPathname);
    window.addEventListener(NAVIGATION_EVENT, syncPathname);

    return () => {
      window.removeEventListener('popstate', syncPathname);
      window.removeEventListener(NAVIGATION_EVENT, syncPathname);
    };
  }, []);

  const session = getStoredSession();
  const isAdminRoute = pathname === AUTHENTICATED_ADMIN_ROUTES.ROOT
    || pathname.startsWith(`${AUTHENTICATED_ADMIN_ROUTES.ROOT}/`);
  const isUserRoute = pathname === USER_ROUTES.ROOT
    || pathname.startsWith(`${USER_ROUTES.ROOT}/`);

  if (isAdminRoute) {
    if (!session) {
      return <RouteRedirect to={PUBLIC_ROUTES.LOGIN} />;
    }

    if (session.user.role !== ROLES.ADMIN) {
      return <RouteRedirect to={getDefaultRouteForRole(session.user.role)} />;
    }

    return <AdminRoutes pathname={pathname} />;
  }

  if (isUserRoute) {
    if (!session) {
      return <RouteRedirect to={PUBLIC_ROUTES.LOGIN} />;
    }

    if (session.user.role !== ROLES.USER) {
      return <RouteRedirect to={getDefaultRouteForRole(session.user.role)} />;
    }

    return <UserRoutes pathname={pathname} />;
  }

  if (pathname === '/profile' || pathname === '/my-profile') {
    const legacyProfileTarget = session?.user.role === ROLES.USER
      ? USER_ROUTES.PROFILE
      : getDefaultRouteForRole(session?.user.role);

    return (
      <RouteRedirect
        to={session ? legacyProfileTarget : PUBLIC_ROUTES.LOGIN}
      />
    );
  }

  return <App />;
}

export default AppRoutes;
