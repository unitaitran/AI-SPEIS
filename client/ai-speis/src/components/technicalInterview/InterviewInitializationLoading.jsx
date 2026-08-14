import React from 'react';
import InterviewRoomState from '../interviewRoom/InterviewRoomState';

function InterviewInitializationLoading({ phase = 'initializingSession', t }) {
  const isGenerating = phase === 'generatingQuestion' || phase === 'generatingNextQuestion';

  return (
    <InterviewRoomState
      isGenerating={isGenerating}
      title={isGenerating ? t('room.generatingQuestionTitle') : t('room.initializingTitle')}
      description={isGenerating
        ? t('room.generatingQuestionDescription')
        : t('room.initializingDescription')}
      hint={t('room.initializingHint')}
    />
  );
}

export default InterviewInitializationLoading;
