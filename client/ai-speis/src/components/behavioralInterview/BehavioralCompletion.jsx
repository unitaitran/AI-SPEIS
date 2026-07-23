import React from 'react';
import { AlertCircle, ArrowRight, CheckCircle2, LayoutDashboard, RefreshCw } from 'lucide-react';

function BehavioralCompletion({
  result,
  answeredCount,
  onContinue,
  onOverview,
  onRetryFeedback,
  feedbackRetrying,
  feedbackError,
  hasNextRound,
  t,
}) {
  const feedbackFailed = String(result?.finalFeedbackStatus || '').toUpperCase() === 'FAILED';
  return (
    <section className="behavior-completion" aria-labelledby="behavior-completion-title">
      <div className="behavior-completion__icon"><CheckCircle2 size={42} /></div>
      <span>{t('completionEyebrow')}</span>
      <h1 id="behavior-completion-title">{t('completionTitle')}</h1>
      <p>{result?.summary?.overallBehavioralAssessment
        || result?.summary?.executiveSummary
        || t('completionDescription')}</p>
      <dl>
        <div><dt>{t('answeredQuestions')}</dt><dd>{answeredCount}</dd></div>
        <div><dt>{t('performanceBand')}</dt><dd>{result?.performanceBand || t('resultReady')}</dd></div>
        <div><dt>{t('resultStatus')}</dt><dd>{t('completed')}</dd></div>
      </dl>
      {(feedbackFailed || feedbackError) ? (
        <div className="behavior-inline-error" role="alert">
          <AlertCircle size={18} />
          <span>{t('feedbackUnavailable')}</span>
          <button type="button" onClick={onRetryFeedback} disabled={feedbackRetrying}>
            <RefreshCw size={16} />
            {feedbackRetrying ? t('generatingFeedback') : t('retryFeedback')}
          </button>
        </div>
      ) : null}
      <div className="behavior-completion__actions">
        <button type="button" onClick={onOverview}>
          <LayoutDashboard size={18} />
          {t('backToOverview')}
        </button>
        {hasNextRound ? (
          <button type="button" className="behavior-completion__primary" onClick={onContinue}>
            {t('continueNextRound')}
            <ArrowRight size={18} />
          </button>
        ) : null}
      </div>
    </section>
  );
}

export default BehavioralCompletion;
