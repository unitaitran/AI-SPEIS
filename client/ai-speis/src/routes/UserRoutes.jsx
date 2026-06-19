import React, { useEffect } from 'react';
import DashboardPage from '../pages/user/DashboardPage';
import { navigate } from './navigation';
import { USER_ROUTES } from './routePaths';
import MyCVPage from '../pages/user/MyCVPage';

function UserRoutes({ pathname }) {
  const isUserRoot = pathname === USER_ROUTES.ROOT || pathname === `${USER_ROUTES.ROOT}/`;
  const isProfileRoute = pathname === USER_ROUTES.PROFILE;
  const isKnownRoute = pathname === USER_ROUTES.DASHBOARD || pathname === USER_ROUTES.CV;

  useEffect(() => {
    if (isUserRoot || isProfileRoute || !isKnownRoute) {
      navigate(USER_ROUTES.DASHBOARD, { replace: true });
    }
  }, [isKnownRoute, isUserRoot, isProfileRoute]);

  if (pathname === USER_ROUTES.CV) {
    return <MyCVPage />;
  }

  return isKnownRoute ? <DashboardPage /> : null;
}

export default UserRoutes;
