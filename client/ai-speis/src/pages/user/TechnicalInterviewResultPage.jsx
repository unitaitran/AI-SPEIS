import React, { useCallback, useMemo } from 'react';
import { ArrowLeft } from 'lucide-react';
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
import { USER_ROUTES } from '../../routes/routePaths';
import { getActiveInterviewContext, getInterviewSetupDraft } from '../../utils/interviewContext';
import '../../styles/user/TechnicalInterview.css';

function TechnicalInterviewResultPage({ sessionId }) {
  const activeContext = useMemo(() => getActiveInterviewContext(), []);
  const setupDraft = useMemo(() => getInterviewSetupDraft(), []);
  const resolvedSessionId = sessionId || activeContext?.activeSessionId || null;
  const interviewLanguage = (activeContext?.campaign?.language || setupDraft?.language) === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => (
    translate(key, { ...options, lng: interviewLanguage })
  ), [interviewLanguage, translate]);
  const { result, isLoading, error, reload } = useTechnicalInterviewResult(resolvedSessionId);

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
          <button type="button" className="technical-secondary-button" onClick={() => navigate(USER_ROUTES.DASHBOARD)}>
            <ArrowLeft size={18} aria-hidden="true" />{t('room.backToDashboard')}
          </button>
        </div>
        {content}
      </div>
    </UserLayout>
  );
}

export default TechnicalInterviewResultPage;
