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
import CampaignInterviewResultPage from '../pages/user/CampaignInterviewResultPage';
import CodingInterviewPage from '../pages/user/CodingInterview/CodingInterviewPage';
import ProfilePage from '../pages/user/ProfilePage';
import InterviewSetupPage from '../pages/user/InterviewSetupPage';
import InterviewModePage from '../pages/user/InterviewModePage';
import PackagesPage from '../pages/user/PackagesPage';
import PaymentResultPage from '../pages/user/PaymentResultPage';
import InterviewHistoryPage from '../pages/user/InterviewHistoryPage';
import InterviewReviewPage from '../pages/user/InterviewReviewPage';
import NotificationCenterPage from '../pages/user/NotificationCenterPage';
import SingleQuestionInterviewPage from '../pages/user/SingleQuestionInterviewPage';

function UserRoutes({ pathname }) {
  const isInterviewRoomRoute = pathname === USER_ROUTES.INTERVIEW_ROOM
    || pathname.startsWith(`${USER_ROUTES.INTERVIEW_ROOM}/`);
  const isCodingInterviewRoomRoute = pathname === USER_ROUTES.CODING_INTERVIEW_ROOM
    || pathname.startsWith(`${USER_ROUTES.CODING_INTERVIEW_ROOM}/`);
  const isInterviewResultRoute = pathname === USER_ROUTES.INTERVIEW_RESULT
    || pathname.startsWith(`${USER_ROUTES.INTERVIEW_RESULT}/`);
  const isCampaignResultRoute = pathname === USER_ROUTES.CAMPAIGN_RESULT
    || pathname.startsWith(`${USER_ROUTES.CAMPAIGN_RESULT}/`);
  const isInterviewReviewRoute = pathname.startsWith(`${USER_ROUTES.INTERVIEW_REVIEW}/`)
    && pathname.endsWith('/review');
  const getRouteId = (basePath) => {
    if (!pathname.startsWith(`${basePath}/`)) return null;
    const routeId = pathname.slice(basePath.length + 1).split('/')[0];
    return routeId ? decodeURIComponent(routeId) : null;
  };
  const isUserRoot = pathname === USER_ROUTES.ROOT || pathname === `${USER_ROUTES.ROOT}/`;
  const isProfileRoute = pathname === USER_ROUTES.PROFILE;

  useEffect(() => {
    if (isUserRoot) {
      navigate(USER_ROUTES.DASHBOARD, { replace: true });
    }
  }, [isUserRoot]);

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

  if (pathname === USER_ROUTES.INTERVIEW_HISTORY) {
    return <InterviewHistoryPage />;
  }

  if (pathname === USER_ROUTES.SINGLE_QUESTION_INTERVIEW) {
    return <SingleQuestionInterviewPage />;
  }

  if (pathname === USER_ROUTES.NOTIFICATIONS) {
    return <NotificationCenterPage />;
  }

  if (isInterviewReviewRoute) {
    return <InterviewReviewPage sessionId={getRouteId(USER_ROUTES.INTERVIEW_REVIEW)} />;
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

  if (isCampaignResultRoute) {
    return <CampaignInterviewResultPage campaignId={getRouteId(USER_ROUTES.CAMPAIGN_RESULT)} />;
  }

  return <DashboardPage />;
}

export default UserRoutes;
