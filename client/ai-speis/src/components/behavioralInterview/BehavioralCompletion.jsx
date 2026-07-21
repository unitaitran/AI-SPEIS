import React from 'react';
import { ArrowRight, CheckCircle2, LayoutDashboard } from 'lucide-react';

function BehavioralCompletion({ result, answeredCount, onContinue, onOverview, hasNextRound, t }) {
  return (
    <section className="behavior-completion" aria-labelledby="behavior-completion-title">
      <div className="behavior-completion__icon"><CheckCircle2 size={42} /></div>
      <span>{t('completionEyebrow')}</span>
      <h1 id="behavior-completion-title">{t('completionTitle')}</h1>
      <p>{result?.summary?.executiveSummary || t('completionDescription')}</p>
      <dl>
        <div><dt>{t('answeredQuestions')}</dt><dd>{answeredCount}</dd></div>
        <div><dt>{t('performanceBand')}</dt><dd>{result?.performanceBand || t('resultReady')}</dd></div>
        <div><dt>{t('resultStatus')}</dt><dd>{t('completed')}</dd></div>
      </dl>
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
