import React, { useEffect } from 'react';
import DashboardPage from '../pages/user/DashboardPage';
import ProfilePage from '../pages/user/Profile/ProfilePage';
import MyCVPage from '../pages/user/MyCVPage';
import { navigate } from './navigation';
import { USER_ROUTES } from './routePaths';

function UserRoutes({ pathname }) {
  const isUserRoot = pathname === USER_ROUTES.ROOT || pathname === `${USER_ROUTES.ROOT}/`;
  const isKnownRoute = pathname === USER_ROUTES.DASHBOARD || pathname === USER_ROUTES.PROFILE || pathname === USER_ROUTES.CV;

  useEffect(() => {
    if (isUserRoot || !isKnownRoute) {
      navigate(USER_ROUTES.DASHBOARD, { replace: true });
    }
  }, [isKnownRoute, isUserRoot]);

  if (pathname === USER_ROUTES.PROFILE) {
    return <ProfilePage />;
  }

  if (pathname === USER_ROUTES.CV) {
    return <MyCVPage />;
  }

  return isKnownRoute ? <DashboardPage /> : null;
}

export default UserRoutes;
