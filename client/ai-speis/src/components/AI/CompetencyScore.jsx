import React from 'react';
import ProgressBar from '../UI/ProgressBar';

export const CompetencyScore = ({
  title,
  score = 0,
  maxScore = 10,
  description,
  variant = 'primary',
  className = '',
}) => {
  const percentage = (score / maxScore) * 100;

  return (
    <div className={`flex flex-col gap-2 p-4 bg-surface rounded-lg border border-border ${className}`}>
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-bold text-text-primary">{title}</h4>
        <span className="text-sm font-extrabold text-primary">
          {score} <span className="text-xs font-normal text-text-muted">/{maxScore}</span>
        </span>
      </div>

      <ProgressBar value={score} max={maxScore} variant={variant} size="md" />

      {description && (
        <p className="text-xs text-text-secondary mt-1 leading-relaxed">{description}</p>
      )}
    </div>
  );
};

export default CompetencyScore;
