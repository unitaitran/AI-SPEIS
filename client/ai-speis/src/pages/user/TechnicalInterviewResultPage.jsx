import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ArrowLeft, ArrowRight, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import TechnicalInterviewErrorState from '../../components/technicalInterview/TechnicalInterviewErrorState';
import TechnicalV2ResultView from '../../components/technicalInterview/TechnicalV2ResultView';
import useTechnicalInterviewResult from '../../features/technicalInterview/useTechnicalInterviewResult';
import { getTechnicalV2ErrorKey } from '../../features/technicalInterview/technicalV2InterviewErrors';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { getCampaignResultPath, getCodingInterviewRoomPath, getInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  getNextOpenSession,
  resolveInterviewLanguage,
  saveActiveInterviewContext,
} from '../../utils/interviewContext';
import '../../styles/user/TechnicalInterview.css';

function TechnicalInterviewResultPage({ sessionId }) {
  const activeContext = useMemo(() => getActiveInterviewContext(), []);
  const setupDraft = useMemo(() => getInterviewSetupDraft(), []);
  const resolvedSessionId = sessionId || activeContext?.activeSessionId || null;
  const [campaign, setCampaign] = useState(activeContext?.campaign || null);
  const [campaignError, setCampaignError] = useState('');
  const interviewLanguage = resolveInterviewLanguage(campaign?.language, setupDraft?.language);
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => translate(`technicalRoom.${key}`, {
    ...options,
    lng: interviewLanguage,
    defaultValue: options.defaultValue || key,
  }), [interviewLanguage, translate]);
  const {
    result,
    isLoading,
    error,
    reload,
  } = useTechnicalInterviewResult(resolvedSessionId);
  const nextRoundSession = getNextOpenSession(campaign, resolvedSessionId);
  const campaignCompleted = campaign?.status === 'Completed';

  const syncCampaign = useCallback(async () => {
    if (!resolvedSessionId) return;
    setCampaignError('');
    try {
      const session = await interviewSessionService.getSession(resolvedSessionId);
      if (!session?.interviewCampaignId) return;
      const latestCampaign = await interviewSessionService.getCampaign(session.interviewCampaignId);
      const nextSession = getNextOpenSession(latestCampaign, resolvedSessionId);
      saveActiveInterviewContext({
        campaign: latestCampaign,
        activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
        configurationKey: activeContext?.configurationKey || null,
      });
      setCampaign(latestCampaign);
    } catch (syncError) {
      setCampaignError(syncError.message || t('result.campaignSyncFailed'));
    }
  }, [activeContext?.configurationKey, resolvedSessionId, t]);

  useEffect(() => {
    if (result) syncCampaign();
  }, [result, syncCampaign]);

  const handleContinue = () => {
    if (!nextRoundSession) return;
    const isCoding = nextRoundSession.interviewRoundType === 'Coding' || nextRoundSession.interviewRoundType === 'Code';
    navigate(isCoding
      ? getCodingInterviewRoomPath(nextRoundSession.interviewSessionId)
      : getInterviewRoomPath(nextRoundSession.interviewSessionId));
  };

  let content;
  if (!resolvedSessionId) {
    content = <TechnicalInterviewErrorState title={t('result.loadFailedTitle')} message={t('sessionIdMissing')} onBack={() => navigate(USER_ROUTES.DASHBOARD)} backLabel={t('backToDashboard')} />;
  } else if (isLoading) {
    content = <div className="technical-loading" aria-label={t('loading')}><div className="technical-skeleton technical-skeleton--large" /><div className="technical-skeleton" /><div className="technical-skeleton technical-skeleton--large" /></div>;
  } else if (error) {
    content = <TechnicalInterviewErrorState title={t('result.loadFailedTitle')} message={t(getTechnicalV2ErrorKey(error), { defaultValue: t('error.UNKNOWN_ERROR') })} onRetry={reload} onBack={() => navigate(USER_ROUTES.DASHBOARD)} retryLabel={t('retry')} backLabel={t('backToDashboard')} />;
  } else if (result) {
    content = (
      <>

        <TechnicalV2ResultView result={result} t={t} />
      </>
    );
  } else {
    content = <TechnicalInterviewErrorState title={t('result.loadFailedTitle')} message={t('result.noResult')} onRetry={reload} retryLabel={t('retry')} />;
  }

  return (
    <UserLayout>
      <div className="technical-page technical-page--result animate-pageEntrance" lang={interviewLanguage}>
        <div className="technical-result-heading">
          <div><p className="technical-room-header__eyebrow">AI-SPEIS</p><h1>{t('result.title')}</h1><p>{t('result.subtitle')}</p></div>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="technical-secondary-button" onClick={() => navigate(USER_ROUTES.DASHBOARD)}><ArrowLeft size={18} />{t('backToDashboard')}</button>
            {nextRoundSession ? <button type="button" className="technical-primary-button" onClick={handleContinue}>{t('continueNextRound')}<ArrowRight size={18} /></button> : null}
            {!nextRoundSession && campaignCompleted ? <button type="button" className="technical-primary-button" onClick={() => navigate(getCampaignResultPath(campaign.interviewCampaignId))}>{t('result.viewCampaignResult')}<ArrowRight size={18} /></button> : null}
          </div>
        </div>
        {campaignError ? <div className="technical-inline-error" role="alert"><span>{campaignError}</span><button type="button" className="technical-secondary-button" onClick={syncCampaign}><RefreshCw size={16} />{t('retry')}</button></div> : null}
        {content}
      </div>
    </UserLayout>
  );
}

export default TechnicalInterviewResultPage;
