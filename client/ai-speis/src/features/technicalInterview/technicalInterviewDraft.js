const getDraftKey = (sessionId, attemptId) => (
  `technical-interview:${sessionId}:${attemptId}:draft`
);

const getDraftPrefix = (sessionId) => `technical-interview:${sessionId}:`;
const DRAFT_SUFFIX = ':draft';

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

export const readTechnicalInterviewSessionDraft = (sessionId) => {
  if (!sessionId) return null;
  const prefix = getDraftPrefix(sessionId);
  let latestDraft = null;
  try {
    for (let index = 0; index < localStorage.length; index += 1) {
      const key = localStorage.key(index);
      if (!key?.startsWith(prefix) || !key.endsWith(DRAFT_SUFFIX)) continue;
      const transcript = localStorage.getItem(key) || '';
      if (!transcript.trim()) continue;
      latestDraft = {
        attemptId: key.slice(prefix.length, -DRAFT_SUFFIX.length),
        transcript,
      };
    }
  } catch {
    return null;
  }
  return latestDraft;
};

export const clearStaleTechnicalInterviewDrafts = (sessionId, activeAttemptId) => {
  if (!sessionId) return;
  const prefix = getDraftPrefix(sessionId);
  const activeKey = activeAttemptId ? getDraftKey(sessionId, activeAttemptId) : null;
  try {
    const staleKeys = [];
    for (let index = 0; index < localStorage.length; index += 1) {
      const key = localStorage.key(index);
      if (key?.startsWith(prefix) && key.endsWith(DRAFT_SUFFIX) && key !== activeKey) {
        staleKeys.push(key);
      }
    }
    staleKeys.forEach((key) => localStorage.removeItem(key));
  } catch {
    // Stale draft cleanup is best-effort and must not interrupt the interview.
  }
};

