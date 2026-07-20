import React from 'react';

function TechnicalInterviewProgress({ current, total, t }) {
  const numericCurrent = Number(current);
  const numericTotal = Number(total);
  const hasProgress = Number.isFinite(numericCurrent) && Number.isFinite(numericTotal) && numericTotal > 0;
  const percentage = hasProgress
    ? Math.min(100, Math.max(0, (numericCurrent / numericTotal) * 100))
    : 0;

  return (
    <section className="technical-progress technical-card" aria-label={t('room.progressAria')}>
      <div className="technical-progress__copy">
        <strong>
          {hasProgress
            ? t('room.questionProgress', { current: numericCurrent, total: numericTotal })
            : t('room.questionProgressUnknown')}
        </strong>
        <span>{t('room.mainQuestionsOnly')}</span>
      </div>
      <div
        className="technical-progress__track"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={hasProgress ? numericTotal : undefined}
        aria-valuenow={hasProgress ? numericCurrent : undefined}
      >
        <div className="technical-progress__fill" style={{ width: `${percentage}%` }} />
      </div>
    </section>
  );
}

export default TechnicalInterviewProgress;

