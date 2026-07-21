import { useCallback, useRef, useState } from 'react';
import technicalInterviewApi from '../../services/technicalInterviewApi';

const createIdempotencyKey = (sessionId, attemptId) => {
  const randomPart = typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `technical-answer:${sessionId}:${attemptId}:${randomPart}`;
};

export default function useSubmitTechnicalAnswer(sessionId) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const submittingRef = useRef(false);
  const requestRef = useRef({ attemptId: null, idempotencyKey: null });

  const submitAnswer = useCallback(async (submission) => {
    if (submittingRef.current) return null;
    if (!sessionId || !submission?.attemptId) return null;

    if (requestRef.current.attemptId !== submission.attemptId) {
      requestRef.current = {
        attemptId: submission.attemptId,
        idempotencyKey: createIdempotencyKey(sessionId, submission.attemptId),
      };
    }

    submittingRef.current = true;
    setIsSubmitting(true);
    setError(null);

    try {
      const response = await technicalInterviewApi.submitAnswer(sessionId, submission, {
        idempotencyKey: requestRef.current.idempotencyKey,
      });
      requestRef.current = { attemptId: null, idempotencyKey: null };
      return response;
    } catch (requestError) {
      setError(requestError);
      throw requestError;
    } finally {
      submittingRef.current = false;
      setIsSubmitting(false);
    }
  }, [sessionId]);

  return { submitAnswer, isSubmitting, error };
}

