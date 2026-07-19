import React from 'react';
import TechnicalQuestionTypeBadge from './TechnicalQuestionTypeBadge';
import { TechnicalQuestionType } from '../../features/technicalInterview/technicalInterview.types';

function TechnicalQuestionPanel({ question, t }) {
  if (!question) return null;
  const isSubQuestion = question.questionType === TechnicalQuestionType.CLARIFICATION
    || question.questionType === TechnicalQuestionType.FOLLOW_UP;

  return (
    <section className="technical-question-panel technical-card" aria-labelledby="technical-question-title">
      <div className="technical-question-panel__header">
        <div>
          <p className="technical-section__eyebrow">{t('room.interviewerAsks')}</p>
          <h2 id="technical-question-title">{t('room.questionTitle')}</h2>
        </div>
        <div className="technical-question-panel__badges">
          <TechnicalQuestionTypeBadge type={question.questionType} t={t} />
          {question.skill && <span className="technical-tag">{question.skill}</span>}
          {question.difficulty && <span className="technical-tag">{question.difficulty}</span>}
        </div>
      </div>
      <p className="technical-question-panel__content">{question.content}</p>
      {isSubQuestion && (
        <p className="technical-question-panel__context">
          {question.mainQuestionContent || t('room.subQuestionContext')}
        </p>
      )}
    </section>
  );
}

export default TechnicalQuestionPanel;

