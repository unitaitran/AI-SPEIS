import { useCallback, useEffect, useState } from 'react';

export const TechnicalTranscriptRole = Object.freeze({
  INTERVIEWER: 'INTERVIEWER',
  CANDIDATE: 'CANDIDATE',
});

export const TechnicalTranscriptItemStatus = Object.freeze({
  DRAFT: 'DRAFT',
  PROCESSING: 'PROCESSING',
  FINAL: 'FINAL',
  ERROR: 'ERROR',
});

const getStorageKey = (sessionId) => `technical-interview:${sessionId}:transcript`;

const isValidItem = (item) => (
  item
  && typeof item.id === 'string'
  && typeof item.attemptId === 'string'
  && Object.values(TechnicalTranscriptRole).includes(item.role)
  && typeof item.content === 'string'
);

export const readTechnicalInterviewTranscript = (sessionId) => {
  if (!sessionId) return [];
  try {
    const parsed = JSON.parse(localStorage.getItem(getStorageKey(sessionId)) || '[]');
    return Array.isArray(parsed) ? parsed.filter(isValidItem) : [];
  } catch {
    return [];
  }
};

const persistTechnicalInterviewTranscript = (sessionId, items) => {
  if (!sessionId) return;
  try {
    localStorage.setItem(getStorageKey(sessionId), JSON.stringify(items));
  } catch {
    // Transcript persistence is best-effort and must never block the interview.
  }
};

export const upsertTechnicalTranscriptItem = (items, item) => {
  const existingIndex = items.findIndex((current) => current.id === item.id);
  if (existingIndex < 0) return [...items, item];
  return items.map((current, index) => (
    index === existingIndex ? { ...current, ...item } : current
  ));
};

export default function useTechnicalInterviewTranscript(sessionId) {
  const [items, setItems] = useState(() => readTechnicalInterviewTranscript(sessionId));

  useEffect(() => {
    setItems(readTechnicalInterviewTranscript(sessionId));
  }, [sessionId]);

  const updateItems = useCallback((updater) => {
    setItems((current) => {
      const next = updater(current);
      persistTechnicalInterviewTranscript(sessionId, next);
      return next;
    });
  }, [sessionId]);

  const syncQuestion = useCallback((question) => {
    const attemptId = question?.attemptId == null ? '' : String(question.attemptId);
    const content = question?.content?.trim();
    if (!attemptId || !content) return;
    updateItems((current) => upsertTechnicalTranscriptItem(current, {
      id: `${attemptId}:question`,
      attemptId,
      role: TechnicalTranscriptRole.INTERVIEWER,
      content,
      status: TechnicalTranscriptItemStatus.FINAL,
      questionType: question.questionType,
      mainQuestionIndex: question.mainQuestionIndex,
    }));
  }, [updateItems]);

  const syncCandidate = useCallback((attemptIdValue, contentValue, status) => {
    const attemptId = attemptIdValue == null ? '' : String(attemptIdValue);
    const content = typeof contentValue === 'string' ? contentValue.trim() : '';
    if (!attemptId || !content) return;
    updateItems((current) => upsertTechnicalTranscriptItem(current, {
      id: `${attemptId}:answer`,
      attemptId,
      role: TechnicalTranscriptRole.CANDIDATE,
      content,
      status: status || TechnicalTranscriptItemStatus.DRAFT,
    }));
  }, [updateItems]);

  const markCandidateFinal = useCallback((attemptId, content) => syncCandidate(
    attemptId,
    content,
    TechnicalTranscriptItemStatus.FINAL,
  ), [syncCandidate]);

  const markCandidateProcessing = useCallback((attemptId, content) => syncCandidate(
    attemptId,
    content,
    TechnicalTranscriptItemStatus.PROCESSING,
  ), [syncCandidate]);

  const markCandidateError = useCallback((attemptId, content) => syncCandidate(
    attemptId,
    content,
    TechnicalTranscriptItemStatus.ERROR,
  ), [syncCandidate]);

  return {
    items,
    syncQuestion,
    syncCandidate,
    markCandidateFinal,
    markCandidateProcessing,
    markCandidateError,
  };
}
