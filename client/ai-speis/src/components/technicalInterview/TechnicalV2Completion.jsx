import React from 'react';
import { ArrowRight, CheckCircle2, LayoutDashboard } from 'lucide-react';

function TechnicalV2Completion({
  result,
  answeredCount,
  hasNextRound,
  onContinue,
  onOverview,
  feedbackError,
  feedbackRetrying,
  onRetryFeedback,
  t,
}) {
  const summary = result?.summary || {};
  const questions = Array.isArray(result?.mainQuestions) ? result.mainQuestions : [];
  const performanceBand = result?.performanceBand
    ? t(`result.performanceBand.${result.performanceBand}`, { defaultValue: result.performanceBand })
    : t('result.resultReady');

  return (
    <section className="behavior-completion" aria-labelledby="technical-v2-completion-title">
      <div className="behavior-completion__icon"><CheckCircle2 size={42} /></div>
      <span>{t('completionEyebrow')}</span>
      <h1 id="technical-v2-completion-title">{t('completionTitle')}</h1>
      <p>{summary.overallTechnicalAssessment || summary.executiveSummary || t('completionDescription')}</p>
      <dl>
        <div><dt>{t('answeredQuestions')}</dt><dd>{answeredCount || questions.length}</dd></div>
        <div><dt>{t('overallScore')}</dt><dd>{Number(result?.overallScore || 0).toFixed(2)}/10</dd></div>
        <div><dt>{t('performanceBand')}</dt><dd>{performanceBand}</dd></div>
      </dl>
      {summary.recommendationsForImprovement?.length ? (
        <div className="technical-v2-completion__recommendations">
          <strong>{t('recommendations')}</strong>
          <ul>
            {summary.recommendationsForImprovement.slice(0, 3).map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}
          </ul>
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

export default TechnicalV2Completion;
