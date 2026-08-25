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

  return (
    <div className="flex min-h-screen w-full items-center justify-center bg-surface-1">
      <div className="h-8 w-8 animate-spin rounded-full border-3 border-primary border-t-transparent" />
    </div>
  );
}

function AppRoutes() {
  const [pathname, setPathname] = useState(window.location.pathname);

  useEffect(() => {
    const syncPathname = () => setPathname(window.location.pathname);

    window.addEventListener('popstate', syncPathname);
    window.addEventListener(NAVIGATION_EVENT, syncPathname);

    // Handle OAuth redirect from backend
    const hash = window.location.hash;
    if (hash.startsWith('#/packages/payment-result')) {
      const queryIndex = hash.indexOf('?');
      const queryString = queryIndex >= 0 ? hash.slice(queryIndex) : '';
      const target = `${USER_ROUTES.PACKAGES}${queryString}`;

      window.history.replaceState(null, '', target);
      setPathname(target.split('?')[0]);
      return;
    }

    if (hash.startsWith('#dashboard?')) {
      const queryString = hash.split('?')[1];
      const urlParams = new URLSearchParams(queryString);

      const token = urlParams.get('token');
      const userId = urlParams.get('userId');
      const fullName = urlParams.get('fullName');
      const email = urlParams.get('email');
      const role = urlParams.get('role');
      const imageUrl = urlParams.get('imageUrl');
      const isPremiumParam = urlParams.get('isPremium');
      const remainingInterviewQuotaParam = urlParams.get('remainingInterviewQuota');

      if (token) {
        localStorage.setItem('token', token);
        localStorage.setItem('user', JSON.stringify({
          userId: parseInt(userId, 10),
          fullName: fullName,
          email: email,
          role: role,
          avatar: imageUrl,
          isPremium: isPremiumParam === 'true' || isPremiumParam === 'True',
          remainingInterviewQuota: remainingInterviewQuotaParam ? parseInt(remainingInterviewQuotaParam, 10) : undefined
        }));
        
        const dashboardPath = getDefaultRouteForRole(role);
        
        // Replace the current history entry (the OAuth callback URL) with dashboard
        // This prevents Back button from going to Google auth page
        window.history.replaceState(null, '', dashboardPath);
        
        // Sync pathname state directly to trigger re-render immediately
        setPathname(dashboardPath);
        return;
      }
    }

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

  if (pathname === '/dashboard' || pathname === '/dashboard/') {
    return (
      <RouteRedirect
        to={session ? getDefaultRouteForRole(session?.user?.role) : PUBLIC_ROUTES.LOGIN}
      />
    );
  }

  if (pathname === '/packages' || pathname.startsWith('/packages/')) {
    const search = window.location.search || '';
    const target = `${USER_ROUTES.PACKAGES}${search}`;
    return (
      <RouteRedirect
        to={session ? target : `${PUBLIC_ROUTES.LOGIN}${search}`}
      />
    );
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

  // Prevent flashing the landing page while OAuth callback is being processed in useEffect
  if (window.location.hash.startsWith('#dashboard?')) {
    return (
      <div className="flex min-h-screen w-full items-center justify-center bg-surface-1">
        <div className="h-8 w-8 animate-spin rounded-full border-3 border-primary border-t-transparent" />
      </div>
    );
  }

  return <App />;
}

export default AppRoutes;
