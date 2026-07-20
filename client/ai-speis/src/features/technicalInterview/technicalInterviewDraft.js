const getDraftKey = (sessionId, attemptId) => (
  `technical-interview:${sessionId}:${attemptId}:draft`
);

export const readTechnicalInterviewDraft = (sessionId, attemptId) => {
  if (!sessionId || !attemptId) return '';
  try {
    return localStorage.getItem(getDraftKey(sessionId, attemptId)) || '';
  } catch {
    return '';
  }
};

export const saveTechnicalInterviewDraft = (sessionId, attemptId, transcript) => {
  if (!sessionId || !attemptId || typeof transcript !== 'string') return;
  try {
    localStorage.setItem(getDraftKey(sessionId, attemptId), transcript);
  } catch {
    // Draft persistence must never block answering or submitting.
  }
};

export const clearTechnicalInterviewDraft = (sessionId, attemptId) => {
  if (!sessionId || !attemptId) return;
  try {
    localStorage.removeItem(getDraftKey(sessionId, attemptId));
  } catch {
    // The backend response remains the source of truth if storage is unavailable.
  }
};

