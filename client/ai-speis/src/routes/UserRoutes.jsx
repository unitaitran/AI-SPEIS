import React, { useEffect } from 'react';
import DashboardPage from '../pages/user/DashboardPage';
import { navigate } from './navigation';
import { USER_ROUTES } from './routePaths';
import MyCVPage from '../pages/user/MyCVPage';
import CVJDManagementPage from '../pages/user/CVJDManagementPage';
import QuestionsPage from '../pages/user/QuestionsPage';
import DeviceReadinessCheckPage from '../pages/user/DeviceReadinessCheckPage';
import AIInterviewRoomPage from '../pages/user/AIInterviewRoomPage';
import ProfilePage from '../pages/user/ProfilePage';
import InterviewSetupPage from '../pages/user/InterviewSetupPage';
import InterviewModePage from '../pages/user/InterviewModePage';

function UserRoutes({ pathname }) {
  const isUserRoot = pathname === USER_ROUTES.ROOT || pathname === `${USER_ROUTES.ROOT}/`;
  const isProfileRoute = pathname === USER_ROUTES.PROFILE;
  const isKnownRoute =
    pathname === USER_ROUTES.DASHBOARD ||
    pathname === USER_ROUTES.CV ||
    pathname === USER_ROUTES.CV_DETAIL ||
    pathname === USER_ROUTES.QUESTIONS ||
    pathname === USER_ROUTES.INTERVIEW_MODE ||
    pathname === USER_ROUTES.INTERVIEW_SETUP ||
    pathname === USER_ROUTES.DEVICE_CHECK ||
    pathname === USER_ROUTES.INTERVIEW_ROOM;

  useEffect(() => {
    if ((isUserRoot || !isKnownRoute) && !isProfileRoute) {
      navigate(USER_ROUTES.DASHBOARD, { replace: true });
    }
  }, [isKnownRoute, isUserRoot, isProfileRoute]);

  if (isProfileRoute) {
    return <ProfilePage />;
  }

  if (pathname === USER_ROUTES.CV) {
    return <CVJDManagementPage />;
  }

  if (pathname === USER_ROUTES.CV_DETAIL) {
    return <MyCVPage />;
  }

  if (pathname === USER_ROUTES.QUESTIONS) {
    return <QuestionsPage />;
  }

  if (pathname === USER_ROUTES.INTERVIEW_MODE) {
    return <InterviewModePage />;
  }

  if (pathname === USER_ROUTES.INTERVIEW_SETUP) {
    return <InterviewSetupPage />;
  }

  if (pathname === USER_ROUTES.DEVICE_CHECK) {
    return <DeviceReadinessCheckPage />;
  }

  if (pathname === USER_ROUTES.INTERVIEW_ROOM) {
    return <AIInterviewRoomPage />;
  }

  return isKnownRoute ? <DashboardPage /> : null;
}

export default UserRoutes;
