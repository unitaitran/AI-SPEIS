import React from 'react';

function TechnicalRecommendations({ recommendations, t }) {
  if (!Array.isArray(recommendations) || recommendations.length === 0) return null;
  return (
    <section className="technical-result-section technical-card" aria-labelledby="technical-recommendations-title">
      <div className="technical-section__header">
        <div>
          <p className="technical-section__eyebrow">{t('result.nextStepsEyebrow')}</p>
          <h2 id="technical-recommendations-title">{t('result.recommendations')}</h2>
        </div>
      </div>
      <ul className="technical-recommendations">
        {recommendations.map((recommendation, index) => (
          <li key={`${index}-${recommendation}`}>{recommendation}</li>
        ))}
      </ul>
    </section>
  );
}

export default TechnicalRecommendations;
