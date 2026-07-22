import React from 'react';
import { Award, CheckCircle2, XCircle } from 'lucide-react';
import { getPerformanceBandLabel } from '../../features/campaignResult/campaignResult';
import { getScorePercentage } from '../../features/technicalInterview/technicalInterviewResult';
import TechnicalResultFeedbackList from './TechnicalResultFeedbackList';

function TechnicalResultSummary({ result, t, language = 'vi' }) {
  const percentage = getScorePercentage(result?.technicalScore, result?.maxScore);
  const hasScore = result?.technicalScore != null;
  const hasPassStatus = typeof result?.passed === 'boolean';

  return (
    <section className="technical-result-summary technical-card" aria-labelledby="technical-result-summary-title">
      <div className="technical-score-card">
        <span className="technical-score-card__label" id="technical-result-summary-title">
          {t('result.technicalScore')}
        </span>
        <div className="technical-score-card__value">
          {hasScore ? result.technicalScore : t('result.notAvailable')}
          {result?.maxScore != null && <small> / {result.maxScore}</small>}
        </div>
        {percentage != null && (
          <>
            <div
              className="technical-score-bar"
              role="progressbar"
              aria-label={t('result.scoreAlternative', {
                score: result.technicalScore,
                maxScore: result.maxScore,
              })}
              aria-valuemin={0}
              aria-valuemax={result.maxScore}
              aria-valuenow={result.technicalScore}
            >
              <div className="technical-score-bar__fill" style={{ width: `${percentage}%` }} />
            </div>
            <p className="technical-score-card__text-alternative">
              {t('result.scoreAlternative', { score: result.technicalScore, maxScore: result.maxScore })}
            </p>
          </>
        )}
      </div>
      <div className="technical-summary-details">
        <div className="technical-summary-badges">
          {result?.performanceBand && (
            <span className="technical-result-badge">
              <Award size={17} aria-hidden="true" />
              {getPerformanceBandLabel(result.performanceBand, language)}
            </span>
          )}
          {hasPassStatus && (
            <span className={`technical-result-badge technical-result-badge--${result.passed ? 'passed' : 'failed'}`}>
              {result.passed
                ? <CheckCircle2 size={17} aria-hidden="true" />
                : <XCircle size={17} aria-hidden="true" />}
              {result.passed ? t('result.passed') : t('result.failed')}
            </span>
          )}
          {result?.rubricVersion && (
            <span className="technical-result-badge">
              {t('result.rubricVersion', { version: result.rubricVersion })}
            </span>
          )}
        </div>
        {result?.summaryFeedback ? (
          <p className="technical-summary-feedback">{result.summaryFeedback}</p>
        ) : (
          <p className="technical-empty-copy">{t('result.noSummary')}</p>
        )}
        <div className="technical-feedback-columns">
          <TechnicalResultFeedbackList title={t('result.strengths')} items={result.summaryStrengths || result.strengths} />
          <TechnicalResultFeedbackList title={t('result.areasForImprovement')} items={result.areasForImprovement} />
        </div>
      </div>
    </section>
  );
}

export default TechnicalResultSummary;

