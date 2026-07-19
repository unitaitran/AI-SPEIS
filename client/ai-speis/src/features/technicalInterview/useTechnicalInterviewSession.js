import { useCallback, useEffect, useRef, useState } from 'react';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import { TechnicalSessionStatus } from './technicalInterview.types';

export const getTechnicalSessionStatus = (session) => (
  session?.sessionStatus || session?.status || null
);

const canHaveCurrentQuestion = (status) => (
  status === TechnicalSessionStatus.QUESTION_READY
  || status === TechnicalSessionStatus.ANSWERING
  || status === TechnicalSessionStatus.EVALUATING
);

export default function useTechnicalInterviewSession(sessionId) {
  const [session, setSession] = useState(null);
  const [currentQuestion, setCurrentQuestion] = useState(null);
  const [isLoading, setIsLoading] = useState(Boolean(sessionId));
  const [error, setError] = useState(null);
  const requestIdRef = useRef(0);

  const load = useCallback(async () => {
    if (!sessionId) {
      setIsLoading(false);
      return null;
    }

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setIsLoading(true);
    setError(null);

    try {
      const sessionResponse = await technicalInterviewApi.getSession(sessionId);
      if (requestIdRef.current !== requestId) return null;
      const nextSession = sessionResponse?.session || sessionResponse;
      const status = getTechnicalSessionStatus(nextSession);
      setSession(nextSession);

      if (canHaveCurrentQuestion(status)) {
        const questionResponse = await technicalInterviewApi.getCurrentQuestion(sessionId);
        if (requestIdRef.current !== requestId) return null;
        setCurrentQuestion(questionResponse?.currentQuestion || questionResponse?.question || questionResponse);
      } else {
        setCurrentQuestion(null);
      }
      return nextSession;
    } catch (requestError) {
      if (requestIdRef.current === requestId) setError(requestError);
      return null;
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

  const startSession = useCallback(async () => {
    if (!sessionId) return null;
    setError(null);
    const response = await technicalInterviewApi.startSession(sessionId);
    const nextSession = response?.session || response;
    setSession(nextSession);
    if (response?.currentQuestion || response?.question) {
      setCurrentQuestion(response.currentQuestion || response.question);
    } else {
      await load();
    }
    return nextSession;
  }, [load, sessionId]);

  const applyAnswerResponse = useCallback((response) => {
    if (!response) return;
    if (response.session) setSession(response.session);
    else if (response.sessionStatus) {
      setSession((current) => ({ ...current, sessionStatus: response.sessionStatus }));
    }
    if (response.currentQuestion || response.question) {
      setCurrentQuestion(response.currentQuestion || response.question);
    } else if (response.sessionStatus === TechnicalSessionStatus.COMPLETED) {
      setCurrentQuestion(null);
    }
  }, []);

  return {
    session,
    currentQuestion,
    isLoading,
    error,
    reload: load,
    startSession,
    applyAnswerResponse,
  };
}

