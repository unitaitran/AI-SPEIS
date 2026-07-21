import { useCallback, useEffect, useReducer, useRef } from 'react';
import { getInterviewRoomPath } from '../../routes/routePaths';
import behavioralInterviewApi, {
  BehavioralInterviewError,
} from '../../services/behavioralInterviewApi';
import interviewSessionService from '../../services/InterviewSessionService';
import {
  BehavioralErrorCode,
  BehavioralFlowPhase,
  BehavioralSessionStatus,
} from './behavioralInterview.types';

const initialState = {
  phase: BehavioralFlowPhase.CHECKING_SESSION,
  generalSession: null,
  session: null,
  currentQuestion: null,
  transcriptMessages: [],
  completionResult: null,
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
  createdAt: new Date().toISOString(),
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
  .filter((entry) => entry?.id && entry?.content)
  .map((entry) => ({
    id: String(entry.id),
    speaker: String(entry.role || '').toUpperCase() === 'CANDIDATE' ? 'candidate' : 'interviewer',
    content: entry.content,
    questionType: entry.questionType,
    status: String(entry.status || '').toUpperCase() === 'CURRENT' ? 'current' : 'submitted',
    createdAt: entry.createdAt || new Date().toISOString(),
  }));

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
        phase: BehavioralFlowPhase.READY_TO_ANSWER,
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
          ? BehavioralFlowPhase.READY_TO_ANSWER
          : BehavioralFlowPhase.COMPLETING,
        currentQuestion: action.nextQuestion || null,
        transcriptMessages: appendUnique(state.transcriptMessages, additions),
        error: null,
      };
    }
    case 'COMPLETED':
      return {
        ...state,
        phase: BehavioralFlowPhase.COMPLETED,
        generalSession: action.generalSession || state.generalSession,
        currentQuestion: null,
        completionResult: action.result,
        error: null,
      };
    case 'CONFLICT':
      return {
        ...state,
        phase: BehavioralFlowPhase.SESSION_CONFLICT,
        conflict: action.conflict,
        generalSession: action.generalSession || state.generalSession,
        error: null,
      };
    case 'ERROR':
      return {
        ...state,
        phase: action.fatal
          ? BehavioralFlowPhase.FATAL_ERROR
          : BehavioralFlowPhase.RECOVERABLE_ERROR,
        error: action.error,
      };
    case 'DISMISS_ERROR':
      return {
        ...state,
        phase: state.currentQuestion
          ? BehavioralFlowPhase.READY_TO_ANSWER
          : BehavioralFlowPhase.LOADING_QUESTION,
        error: null,
      };
    default:
      return state;
  }
}

const normalizeError = (error, fallbackCode = BehavioralErrorCode.UNKNOWN_ERROR) => {
  if (error instanceof BehavioralInterviewError) return error;
  return new BehavioralInterviewError(error?.message || 'Behavioral interview request failed', {
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
  canEnd: false,
  canCloseCampaign: false,
  resumePath: getInterviewRoomPath(activeSession.interviewSessionId),
});

export default function useBehavioralInterviewSession(sessionId) {
  const [state, dispatch] = useReducer(reducer, initialState);
  const loadControllerRef = useRef(null);
  const submitInFlightRef = useRef(false);
  const completeInFlightRef = useRef(false);
  const idempotencyKeysRef = useRef(new Map());

  const completeInterview = useCallback(async () => {
    if (!sessionId || completeInFlightRef.current) return null;
    completeInFlightRef.current = true;
    dispatch({ type: 'PHASE', phase: BehavioralFlowPhase.COMPLETING });
    try {
      const result = await behavioralInterviewApi.complete(sessionId);
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

  const loadRoom = useCallback(async () => {
    loadControllerRef.current?.abort();
    const controller = new AbortController();
    loadControllerRef.current = controller;

    if (!sessionId) {
      dispatch({
        type: 'ERROR',
        fatal: true,
        error: new BehavioralInterviewError('Interview session ID is missing', {
          code: BehavioralErrorCode.SESSION_NOT_FOUND,
        }),
      });
      return;
    }

    dispatch({ type: 'PHASE', phase: BehavioralFlowPhase.CHECKING_SESSION });
    try {
      const [generalSession, activeCampaign] = await Promise.all([
        interviewSessionService.getSession(sessionId),
        interviewSessionService.getActiveCampaign(),
      ]);
      if (controller.signal.aborted) return;

      if (generalSession?.interviewRoundType !== 'Behavior') {
        throw new BehavioralInterviewError('Session is not a behavioral interview round', {
          code: BehavioralErrorCode.WRONG_ROUND_TYPE,
          status: 400,
        });
      }

      if (isCompletedStatus(generalSession.status)) {
        dispatch({ type: 'PHASE', phase: BehavioralFlowPhase.COMPLETING });
        const result = await behavioralInterviewApi.getResult(sessionId, { signal: controller.signal });
        dispatch({ type: 'COMPLETED', result, generalSession });
        return;
      }

      if (generalSession.status === 'Cancelled') {
        throw new BehavioralInterviewError('Interview session is cancelled', {
          code: BehavioralErrorCode.ROUND_COMPLETED,
          status: 409,
        });
      }

      const targetCampaign = String(activeCampaign?.interviewCampaignId)
        === String(generalSession.interviewCampaignId)
        ? activeCampaign
        : await interviewSessionService.getCampaign(generalSession.interviewCampaignId);
      if (targetCampaign?.status === 'Expired') {
        throw new BehavioralInterviewError('Interview campaign is expired', {
          code: BehavioralErrorCode.SESSION_EXPIRED,
          status: 409,
        });
      }
      if (targetCampaign?.status === 'Cancelled' || targetCampaign?.status === 'Completed') {
        throw new BehavioralInterviewError('Interview campaign is closed', {
          code: BehavioralErrorCode.CAMPAIGN_CLOSED,
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

      dispatch({ type: 'PHASE', phase: BehavioralFlowPhase.INITIALIZING });
      let sessionState = null;
      try {
        sessionState = await behavioralInterviewApi.getState(sessionId, { signal: controller.signal });
      } catch (error) {
        if (error?.name === 'AbortError') return;
        if (error?.code !== BehavioralErrorCode.NOT_INITIALIZED) throw error;
        sessionState = await behavioralInterviewApi.initialize(sessionId, undefined, {
          signal: controller.signal,
        });
      }

      if (isCompletedStatus(sessionState?.status)) {
        const result = await behavioralInterviewApi.getResult(sessionId, { signal: controller.signal });
        dispatch({ type: 'COMPLETED', result, generalSession });
        return;
      }

      dispatch({ type: 'PHASE', phase: BehavioralFlowPhase.LOADING_QUESTION });
      let question;
      try {
        question = await behavioralInterviewApi.start(sessionId, { signal: controller.signal });
      } catch (error) {
        if (error?.code === BehavioralErrorCode.ALL_QUESTIONS_ANSWERED) {
          const latestState = await behavioralInterviewApi.getState(sessionId, {
            signal: controller.signal,
          });
          if (!isReadyToComplete(latestState)) throw error;
          const result = await behavioralInterviewApi.complete(sessionId, { signal: controller.signal });
          dispatch({ type: 'COMPLETED', result, generalSession });
          return;
        }
        if (error?.code === BehavioralErrorCode.ROUND_COMPLETED) {
          const result = await behavioralInterviewApi.getResult(sessionId, { signal: controller.signal });
          dispatch({ type: 'COMPLETED', result });
          return;
        }
        throw error;
      }

      const latestState = sessionState || await behavioralInterviewApi.getState(
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
        || normalized.code === BehavioralErrorCode.SESSION_NOT_FOUND
        || normalized.code === BehavioralErrorCode.SESSION_EXPIRED
        || normalized.code === BehavioralErrorCode.CAMPAIGN_CLOSED
        || normalized.code === BehavioralErrorCode.WRONG_ROUND_TYPE;
      dispatch({ type: 'ERROR', error: normalized, fatal });
    }
  }, [sessionId]);

  useEffect(() => {
    loadRoom();
    return () => loadControllerRef.current?.abort();
  }, [loadRoom]);

  const submitAnswer = useCallback(async ({ transcript, audioId, durationSeconds, sttConfidence }) => {
    const question = state.currentQuestion;
    const normalizedTranscript = transcript?.trim();
    if (!normalizedTranscript) {
      throw new BehavioralInterviewError('Transcript is required', {
        code: BehavioralErrorCode.TRANSCRIPT_REQUIRED,
      });
    }
    if (!question?.sessionQuestionId || submitInFlightRef.current) return null;

    const keyId = String(question.sessionQuestionId);
    if (!idempotencyKeysRef.current.has(keyId)) {
      const suffix = typeof crypto?.randomUUID === 'function'
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
      idempotencyKeysRef.current.set(keyId, `behavior-${sessionId}-${keyId}-${suffix}`);
    }

    submitInFlightRef.current = true;
    dispatch({ type: 'PHASE', phase: BehavioralFlowPhase.SUBMITTING_ANSWER });
    try {
      dispatch({ type: 'PHASE', phase: BehavioralFlowPhase.EVALUATING_ANSWER });
      const response = await behavioralInterviewApi.submitAnswer(
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

      dispatch({
        type: 'ANSWER_ACCEPTED',
        question,
        transcript: normalizedTranscript,
        nextQuestion: response?.nextQuestion || null,
      });
      idempotencyKeysRef.current.delete(keyId);

      if (response?.sessionStatus === BehavioralSessionStatus.READY_TO_COMPLETE) {
        try {
          const result = await behavioralInterviewApi.complete(sessionId);
          dispatch({ type: 'COMPLETED', result });
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
        const current = await behavioralInterviewApi.getCurrentQuestion(sessionId);
        if (String(current?.sessionQuestionId) !== String(question.sessionQuestionId)) {
          dispatch({
            type: 'ANSWER_ACCEPTED',
            question,
            transcript: normalizedTranscript,
            nextQuestion: current,
          });
          idempotencyKeysRef.current.delete(keyId);
          return { accepted: true, reconciled: true };
        }
      } catch (reconcileError) {
        if (reconcileError?.code === BehavioralErrorCode.ALL_QUESTIONS_ANSWERED) {
          const latestState = await behavioralInterviewApi.getState(sessionId);
          if (isReadyToComplete(latestState)) {
            dispatch({
              type: 'ANSWER_ACCEPTED',
              question,
              transcript: normalizedTranscript,
              nextQuestion: null,
            });
            idempotencyKeysRef.current.delete(keyId);
            try {
              const result = await behavioralInterviewApi.complete(sessionId);
              dispatch({ type: 'COMPLETED', result });
              return { accepted: true, reconciled: true, completed: true, result };
            } catch (completionError) {
              dispatch({ type: 'ERROR', error: normalizeError(completionError), fatal: false });
              return { accepted: true, reconciled: true, completionPending: true };
            }
          }
        }
      }

      dispatch({ type: 'ERROR', error: normalized, fatal: false });
      throw normalized;
    } finally {
      submitInFlightRef.current = false;
    }
  }, [sessionId, state.currentQuestion]);

  return {
    ...state,
    isBusy: [
      BehavioralFlowPhase.CHECKING_SESSION,
      BehavioralFlowPhase.INITIALIZING,
      BehavioralFlowPhase.LOADING_QUESTION,
      BehavioralFlowPhase.SUBMITTING_ANSWER,
      BehavioralFlowPhase.EVALUATING_ANSWER,
      BehavioralFlowPhase.COMPLETING,
    ].includes(state.phase),
    reload: loadRoom,
    submitAnswer,
    completeInterview,
    dismissError: () => dispatch({ type: 'DISMISS_ERROR' }),
  };
}
