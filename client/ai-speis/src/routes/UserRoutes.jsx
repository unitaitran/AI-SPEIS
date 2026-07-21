import React, { useEffect } from 'react';
import DashboardPage from '../pages/user/DashboardPage';
import { navigate } from './navigation';
import { USER_ROUTES } from './routePaths';
import MyCVPage from '../pages/user/MyCVPage';
import CVJDManagementPage from '../pages/user/CVJDManagementPage';
import QuestionsPage from '../pages/user/QuestionsPage';
import DeviceReadinessCheckPage from '../pages/user/DeviceReadinessCheckPage';
import AIInterviewRoomPage from '../pages/user/AIInterviewRoomPage';
import TechnicalInterviewResultPage from '../pages/user/TechnicalInterviewResultPage';
import CodingInterviewPage from '../pages/user/CodingInterview/CodingInterviewPage';
import ProfilePage from '../pages/user/ProfilePage';
import InterviewSetupPage from '../pages/user/InterviewSetupPage';
import InterviewModePage from '../pages/user/InterviewModePage';
import PackagesPage from '../pages/user/PackagesPage';
import PaymentResultPage from '../pages/user/PaymentResultPage';

function UserRoutes({ pathname }) {
  const isInterviewRoomRoute = pathname === USER_ROUTES.INTERVIEW_ROOM
    || pathname.startsWith(`${USER_ROUTES.INTERVIEW_ROOM}/`);
  const isCodingInterviewRoomRoute = pathname === USER_ROUTES.CODING_INTERVIEW_ROOM
    || pathname.startsWith(`${USER_ROUTES.CODING_INTERVIEW_ROOM}/`);
  const isInterviewResultRoute = pathname === USER_ROUTES.INTERVIEW_RESULT
    || pathname.startsWith(`${USER_ROUTES.INTERVIEW_RESULT}/`);
  const getRouteId = (basePath) => {
    if (!pathname.startsWith(`${basePath}/`)) return null;
    const routeId = pathname.slice(basePath.length + 1).split('/')[0];
    return routeId ? decodeURIComponent(routeId) : null;
  };
  const isUserRoot = pathname === USER_ROUTES.ROOT || pathname === `${USER_ROUTES.ROOT}/`;
  const isProfileRoute = pathname === USER_ROUTES.PROFILE;
  const isKnownRoute =
    pathname === USER_ROUTES.DASHBOARD ||
    pathname === USER_ROUTES.PACKAGES ||
    pathname === USER_ROUTES.CV ||
    pathname === USER_ROUTES.CV_DETAIL ||
    pathname === USER_ROUTES.QUESTIONS ||
    pathname === USER_ROUTES.INTERVIEW_MODE ||
    pathname === USER_ROUTES.INTERVIEW_SETUP ||
    pathname === USER_ROUTES.DEVICE_CHECK ||
    isInterviewRoomRoute ||
    isCodingInterviewRoomRoute ||
    isInterviewResultRoute ||
    pathname === USER_ROUTES.PAYMENT_RESULT;

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

  if (pathname === USER_ROUTES.PACKAGES) {
    return <PackagesPage />;
  }

  if (pathname === USER_ROUTES.PAYMENT_RESULT) {
    return <PaymentResultPage />;
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

  if (isInterviewRoomRoute) {
    return <AIInterviewRoomPage sessionId={getRouteId(USER_ROUTES.INTERVIEW_ROOM)} />;
  }
  
  if (isCodingInterviewRoomRoute) {
    return <CodingInterviewPage sessionId={getRouteId(USER_ROUTES.CODING_INTERVIEW_ROOM)} />;
  }

  if (isInterviewResultRoute) {
    return <TechnicalInterviewResultPage sessionId={getRouteId(USER_ROUTES.INTERVIEW_RESULT)} />;
  }

  return isKnownRoute ? <DashboardPage /> : null;
}

export default UserRoutes;
