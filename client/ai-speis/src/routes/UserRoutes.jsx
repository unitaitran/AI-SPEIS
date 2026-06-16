import React, { useEffect } from 'react';
import DashboardPage from '../pages/user/DashboardPage';
import ProfilePage from '../pages/user/Profile/ProfilePage';
import { navigate } from './navigation';
import { USER_ROUTES } from './routePaths';

function UserRoutes({ pathname }) {
  const isUserRoot = pathname === USER_ROUTES.ROOT || pathname === `${USER_ROUTES.ROOT}/`;
  const isKnownRoute = pathname === USER_ROUTES.DASHBOARD || pathname === USER_ROUTES.PROFILE;

  useEffect(() => {
    if (isUserRoot || !isKnownRoute) {
      navigate(USER_ROUTES.DASHBOARD, { replace: true });
    }
  }, [isKnownRoute, isUserRoot]);

  if (pathname === USER_ROUTES.PROFILE) {
    return <ProfilePage />;
  }

  return isKnownRoute ? <DashboardPage /> : null;
}

export default UserRoutes;
