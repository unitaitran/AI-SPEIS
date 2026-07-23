import { useCallback, useEffect, useRef, useState } from 'react';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import { normalizeTechnicalInterviewResult } from './technicalInterviewResult';

export default function useTechnicalInterviewResult(sessionId) {
  const [result, setResult] = useState(null);
  const [isLoading, setIsLoading] = useState(Boolean(sessionId));
  const [error, setError] = useState(null);
  const [feedbackError, setFeedbackError] = useState(null);
  const [isRetryingFeedback, setIsRetryingFeedback] = useState(false);
  const requestIdRef = useRef(0);
  const feedbackInFlightRef = useRef(false);

  const load = useCallback(async () => {
    if (!sessionId) {
      setIsLoading(false);
      return;
    }
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setIsLoading(true);
    setError(null);
    try {
      const response = await technicalInterviewApi.getResult(sessionId);
      if (requestIdRef.current === requestId) {
        setResult(normalizeTechnicalInterviewResult(response?.result || response));
      }
    } catch (requestError) {
      if (requestIdRef.current === requestId) setError(requestError);
    } finally {
      if (requestIdRef.current === requestId) setIsLoading(false);
    }
  }, [sessionId]);

  useEffect(() => {
    load();
    return () => {
      requestIdRef.current += 1;
    };
  }, [load]);

  const retryFeedback = useCallback(async () => {
    if (!sessionId || feedbackInFlightRef.current) return null;
    feedbackInFlightRef.current = true;
    setIsRetryingFeedback(true);
    setFeedbackError(null);
    try {
      const response = await technicalInterviewApi.generateFeedback(sessionId);
      const normalized = normalizeTechnicalInterviewResult(response?.result || response);
      setResult(normalized);
      return normalized;
    } catch (requestError) {
      setFeedbackError(requestError);
      throw requestError;
    } finally {
      feedbackInFlightRef.current = false;
      setIsRetryingFeedback(false);
    }
  }, [sessionId]);

  return {
    result,
    isLoading,
    error,
    feedbackError,
    isRetryingFeedback,
    reload: load,
    retryFeedback,
  };
}
