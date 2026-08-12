import React from 'react';
import Badge from '../UI/Badge';

export const EvaluationScore = ({
  score = 0, // 0 - 10
  maxScore = 10,
  label = 'Điểm tổng quan',
  size = 'md', // 'sm' | 'md' | 'lg'
  className = '',
}) => {
  const normalizedScore = Number(score) || 0;
  const ratio = normalizedScore / maxScore;

  const getScoreStatus = (val) => {
    if (val >= 8) return { label: 'Xuất sắc', variant: 'success', color: 'text-success' };
    if (val >= 6.5) return { label: 'Khá', variant: 'primary', color: 'text-primary' };
    if (val >= 5) return { label: 'Trung bình', variant: 'warning', color: 'text-warning' };
    return { label: 'Cần cải thiện', variant: 'error', color: 'text-error' };
  };

  const status = getScoreStatus(normalizedScore);

  if (size === 'lg') {
    return (
      <div className={`flex flex-col items-center justify-center p-6 bg-surface rounded-xl border border-border shadow-md text-center ${className}`}>
        <span className="text-xs font-semibold text-text-secondary uppercase tracking-wider mb-2">{label}</span>
        
        {/* Radial Circle Badge */}
        <div className="relative w-28 h-28 flex items-center justify-center my-2">
          <svg className="w-full h-full transform -rotate-90" viewBox="0 0 36 36">
            <path
              className="text-surface-muted"
              strokeWidth="3"
              stroke="currentColor"
              fill="none"
              d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
            />
            <path
              className={status.color}
              strokeDasharray={`${ratio * 100}, 100`}
              strokeWidth="3"
              strokeLinecap="round"
              stroke="currentColor"
              fill="none"
              d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
            />
          </svg>
          <div className="absolute flex flex-col items-center">
            <span className="text-2xl font-extrabold text-text-primary">{normalizedScore.toFixed(1)}</span>
            <span className="text-[10px] font-semibold text-text-muted">/{maxScore}</span>
          </div>
        </div>

        <Badge variant={status.variant} size="md" className="mt-2">
          {status.label}
        </Badge>
      </div>
    );
  }

  return (
    <div className={`inline-flex items-center gap-3 px-4 py-2 bg-surface rounded-lg border border-border shadow-sm ${className}`}>
      <div className="flex flex-col">
        <span className="text-xs text-text-secondary font-medium">{label}</span>
        <div className="flex items-baseline gap-1">
          <span className={`text-lg font-bold ${status.color}`}>{normalizedScore.toFixed(1)}</span>
          <span className="text-xs text-text-muted">/{maxScore}</span>
        </div>
      </div>
      <Badge variant={status.variant} size="sm">
        {status.label}
      </Badge>
    </div>
  );
};

export default EvaluationScore;
