import { useCallback, useEffect, useRef, useState } from 'react';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import { TechnicalSessionStatus } from './technicalInterview.types';

const PROCESSING_POLL_INTERVAL_MS = 1500;

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

const canFetchCurrentQuestion = (status) => (
  status === TechnicalSessionStatus.QUESTION_READY
  || status === TechnicalSessionStatus.ANSWERING
);

const isProcessingStatus = (status) => (
  status === TechnicalSessionStatus.EVALUATING
  || status === TechnicalSessionStatus.SELECTING_QUESTION
);

const firstDefined = (...values) => values.find((value) => value !== undefined && value !== null);

export const normalizeTechnicalProgress = (source = {}, fallback = {}) => ({
  mainQuestionIndex: firstDefined(source.mainQuestionIndex, fallback.mainQuestionIndex),
  totalMainQuestions: firstDefined(source.totalMainQuestions, fallback.totalMainQuestions),
  subQuestionIndex: firstDefined(source.subQuestionIndex, fallback.subQuestionIndex, null),
  requiredSubQuestionCount: firstDefined(
    source.requiredSubQuestionCount,
    fallback.requiredSubQuestionCount,
    source.requiredFollowUpCount,
    fallback.requiredFollowUpCount,
    0,
  ),
  completedSubQuestionCount: firstDefined(
    source.completedSubQuestionCount,
    fallback.completedSubQuestionCount,
    source.completedFollowUpCount,
    fallback.completedFollowUpCount,
    0,
  ),
});

export const normalizeTechnicalQuestion = (question, progress) => {
  if (!question) return null;
  const normalizedProgress = normalizeTechnicalProgress(progress || question.progress || question, question);
  return {
    ...question,
    questionId: question.questionId ?? null,
    ...normalizedProgress,
    progress: normalizedProgress,
  };
};

export default function useTechnicalInterviewSession(sessionId) {
  const [session, setSession] = useState(null);
  const [currentQuestion, setCurrentQuestion] = useState(null);
  const [processingStatus, setProcessingStatus] = useState(null);
  const [isLoading, setIsLoading] = useState(Boolean(sessionId));
  const [error, setError] = useState(null);
  const [questionError, setQuestionError] = useState(null);
  const requestIdRef = useRef(0);
  const sessionRef = useRef(null);
  const currentQuestionRef = useRef(null);

  const commitSession = useCallback((nextSession) => {
    sessionRef.current = nextSession;
    setSession(nextSession);
  }, []);

  const commitQuestion = useCallback((nextQuestion) => {
    currentQuestionRef.current = nextQuestion;
    setCurrentQuestion(nextQuestion);
  }, []);

  const synchronize = useCallback(async ({ initialize = false, showLoading = false } = {}) => {
    if (!sessionId) {
      if (showLoading) setIsLoading(false);
      return { session: null, currentQuestion: null, status: null };
    }

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    if (showLoading) setIsLoading(true);
    setQuestionError(null);
    if (showLoading) setError(null);

    try {
      if (initialize) await technicalInterviewApi.initializeSession(sessionId);
      if (requestIdRef.current !== requestId) return null;

      const sessionResponse = await technicalInterviewApi.getSession(sessionId);
      if (requestIdRef.current !== requestId) return null;
      const nextSession = sessionResponse?.session || sessionResponse;
      const status = getTechnicalSessionStatus(nextSession);
      let nextQuestion = currentQuestionRef.current;
      let nextQuestionError = null;

      commitSession(nextSession);
      if (canFetchCurrentQuestion(status)) {
        try {
          const questionResponse = await technicalInterviewApi.getCurrentQuestion(sessionId);
          if (requestIdRef.current !== requestId) return null;
          nextQuestion = normalizeTechnicalQuestion(
            questionResponse?.currentQuestion || questionResponse?.question || questionResponse,
          );
          commitQuestion(nextQuestion);
        } catch (requestError) {
          if (requestIdRef.current !== requestId) return null;
          nextQuestion = null;
          nextQuestionError = requestError;
          commitQuestion(null);
          setQuestionError(requestError);
        }
      } else if (!isProcessingStatus(status)) {
        nextQuestion = null;
        commitQuestion(null);
      }

      return {
        session: nextSession,
        currentQuestion: nextQuestion,
        status,
        questionError: nextQuestionError,
      };
    } catch (requestError) {
      if (requestIdRef.current === requestId && showLoading) setError(requestError);
      return { error: requestError };
    } finally {
      if (requestIdRef.current === requestId && showLoading) setIsLoading(false);
    }
  }, [commitQuestion, commitSession, sessionId]);

  const load = useCallback(() => synchronize({ initialize: true, showLoading: true }), [synchronize]);

  useEffect(() => {
    load();
    return () => {
      requestIdRef.current += 1;
    };
  }, [load]);

  const status = getTechnicalSessionStatus(session);
  useEffect(() => {
    if (!isProcessingStatus(status)) return undefined;
    let cancelled = false;
    let timer = null;
    const poll = async () => {
      await synchronize();
      if (!cancelled) timer = window.setTimeout(poll, PROCESSING_POLL_INTERVAL_MS);
    };
    timer = window.setTimeout(poll, PROCESSING_POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      if (timer) window.clearTimeout(timer);
    };
  }, [status, synchronize]);

  const startSession = useCallback(async () => {
    if (!sessionId) return null;
    setError(null);
    const response = await technicalInterviewApi.startSession(sessionId);
    const question = normalizeTechnicalQuestion(
      response?.currentQuestion || response?.question || response,
      response?.progress,
    );
    const nextSession = response?.session || {
      ...sessionRef.current,
      sessionId,
      status: question?.sessionStatus || TechnicalSessionStatus.QUESTION_READY,
      sessionStatus: question?.sessionStatus || TechnicalSessionStatus.QUESTION_READY,
    };
    commitSession(nextSession);
    if (question?.attemptId) commitQuestion(question);
    else await synchronize();
    return nextSession;
  }, [commitQuestion, commitSession, sessionId, synchronize]);

  const markProcessing = useCallback((attemptId) => {
    setProcessingStatus({ attemptId, evaluation: 'PROCESSING' });
    commitSession({
      ...sessionRef.current,
      sessionId,
      status: TechnicalSessionStatus.EVALUATING,
      sessionStatus: TechnicalSessionStatus.EVALUATING,
    });
  }, [commitSession, sessionId]);

  const applyAnswerResponse = useCallback((response) => {
    if (!response) return;
    setProcessingStatus(response.processing || null);
    const responseStatus = response.sessionStatus || getTechnicalSessionStatus(response.session);
    if (response.session) commitSession(response.session);
    else if (responseStatus) {
      commitSession({
        ...sessionRef.current,
        sessionStatus: responseStatus,
        status: responseStatus,
      });
    }

    const nextQuestion = normalizeTechnicalQuestion(
      response.nextQuestion || response.currentQuestion || response.question,
      response.progress,
    );
    if (nextQuestion) commitQuestion(nextQuestion);
    else if (responseStatus === TechnicalSessionStatus.COMPLETED) commitQuestion(null);
  }, [commitQuestion, commitSession]);

  const reconcileAfterSubmission = useCallback(async (submittedAttemptId) => {
    const synchronized = await synchronize();
    if (!synchronized || synchronized.error) {
      return { state: 'UNKNOWN', error: synchronized?.error };
    }

    const nextStatus = synchronized.status;
    const nextQuestion = synchronized.currentQuestion;
    if (nextStatus === TechnicalSessionStatus.COMPLETED) return { state: 'ACCEPTED_COMPLETED' };
    if (isProcessingStatus(nextStatus)) return { state: 'PROCESSING' };
    if (canFetchCurrentQuestion(nextStatus) && nextQuestion?.attemptId) {
      return String(nextQuestion.attemptId) === String(submittedAttemptId)
        ? { state: 'RETRYABLE', currentQuestion: nextQuestion }
        : { state: 'ACCEPTED_NEXT_QUESTION', currentQuestion: nextQuestion };
    }
    return { state: 'UNKNOWN' };
  }, [synchronize]);

  return {
    session,
    currentQuestion,
    processingStatus,
    isProcessing: isProcessingStatus(status),
    isLoading,
    error,
    questionError,
    reload: load,
    synchronize,
    startSession,
    markProcessing,
    applyAnswerResponse,
    reconcileAfterSubmission,
  };
}
