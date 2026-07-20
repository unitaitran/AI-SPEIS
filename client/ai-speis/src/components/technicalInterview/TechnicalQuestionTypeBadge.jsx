import React from 'react';
import { TechnicalQuestionType } from '../../features/technicalInterview/technicalInterview.types';

const BADGE_CLASS = {
  [TechnicalQuestionType.MAIN]: 'main',
  [TechnicalQuestionType.CLARIFICATION]: 'clarification',
  [TechnicalQuestionType.FOLLOW_UP]: 'follow-up',
};

function TechnicalQuestionTypeBadge({ type, t }) {
  const normalizedType = BADGE_CLASS[type] ? type : TechnicalQuestionType.MAIN;
  return (
    <span className={`technical-question-type technical-question-type--${BADGE_CLASS[normalizedType]}`}>
      {t(`room.questionTypes.${normalizedType}`)}
    </span>
  );
}

export default TechnicalQuestionTypeBadge;

