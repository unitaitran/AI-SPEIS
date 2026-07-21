import React, { useEffect, useRef } from 'react';
import TechnicalQuestionTypeBadge from './TechnicalQuestionTypeBadge';
import { TechnicalQuestionType } from '../../features/technicalInterview/technicalInterview.types';
import QuestionAudioControls from './QuestionAudioControls';

function TechnicalQuestionPanel({ question, audio, audioDisabled = false, stageMode = false, t }) {
  const headingRef = useRef(null);

  useEffect(() => {
    if (!question?.attemptId) return;
    headingRef.current?.focus({ preventScroll: true });
  }, [question?.attemptId]);

  if (!question) return null;
  const isSubQuestion = question.questionType === TechnicalQuestionType.CLARIFICATION
    || question.questionType === TechnicalQuestionType.FOLLOW_UP;

  return (
    <section
      className={`technical-question-panel technical-card${stageMode ? ' technical-question-panel--stage' : ''}`}
      aria-labelledby="technical-question-title"
      aria-live="polite"
    >
      <div className="technical-question-panel__header">
        <div>
          <p className="technical-section__eyebrow">{t('room.interviewerAsks')}</p>
          <h2
            id="technical-question-title"
            ref={headingRef}
            tabIndex={-1}
            className={stageMode ? 'technical-visually-hidden' : undefined}
          >
            {t('room.questionTitle')}
          </h2>
        </div>
        <div className="technical-question-panel__badges">
          <TechnicalQuestionTypeBadge type={question.questionType} t={t} />
          {question.skill && <span className="technical-tag">{question.skill}</span>}
          {question.difficulty && <span className="technical-tag">{question.difficulty}</span>}
        </div>
      </div>
      <p className="technical-question-panel__content">{question.content}</p>
      {audio && <QuestionAudioControls audio={audio} disabled={audioDisabled} t={t} />}
      {isSubQuestion && (
        <p className="technical-question-panel__context">
          {question.questionType === TechnicalQuestionType.CLARIFICATION
            ? t('room.clarificationContext', { current: question.mainQuestionIndex })
            : t('room.followUpContext', { current: question.mainQuestionIndex })}
        </p>
      )}
    </section>
  );
}

export default TechnicalQuestionPanel;

