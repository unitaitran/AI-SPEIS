import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, ArrowRight, Flag, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import FeedbackModal from '../../components/feedback/FeedbackModal';
import TechnicalInterviewErrorState from '../../components/technicalInterview/TechnicalInterviewErrorState';
import TechnicalQuestionBreakdown from '../../components/technicalInterview/TechnicalQuestionBreakdown';
import TechnicalRecommendations from '../../components/technicalInterview/TechnicalRecommendations';
import TechnicalResultSummary from '../../components/technicalInterview/TechnicalResultSummary';
import TechnicalRubricBreakdown from '../../components/technicalInterview/TechnicalRubricBreakdown';
import { getTechnicalInterviewErrorKey } from '../../features/technicalInterview/technicalInterviewErrors';
import useTechnicalInterviewResult from '../../features/technicalInterview/useTechnicalInterviewResult';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { getCampaignResultPath, getInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';
import { submitEvaluationFeedback } from '../../services/aiEvaluationFeedbackApi';
import interviewSessionService from '../../services/InterviewSessionService';
import notify from '../../utils/notification';
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
  const [isFeedbackModalOpen, setIsFeedbackModalOpen] = useState(false);
  const [isSubmittingFeedback, setIsSubmittingFeedback] = useState(false);
  const autoFeedbackOpenedRef = useRef(false);
  const shouldOpenFeedbackDirectly = useMemo(() => {
    const params = new URLSearchParams(window.location.search);
    return params.get('report') === '1';
  }, []);
  const interviewLanguage = resolveInterviewLanguage(campaign?.language, setupDraft?.language);
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => (
    translate(key, { ...options, lng: interviewLanguage })
  ), [interviewLanguage, translate]);
  const {
    result,
    isLoading,
    error,
    feedbackError,
    isRetryingFeedback,
    reload,
    retryFeedback,
  } = useTechnicalInterviewResult(resolvedSessionId);
  const nextRoundSession = getNextOpenSession(campaign, resolvedSessionId);
  const campaignCompleted = campaign?.status === 'Completed';
  const canBypassResultForFeedback = Boolean(
    shouldOpenFeedbackDirectly
    && error
    && (Number(error?.status) === 409 || String(error?.code || '').toUpperCase() === 'SESSION_NOT_COMPLETED')
  );
  const hasEvaluation = Boolean(
    result
    && (
      result.technicalScore != null
      || result.summaryFeedback
      || result.questionResults?.length
      || result.recommendations?.length
    )
  );
  const evaluationId = result?.evaluationId
    ?? result?.technicalEvaluationId
    ?? result?.resultId
    ?? null;
  const feedbackQuestions = useMemo(() => (
    Array.isArray(result?.questionResults)
      ? result.questionResults.map((question, index) => ({
        id: question?.sessionQuestionId ?? question?.attemptId ?? question?.mainQuestionIndex ?? index + 1,
        label: t('feedback.questionItem', { index: question?.mainQuestionIndex || index + 1 }),
      }))
      : []
  ), [result?.questionResults, t]);

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

  const handleSubmitFeedback = async (payload) => {
    setIsSubmittingFeedback(true);
    try {
      await submitEvaluationFeedback(payload);
      setIsFeedbackModalOpen(false);
      notify.success(t('feedback.toastSuccess'));
    } catch (submitError) {
      if (Number(submitError?.status) === 404) {
        notify.warning(t('feedback.apiNotImplemented'));
      } else {
        notify.error(t('feedback.toastError'));
      }
    } finally {
      setIsSubmittingFeedback(false);
    }
  };

  useEffect(() => {
    if (!shouldOpenFeedbackDirectly) return;
    if (isLoading) return;
    if (!hasEvaluation && !canBypassResultForFeedback) return;
    if (error && !canBypassResultForFeedback) return;
    if (autoFeedbackOpenedRef.current) return;

    autoFeedbackOpenedRef.current = true;
    setIsFeedbackModalOpen(true);
  }, [canBypassResultForFeedback, error, hasEvaluation, isLoading, shouldOpenFeedbackDirectly]);

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
  } else if (error && !canBypassResultForFeedback) {
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
  } else if (canBypassResultForFeedback) {
    content = (
      <div className="technical-result-stack">
        <div className="technical-inline-error" role="status">
          <span>{t('feedback.emptyResult')}</span>
        </div>
        <div className="technical-feedback-report">
          <button
            type="button"
            className="technical-report-button"
            onClick={() => setIsFeedbackModalOpen(true)}
            aria-label={t('feedback.reportButton')}
          >
            <Flag size={16} aria-hidden="true" />
            {t('feedback.reportButton')}
          </button>
        </div>
      </div>
    );
  } else if (result) {
    const feedbackFailed = String(result.finalFeedbackStatus || '').toUpperCase() === 'FAILED';
    content = (
      <div className="technical-result-stack">
        {(feedbackFailed || feedbackError) ? (
          <div className="technical-inline-error" role="alert">
            <span>{t('result.feedbackUnavailable')}</span>
            <button
              type="button"
              className="technical-secondary-button"
              onClick={() => retryFeedback().catch(() => undefined)}
              disabled={isRetryingFeedback}
            >
              <RefreshCw size={16} />
              {isRetryingFeedback ? t('result.generatingFeedback') : t('result.retryFeedback')}
            </button>
          </div>
        ) : null}
        <TechnicalResultSummary result={result} t={t} language={interviewLanguage} />
        {result.dimensionResults?.length > 0 && (
          <TechnicalRubricBreakdown dimensions={result.dimensionResults} t={t} />
        )}
        <TechnicalQuestionBreakdown questions={result.questionResults} t={t} />
        <TechnicalRecommendations recommendations={result.recommendations} t={t} />
        {hasEvaluation ? (
          <div className="technical-feedback-report">
            <button
              type="button"
              className="technical-report-button"
              onClick={() => setIsFeedbackModalOpen(true)}
              aria-label={t('feedback.reportButton')}
            >
              <Flag size={16} aria-hidden="true" />
              {t('feedback.reportButton')}
            </button>
          </div>
        ) : null}
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
            ) : campaignCompleted ? (
              <button
                type="button"
                className="technical-primary-button"
                onClick={() => navigate(getCampaignResultPath(campaign.interviewCampaignId))}
              >
                {t('result.viewCampaignResult', { defaultValue: 'View final campaign result' })}<ArrowRight size={18} />
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
        <FeedbackModal
          isOpen={isFeedbackModalOpen}
          onClose={() => {
            if (!isSubmittingFeedback) setIsFeedbackModalOpen(false);
          }}
          onSubmit={handleSubmitFeedback}
          isSubmitting={isSubmittingFeedback}
          questions={feedbackQuestions}
          interviewSessionId={resolvedSessionId}
          evaluationId={evaluationId}
          t={t}
        />
      </div>
    </UserLayout>
  );
}

export default TechnicalInterviewResultPage;
