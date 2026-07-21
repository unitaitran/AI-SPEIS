import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ArrowLeft, ArrowRight, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import TechnicalInterviewErrorState from '../../components/technicalInterview/TechnicalInterviewErrorState';
import TechnicalQuestionBreakdown from '../../components/technicalInterview/TechnicalQuestionBreakdown';
import TechnicalRecommendations from '../../components/technicalInterview/TechnicalRecommendations';
import TechnicalResultSummary from '../../components/technicalInterview/TechnicalResultSummary';
import TechnicalRubricBreakdown from '../../components/technicalInterview/TechnicalRubricBreakdown';
import TechnicalSkillBreakdown from '../../components/technicalInterview/TechnicalSkillBreakdown';
import { getTechnicalInterviewErrorKey } from '../../features/technicalInterview/technicalInterviewErrors';
import useTechnicalInterviewResult from '../../features/technicalInterview/useTechnicalInterviewResult';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { getInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  getNextOpenSession,
  saveActiveInterviewContext,
} from '../../utils/interviewContext';
import '../../styles/user/TechnicalInterview.css';

function TechnicalInterviewResultPage({ sessionId }) {
  const activeContext = useMemo(() => getActiveInterviewContext(), []);
  const setupDraft = useMemo(() => getInterviewSetupDraft(), []);
  const resolvedSessionId = sessionId || activeContext?.activeSessionId || null;
  const [campaign, setCampaign] = useState(activeContext?.campaign || null);
  const [campaignError, setCampaignError] = useState('');
  const interviewLanguage = (campaign?.language || setupDraft?.language) === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => (
    translate(key, { ...options, lng: interviewLanguage })
  ), [interviewLanguage, translate]);
  const { result, isLoading, error, reload } = useTechnicalInterviewResult(resolvedSessionId);
  const nextRoundSession = getNextOpenSession(campaign, resolvedSessionId);

  const syncCampaign = useCallback(async () => {
    if (!resolvedSessionId) return;
    setCampaignError('');
    try {
      const session = await interviewSessionService.getSession(resolvedSessionId);
      const latestCampaign = await interviewSessionService.getCampaign(session.interviewCampaignId);
      const nextSession = getNextOpenSession(latestCampaign, resolvedSessionId);
      saveActiveInterviewContext({
        campaign: latestCampaign,
        activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
        configurationKey: activeContext?.configurationKey || null,
      });
      setCampaign(latestCampaign);
    } catch (syncError) {
      setCampaignError(syncError.message || t('result.campaignSyncFailed', {
        defaultValue: 'Could not load the next interview round.',
      }));
    }
  }, [activeContext?.configurationKey, resolvedSessionId, t]);

  useEffect(() => {
    if (result) syncCampaign();
  }, [result, syncCampaign]);

  const handleContinue = () => {
    if (!nextRoundSession) return;
    navigate(nextRoundSession.status === 'Active'
      ? getInterviewRoomPath(nextRoundSession.interviewSessionId)
      : USER_ROUTES.DEVICE_CHECK);
  };

  let content;
  if (!resolvedSessionId) {
    content = (
      <TechnicalInterviewErrorState
        title={t('result.loadFailedTitle')}
        message={t('room.sessionIdMissing')}
        onBack={() => navigate(USER_ROUTES.DASHBOARD)}
        backLabel={t('room.backToDashboard')}
      />
    );
  } else if (isLoading) {
    content = (
      <div className="technical-loading" aria-label={t('common.loading')}>
        <div className="technical-skeleton technical-skeleton--large" />
        <div className="technical-skeleton" />
        <div className="technical-skeleton technical-skeleton--large" />
      </div>
    );
  } else if (error) {
    content = (
      <TechnicalInterviewErrorState
        title={t('result.loadFailedTitle')}
        message={t(getTechnicalInterviewErrorKey(error), {
          defaultValue: t('errors.UNKNOWN_ERROR'),
        })}
        onRetry={reload}
        onBack={() => navigate(USER_ROUTES.DASHBOARD)}
        retryLabel={t('common.retry')}
        backLabel={t('room.backToDashboard')}
      />
    );
  } else if (result) {
    content = (
      <div className="technical-result-stack">
        <TechnicalResultSummary result={result} t={t} />
        {result.dimensionResults?.length > 0 && (
          <TechnicalRubricBreakdown dimensions={result.dimensionResults} t={t} />
        )}
        <TechnicalSkillBreakdown skills={result.skillResults} t={t} />
        <TechnicalQuestionBreakdown questions={result.questionResults} t={t} />
        <TechnicalRecommendations recommendations={result.recommendations} t={t} />
      </div>
    );
  } else {
    content = (
      <TechnicalInterviewErrorState
        title={t('result.loadFailedTitle')}
        message={t('result.noResult')}
        onRetry={reload}
        retryLabel={t('common.retry')}
      />
    );
  }

  return (
    <UserLayout>
      <div className="technical-page technical-page--result animate-pageEntrance" lang={interviewLanguage}>
        <div className="technical-result-heading">
          <div>
            <p className="technical-room-header__eyebrow">AI-SPEIS</p>
            <h1>{t('result.title')}</h1>
            <p>{t('result.subtitle')}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="technical-secondary-button" onClick={() => navigate(USER_ROUTES.DASHBOARD)}>
              <ArrowLeft size={18} aria-hidden="true" />{t('room.backToDashboard')}
            </button>
            {nextRoundSession ? (
              <button type="button" className="technical-primary-button" onClick={handleContinue}>
                {t('result.continueNextRound', { defaultValue: 'Continue to next round' })}<ArrowRight size={18} />
              </button>
            ) : null}
          </div>
        </div>
        {campaignError ? (
          <div className="technical-inline-error" role="alert">
            <span>{campaignError}</span>
            <button type="button" className="technical-secondary-button" onClick={syncCampaign}>
              <RefreshCw size={16} />{t('common.retry')}
            </button>
          </div>
        ) : null}
        {content}
      </div>
    </UserLayout>
  );
}

export default TechnicalInterviewResultPage;
