import React from 'react';
import { getScorePercentage } from '../../features/technicalInterview/technicalInterviewResult';
import TechnicalResultFeedbackList from './TechnicalResultFeedbackList';

function TechnicalRubricDimensionCard({ dimension, t }) {
  const percentage = getScorePercentage(dimension.score, dimension.maxScore);
  return (
    <article className="technical-dimension-card">
      <div className="technical-dimension-card__header">
        <div>
          <h3>{dimension.name || dimension.rubricCode}</h3>
          {dimension.description && <p className="technical-dimension-card__description">{dimension.description}</p>}
        </div>
        <div className="technical-dimension-card__metrics">
          {dimension.weight != null && (
            <span className="technical-tag">{t('result.weight', { weight: dimension.weight })}</span>
          )}
          <span className="technical-score-inline">
            {dimension.score ?? t('result.notAvailable')}
            {dimension.maxScore != null && <small> / {dimension.maxScore}</small>}
          </span>
        </div>
      </div>
      {percentage != null && (
        <div
          className="technical-score-bar"
          role="progressbar"
          aria-label={t('result.dimensionScoreAlternative', {
            name: dimension.name || dimension.rubricCode,
            score: dimension.score,
            maxScore: dimension.maxScore,
          })}
          aria-valuemin={0}
          aria-valuemax={dimension.maxScore}
          aria-valuenow={dimension.score}
        >
          <div className="technical-score-bar__fill" style={{ width: `${percentage}%` }} />
        </div>
      )}
      {(dimension.level || dimension.levelDescription) && (
        <p className="technical-dimension-card__level">
          {dimension.level && <strong>{dimension.level}</strong>}
          {dimension.level && dimension.levelDescription && ' — '}
          {dimension.levelDescription}
        </p>
      )}
      <div className="technical-feedback-columns">
        <TechnicalResultFeedbackList title={t('result.strengths')} items={dimension.strengths} />
        <TechnicalResultFeedbackList title={t('result.missingEvidence')} items={dimension.missingEvidence} />
        <TechnicalResultFeedbackList title={t('result.suggestions')} items={dimension.improvementSuggestions} />
      </div>
    </article>
  );
}

export default TechnicalRubricDimensionCard;
