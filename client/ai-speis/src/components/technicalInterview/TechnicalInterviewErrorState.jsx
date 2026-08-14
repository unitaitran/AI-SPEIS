import React from 'react';
import InterviewRoomState from '../interviewRoom/InterviewRoomState';

function TechnicalInterviewErrorState({
  title,
  message,
  onRetry,
  onBack,
  onEnd,
  retryLabel,
  backLabel,
  endLabel,
}) {
  return (
    <InterviewRoomState
      variant="error"
      title={title}
      description={message}
      onRetry={onRetry}
      onBack={onBack}
      onEnd={onEnd}
      retryLabel={retryLabel}
      backLabel={backLabel}
      endLabel={endLabel}
    />
  );
}

export default TechnicalInterviewErrorState;
