import React from 'react';
import { TechnicalQuestionType } from '../../features/technicalInterview/technicalInterview.types';

function TechnicalInterviewProgress({ question, current, total, t }) {
  const numericCurrent = Number(question?.mainQuestionIndex ?? current);
  const numericTotal = Number(question?.totalMainQuestions ?? total);
  const hasProgress = Number.isFinite(numericCurrent) && Number.isFinite(numericTotal) && numericTotal > 0;
  const percentage = hasProgress
    ? Math.min(100, Math.max(0, (numericCurrent / numericTotal) * 100))
    : 0;
  const subQuestionIndex = Number(question?.subQuestionIndex);
  const requiredSubQuestionCount = Number(question?.requiredSubQuestionCount);
  const hasSubProgress = Number.isFinite(subQuestionIndex)
    && subQuestionIndex > 0
    && Number.isFinite(requiredSubQuestionCount)
    && requiredSubQuestionCount > 0;
  const subProgressLabel = question?.questionType === TechnicalQuestionType.CLARIFICATION
    ? t('room.clarificationProgress', { current: numericCurrent })
    : question?.questionType === TechnicalQuestionType.FOLLOW_UP && hasSubProgress
      ? t('room.followUpProgress', {
        current: subQuestionIndex,
        total: requiredSubQuestionCount,
        main: numericCurrent,
      })
      : '';

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
      {subProgressLabel && (
        <p className="technical-progress__sub" aria-live="polite">
          {subProgressLabel}
        </p>
      )}
    </section>
  );
}

export default TechnicalInterviewProgress;

