import React from 'react';
import Badge from '../UI/Badge';

export const CVJDMatchScore = ({
  score = 0, // 0 - 100
  matchedSkills = [],
  missingSkills = [],
  className = '',
}) => {
  const getMatchVariant = (val) => {
    if (val >= 80) return { label: 'Rất phù hợp', variant: 'success' };
    if (val >= 60) return { label: 'Khá phù hợp', variant: 'primary' };
    return { label: 'Cần bổ sung kỹ năng', variant: 'warning' };
  };

  const status = getMatchVariant(score);

  return (
    <div className={`p-5 bg-surface rounded-xl border border-border shadow-sm flex flex-col gap-4 ${className}`}>
      <div className="flex items-center justify-between">
        <div>
          <h4 className="text-sm font-bold text-text-primary">Tỷ lệ phù hợp CV & JD</h4>
          <p className="text-xs text-text-secondary">Phân tích Fast Check bởi AI</p>
        </div>
        <Badge variant={status.variant} size="md">
          {status.label}
        </Badge>
      </div>

      {/* Match Score Display */}
      <div className="flex items-center gap-4 py-2 border-y border-border">
        <div className="text-3xl font-extrabold text-primary">{score}%</div>
        <div className="flex-1 bg-surface-muted h-3 rounded-full overflow-hidden">
          <div 
            className="bg-gradient-to-r from-primary to-secondary h-full rounded-full transition-all duration-500" 
            style={{ width: `${score}%` }}
          />
        </div>
      </div>

      {/* Matched vs Missing Skills */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
        <div>
          <span className="font-semibold text-success block mb-1.5">✓ Kỹ năng đáp ứng ({matchedSkills.length})</span>
          <div className="flex flex-wrap gap-1">
            {matchedSkills.map((sk, i) => (
              <Badge key={i} variant="success" size="sm">{sk}</Badge>
            ))}
          </div>
        </div>

        <div>
          <span className="font-semibold text-warning block mb-1.5">⚠ Kỹ năng thiếu / Cần học ({missingSkills.length})</span>
          <div className="flex flex-wrap gap-1">
            {missingSkills.map((sk, i) => (
              <Badge key={i} variant="warning" size="sm">{sk}</Badge>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default CVJDMatchScore;
