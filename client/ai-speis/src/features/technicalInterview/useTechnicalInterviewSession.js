import { useCallback, useEffect, useRef, useState } from 'react';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import { TechnicalSessionStatus } from './technicalInterview.types';

const LEGACY_SESSION_STATUS = Object.freeze({
  Pending: TechnicalSessionStatus.CREATED,
  Active: TechnicalSessionStatus.QUESTION_READY,
  Completed: TechnicalSessionStatus.COMPLETED,
  Cancelled: TechnicalSessionStatus.FAILED,
});

export const getTechnicalSessionStatus = (session) => {
  const status = session?.sessionStatus || session?.status || null;
  return LEGACY_SESSION_STATUS[status] || status;
};

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
  const [questionError, setQuestionError] = useState(null);
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
    setQuestionError(null);

    try {
      await technicalInterviewApi.initializeSession(sessionId);
      if (requestIdRef.current !== requestId) return null;
      const sessionResponse = await technicalInterviewApi.getSession(sessionId);
      if (requestIdRef.current !== requestId) return null;
      const nextSession = sessionResponse?.session || sessionResponse;
      const status = getTechnicalSessionStatus(nextSession);
      setSession(nextSession);

      if (canHaveCurrentQuestion(status)) {
        try {
          const questionResponse = await technicalInterviewApi.getCurrentQuestion(sessionId);
          if (requestIdRef.current !== requestId) return null;
          setCurrentQuestion(questionResponse?.currentQuestion || questionResponse?.question || questionResponse);
        } catch (requestError) {
          if (requestIdRef.current !== requestId) return null;
          setCurrentQuestion(null);
          setQuestionError(requestError);
        }
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
    const question = response?.currentQuestion || response?.question || response;
    const nextSession = response?.session || {
      ...session,
      sessionId,
      status: question?.sessionStatus || TechnicalSessionStatus.QUESTION_READY,
    };
    setSession(nextSession);
    if (question?.attemptId) {
      setCurrentQuestion(question);
    } else {
      await load();
    }
    return nextSession;
  }, [load, session, sessionId]);

  const applyAnswerResponse = useCallback((response) => {
    if (!response) return;
    if (response.session) setSession(response.session);
    else if (response.sessionStatus) {
      setSession((current) => ({ ...current, sessionStatus: response.sessionStatus }));
    }
    if (response.nextQuestion || response.currentQuestion || response.question) {
      setCurrentQuestion(response.nextQuestion || response.currentQuestion || response.question);
    } else if (response.sessionStatus === TechnicalSessionStatus.COMPLETED) {
      setCurrentQuestion(null);
    }
  }, []);

  return {
    session,
    currentQuestion,
    isLoading,
    error,
    questionError,
    reload: load,
    startSession,
    applyAnswerResponse,
  };
}
