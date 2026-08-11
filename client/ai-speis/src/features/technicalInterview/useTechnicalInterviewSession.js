import { useCallback, useEffect, useReducer, useRef } from 'react';
import { getInterviewRoomPath } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import technicalV2InterviewApi, {
  TechnicalV2InterviewError,
} from '../../services/technicalV2InterviewApi';
import {
  TechnicalV2ErrorCode,
  TechnicalV2FlowPhase,
} from './technicalV2Interview.types';

const initialState = {
  phase: TechnicalV2FlowPhase.CHECKING_SESSION,
  generalSession: null,
  session: null,
  currentQuestion: null,
  transcriptMessages: [],
  completionResult: null,
  feedbackRetrying: false,
  error: null,
  conflict: null,
  resumed: false,
};

const isCompletedStatus = (status) => String(status || '').toLowerCase() === 'completed';

const isReadyToComplete = (sessionState) => {
  const targetCount = Number(sessionState?.targetMainQuestionCount || 0);
  const completedCount = Number(sessionState?.completedMainQuestionCount || 0);
  return targetCount > 0 && completedCount >= targetCount;
};

const interviewerMessage = (question) => ({
  id: `question-${question.sessionQuestionId}`,
  speaker: 'interviewer',
  content: question.content,
  questionType: question.questionType,
  status: 'current',
  createdAt: question.askedAt || new Date().toISOString(),
});

const candidateMessage = (question, transcript) => ({
  id: `answer-${question.sessionQuestionId}`,
  speaker: 'candidate',
  content: transcript,
  questionType: question.questionType,
  status: 'submitted',
  createdAt: new Date().toISOString(),
});

const normalizeServerTranscript = (entries = []) => entries
  .filter((entry) => entry?.sessionQuestionId && entry?.content)
  .map((entry) => {
    const candidate = String(entry.role || '').toUpperCase() === 'CANDIDATE';
    return {
      id: `${candidate ? 'answer' : 'question'}-${entry.sessionQuestionId}`,
      speaker: candidate ? 'candidate' : 'interviewer',
      content: entry.content,
      questionType: entry.questionType,
      status: candidate ? 'submitted' : 'current',
      createdAt: entry.createdAt || new Date().toISOString(),
    };
  });

const appendUnique = (messages, additions) => {
  const existingIds = new Set(messages.map((message) => message.id));
  const additionIds = new Set(additions.map((message) => message.id));
  return [
    ...messages.map((message) => (
      message.status === 'current' && !additionIds.has(message.id)
        ? { ...message, status: 'submitted' }
        : message
    )),
    ...additions.filter((message) => !existingIds.has(message.id)),
  ];
};

function reducer(state, action) {
  switch (action.type) {
    case 'PHASE':
      return { ...state, phase: action.phase, error: null };
    case 'READY': {
      const additions = action.question ? [interviewerMessage(action.question)] : [];
      const restoredTranscript = action.transcript
        ? normalizeServerTranscript(action.transcript)
        : state.transcriptMessages;
      return {
        ...state,
        phase: TechnicalV2FlowPhase.READY_TO_ANSWER,
        generalSession: action.generalSession || state.generalSession,
        session: action.session || state.session,
        currentQuestion: action.question,
        transcriptMessages: appendUnique(restoredTranscript, additions),
        resumed: action.resumed ?? state.resumed,
        error: null,
        conflict: null,
      };
    }
    case 'ANSWER_ACCEPTED': {
      const additions = [candidateMessage(action.question, action.transcript)];
      if (action.nextQuestion) additions.push(interviewerMessage(action.nextQuestion));
      return {
        ...state,
        phase: action.nextQuestion
          ? TechnicalV2FlowPhase.READY_TO_ANSWER
          : TechnicalV2FlowPhase.COMPLETING,
        session: action.session || state.session,
        currentQuestion: action.nextQuestion || null,
        transcriptMessages: appendUnique(state.transcriptMessages, additions),
        error: null,
      };
    }
    case 'COMPLETED':
      return {
        ...state,
        phase: TechnicalV2FlowPhase.COMPLETED,
        generalSession: action.generalSession || state.generalSession,
        currentQuestion: null,
        completionResult: action.result,
        feedbackRetrying: false,
        error: null,
      };
    case 'FEEDBACK_RETRYING':
      return { ...state, feedbackRetrying: true };
    case 'FEEDBACK_RETRY_FAILED':
      return { ...state, feedbackRetrying: false };
    case 'CONFLICT':
      return {
        ...state,
        phase: TechnicalV2FlowPhase.SESSION_CONFLICT,
        conflict: action.conflict,
        generalSession: action.generalSession || state.generalSession,
        error: null,
      };
    case 'ERROR':
      return {
        ...state,
        phase: action.fatal
          ? TechnicalV2FlowPhase.FATAL_ERROR
          : TechnicalV2FlowPhase.RECOVERABLE_ERROR,
        error: action.error,
      };
    case 'DISMISS_ERROR':
      return {
        ...state,
        phase: state.currentQuestion
          ? TechnicalV2FlowPhase.READY_TO_ANSWER
          : TechnicalV2FlowPhase.LOADING_QUESTION,
        error: null,
      };
    default:
      return state;
  }
}

const normalizeError = (error, fallbackCode = TechnicalV2ErrorCode.UNKNOWN_ERROR) => {
  if (error instanceof TechnicalV2InterviewError) return error;
  return new TechnicalV2InterviewError(error?.message || 'Technical V2 interview request failed', {
    code: error?.code || fallbackCode,
    status: error?.status,
    details: error?.details,
  });
};

const createConflict = (campaign, activeSession) => ({
  campaign,
  sessionId: activeSession.interviewSessionId,
  interviewType: activeSession.interviewRoundType,
  status: activeSession.status,
  completedQuestionCount: activeSession.completedQuestionCount || 0,
  canResume: true,
  resumePath: getInterviewRoomPath(activeSession.interviewSessionId),
});

export default function useTechnicalInterviewSession(sessionId) {
  const [state, dispatch] = useReducer(reducer, initialState);
  const loadControllerRef = useRef(null);
  const submitInFlightRef = useRef(false);
  const completeInFlightRef = useRef(false);
  const feedbackInFlightRef = useRef(false);
  const idempotencyKeysRef = useRef(new Map());

  const completeInterview = useCallback(async () => {
    if (!sessionId || completeInFlightRef.current) return null;
    completeInFlightRef.current = true;
    dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.COMPLETING });
    try {
      const result = await technicalV2InterviewApi.complete(sessionId);
      dispatch({ type: 'COMPLETED', result });
      return result;
    } catch (error) {
      const normalized = normalizeError(error);
      dispatch({ type: 'ERROR', error: normalized, fatal: false });
      throw normalized;
    } finally {
      completeInFlightRef.current = false;
    }
  }, [sessionId]);

  const retryFeedback = useCallback(async () => {
    if (!sessionId || feedbackInFlightRef.current) return null;
    feedbackInFlightRef.current = true;
    dispatch({ type: 'FEEDBACK_RETRYING' });
    try {
      const result = await technicalV2InterviewApi.generateFeedback(sessionId);
      dispatch({ type: 'COMPLETED', result });
      return result;
    } catch (error) {
      const normalized = normalizeError(error);
      dispatch({ type: 'FEEDBACK_RETRY_FAILED' });
      throw normalized;
    } finally {
      feedbackInFlightRef.current = false;
    }
  }, [sessionId]);

  const completeIfReady = useCallback(async (sessionState, generalSession) => {
    if (!isReadyToComplete(sessionState)) return null;
    const result = await technicalV2InterviewApi.complete(sessionId);
    dispatch({ type: 'COMPLETED', result, generalSession });
    return result;
  }, [sessionId]);

  const loadRoom = useCallback(async () => {
    loadControllerRef.current?.abort();
    const controller = new AbortController();
    loadControllerRef.current = controller;

    if (!sessionId) {
      dispatch({
        type: 'ERROR',
        fatal: true,
        error: new TechnicalV2InterviewError('Interview session ID is missing', {
          code: TechnicalV2ErrorCode.SESSION_NOT_FOUND,
        }),
      });
      return;
    }

    dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.CHECKING_SESSION });
    try {
      const [generalSession, activeCampaign] = await Promise.all([
        interviewSessionService.getSession(sessionId),
        interviewSessionService.getActiveCampaign(),
      ]);
      if (controller.signal.aborted) return;

      if (generalSession?.interviewRoundType !== 'Technical') {
        throw new TechnicalV2InterviewError('Session is not a technical interview round', {
          code: TechnicalV2ErrorCode.WRONG_ROUND_TYPE,
          status: 400,
        });
      }

      if (isCompletedStatus(generalSession.status)) {
        dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.COMPLETING });
        const result = await technicalV2InterviewApi.getResult(sessionId, { signal: controller.signal });
        dispatch({ type: 'COMPLETED', result, generalSession });
        return;
      }

      if (generalSession.status === 'Cancelled') {
        throw new TechnicalV2InterviewError('Technical interview session is cancelled', {
          code: TechnicalV2ErrorCode.SESSION_CANCELLED,
          status: 409,
        });
      }

      const targetCampaign = String(activeCampaign?.interviewCampaignId)
        === String(generalSession.interviewCampaignId)
        ? activeCampaign
        : await interviewSessionService.getCampaign(generalSession.interviewCampaignId);
      if (targetCampaign?.status === 'Expired' || targetCampaign?.status === 'Cancelled' || targetCampaign?.status === 'Completed') {
        throw new TechnicalV2InterviewError('Interview campaign is closed', {
          code: TechnicalV2ErrorCode.CAMPAIGN_NOT_ACTIVE,
          status: 409,
        });
      }

      const otherActiveSession = activeCampaign?.sessions?.find((session) => (
        session.status === 'Active'
        && String(session.interviewSessionId) !== String(sessionId)
      ));
      if (otherActiveSession) {
        dispatch({
          type: 'CONFLICT',
          generalSession,
          conflict: createConflict(activeCampaign, otherActiveSession),
        });
        return;
      }

      dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.INITIALIZING });
      let sessionState;
      try {
        sessionState = await technicalV2InterviewApi.getState(sessionId, { signal: controller.signal });
      } catch (error) {
        if (error?.name === 'AbortError') return;
        if (error?.code !== TechnicalV2ErrorCode.NOT_INITIALIZED) throw error;
        sessionState = await technicalV2InterviewApi.initialize(sessionId, undefined, {
          signal: controller.signal,
        });
      }

      if (sessionState?.isComplete || isCompletedStatus(sessionState?.sessionStatus)) {
        const result = await technicalV2InterviewApi.getResult(sessionId, { signal: controller.signal });
        dispatch({ type: 'COMPLETED', result, generalSession });
        return;
      }

      dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.LOADING_QUESTION });
      let question = sessionState?.currentQuestion || null;
      if (!question) {
        try {
          question = await technicalV2InterviewApi.start(sessionId, { signal: controller.signal });
        } catch (error) {
          if (error?.code === TechnicalV2ErrorCode.ALL_QUESTIONS_ANSWERED) {
            const latestState = await technicalV2InterviewApi.getState(sessionId, {
              signal: controller.signal,
            });
            const result = await completeIfReady(latestState, generalSession);
            if (result) return;
          }
          if (error?.code === TechnicalV2ErrorCode.ROUND_COMPLETED) {
            const result = await technicalV2InterviewApi.getResult(sessionId, { signal: controller.signal });
            dispatch({ type: 'COMPLETED', result, generalSession });
            return;
          }
          throw error;
        }
      }

      const latestState = sessionState || await technicalV2InterviewApi.getState(
        sessionId,
        { signal: controller.signal },
      );
      if (controller.signal.aborted) return;
      dispatch({
        type: 'READY',
        generalSession,
        session: latestState,
        question,
        transcript: latestState?.transcript,
        resumed: (latestState?.completedMainQuestionCount || 0) > 0,
      });
    } catch (error) {
      if (error?.name === 'AbortError') return;
      const normalized = normalizeError(error);
      const fatal = normalized.status === 401
        || normalized.status === 403
        || normalized.status === 404
        || normalized.code === TechnicalV2ErrorCode.SESSION_NOT_FOUND
        || normalized.code === TechnicalV2ErrorCode.CAMPAIGN_NOT_ACTIVE
        || normalized.code === TechnicalV2ErrorCode.WRONG_ROUND_TYPE
        || normalized.code === TechnicalV2ErrorCode.LEGACY_SESSION;
      dispatch({ type: 'ERROR', error: normalized, fatal });
    }
  }, [completeIfReady, sessionId]);

  useEffect(() => {
    loadRoom();
    return () => loadControllerRef.current?.abort();
  }, [loadRoom]);

  const reconcileSubmission = useCallback(async (submittedQuestion, transcript, generalSession) => {
    const latestState = await technicalV2InterviewApi.getState(sessionId);
    if (latestState?.isComplete || isReadyToComplete(latestState)) {
      dispatch({
        type: 'ANSWER_ACCEPTED',
        question: submittedQuestion,
        transcript,
        nextQuestion: null,
        session: latestState,
      });
      const result = await completeIfReady(latestState, generalSession);
      return result ? { accepted: true, reconciled: true, completed: true, result } : { accepted: true, reconciled: true };
    }

    let currentQuestion = latestState?.currentQuestion || null;
    if (!currentQuestion) {
      try {
        currentQuestion = await technicalV2InterviewApi.getCurrentQuestion(sessionId);
      } catch (error) {
        if (error?.code !== TechnicalV2ErrorCode.ALL_QUESTIONS_ANSWERED) throw error;
      }
    }

    if (currentQuestion
      && String(currentQuestion.sessionQuestionId) !== String(submittedQuestion.sessionQuestionId)) {
      dispatch({
        type: 'ANSWER_ACCEPTED',
        question: submittedQuestion,
        transcript,
        nextQuestion: currentQuestion,
        session: latestState,
      });
      return { accepted: true, reconciled: true };
    }

    if (String(latestState?.evaluationStatus || '').toUpperCase() === 'PROCESSING') {
      return { accepted: false, processing: true };
    }
    return { accepted: false };
  }, [completeIfReady, sessionId]);

  const submitAnswer = useCallback(async ({ transcript, audioId, durationSeconds, sttConfidence }) => {
    const question = state.currentQuestion;
    const normalizedTranscript = transcript?.trim();
    if (!normalizedTranscript) {
      throw new TechnicalV2InterviewError('Transcript is required', {
        code: TechnicalV2ErrorCode.TRANSCRIPT_REQUIRED,
      });
    }
    if (!question?.sessionQuestionId || submitInFlightRef.current) return null;

    const keyId = String(question.sessionQuestionId);
    if (!idempotencyKeysRef.current.has(keyId)) {
      const runtimeCrypto = typeof window !== 'undefined' ? window.crypto : null;
      const suffix = typeof runtimeCrypto?.randomUUID === 'function'
        ? runtimeCrypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
      idempotencyKeysRef.current.set(keyId, `technical-v2-${sessionId}-${keyId}-${suffix}`);
    }

    submitInFlightRef.current = true;
    dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.SUBMITTING_ANSWER });
    try {
      dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.EVALUATING_ANSWER });
      const response = await technicalV2InterviewApi.submitAnswer(
        sessionId,
        question.sessionQuestionId,
        {
          transcript: normalizedTranscript,
          ...(audioId ? { audioId } : {}),
          ...(Number.isFinite(durationSeconds) ? { answerDurationSeconds: durationSeconds } : {}),
          ...(Number.isFinite(sttConfidence) ? { sttConfidence } : {}),
        },
        { idempotencyKey: idempotencyKeysRef.current.get(keyId) },
      );

      const nextState = response?.state || state.session;
      const nextQuestion = response?.nextQuestion || null;
      dispatch({
        type: 'ANSWER_ACCEPTED',
        question,
        transcript: normalizedTranscript,
        nextQuestion,
        session: nextState,
      });
      idempotencyKeysRef.current.delete(keyId);

      if (response?.decision === 'COMPLETE' || isReadyToComplete(nextState)) {
        try {
          const result = await completeInterview();
          return { accepted: true, completed: true, result, response };
        } catch (completionError) {
          dispatch({ type: 'ERROR', error: normalizeError(completionError), fatal: false });
          return { accepted: true, completionPending: true, response };
        }
      }

      return { accepted: true, response };
    } catch (error) {
      if (error?.name === 'AbortError') return null;
      const normalized = normalizeError(error);
      try {
        const reconciliation = await reconcileSubmission(question, normalizedTranscript, state.generalSession);
        if (reconciliation.accepted) {
          idempotencyKeysRef.current.delete(keyId);
          return reconciliation;
        }
        if (reconciliation.processing) {
          dispatch({ type: 'PHASE', phase: TechnicalV2FlowPhase.EVALUATING_ANSWER });
          return { accepted: false, processing: true };
        }
      } catch {
        // Keep the stable idempotency key so a retry represents the same submission.
      }

      dispatch({ type: 'ERROR', error: normalized, fatal: false });
      throw normalized;
    } finally {
      submitInFlightRef.current = false;
    }
  }, [completeInterview, reconcileSubmission, sessionId, state.currentQuestion, state.generalSession, state.session]);

  return {
    ...state,
    isBusy: [
      TechnicalV2FlowPhase.CHECKING_SESSION,
      TechnicalV2FlowPhase.INITIALIZING,
      TechnicalV2FlowPhase.LOADING_QUESTION,
      TechnicalV2FlowPhase.SUBMITTING_ANSWER,
      TechnicalV2FlowPhase.EVALUATING_ANSWER,
      TechnicalV2FlowPhase.COMPLETING,
    ].includes(state.phase),
    reload: loadRoom,
    submitAnswer,
    completeInterview,
    retryFeedback,
    dismissError: () => dispatch({ type: 'DISMISS_ERROR' }),
  };
}
