import React from 'react';
import TechnicalRubricDimensionCard from './TechnicalRubricDimensionCard';

function TechnicalRubricBreakdown({ dimensions, t }) {
  return (
    <section className="technical-result-section technical-card" aria-labelledby="technical-rubric-title">
      <div className="technical-section__header">
        <div>
          <p className="technical-section__eyebrow">{t('result.rubricEyebrow')}</p>
          <h2 id="technical-rubric-title">{t('result.rubricDimensions')}</h2>
        </div>
      </div>
      {Array.isArray(dimensions) && dimensions.length > 0 ? (
        <div className="technical-rubric-grid">
          {dimensions.map((dimension, index) => (
            <TechnicalRubricDimensionCard
              key={dimension.rubricCode || dimension.name || index}
              dimension={dimension}
              t={t}
            />
          ))}
        </div>
      ) : (
        <p className="technical-empty-copy">{t('result.noDimensions')}</p>
      )}
    </section>
  );
}

export default TechnicalRubricBreakdown;

