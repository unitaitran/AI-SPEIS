import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  Bot,
  FileText,
  Loader2,
  Pause,
  RefreshCw,
  RotateCcw,
  Volume2,
} from 'lucide-react';
import BehavioralRecorderControls from '../../components/behavioralInterview/BehavioralRecorderControls';
import EndSessionConfirmDialog from '../../components/technicalInterview/EndSessionConfirmDialog';
import InterviewInitializationLoading from '../../components/technicalInterview/InterviewInitializationLoading';
import TechnicalEvaluationState from '../../components/technicalInterview/TechnicalEvaluationState';
import TechnicalInterviewErrorState from '../../components/technicalInterview/TechnicalInterviewErrorState';
import TechnicalTranscriptPanel from '../../components/technicalInterview/TechnicalTranscriptPanel';
import InterviewRoomShell from '../../components/interviewRoom/InterviewRoomShell';
import EvaluatingAnalysisModal from '../../components/interviewRoom/EvaluatingAnalysisModal';
import {
  clearStaleTechnicalInterviewDrafts,
  clearTechnicalInterviewDraft,
  readTechnicalInterviewDraft,
  readTechnicalInterviewSessionDraft,
  saveTechnicalInterviewDraft,
} from '../../features/technicalInterview/technicalInterviewDraft';
import { getTechnicalInterviewErrorKey } from '../../features/technicalInterview/technicalInterviewErrors';
import {
  RecordingStatus,
  SttStatus,
  TechnicalSessionStatus,
} from '../../features/technicalInterview/technicalInterview.types';
import useQuestionAudio, {
  QuestionAudioStatus,
} from '../../features/technicalInterview/useQuestionAudio';
import useSubmitTechnicalAnswer from '../../features/technicalInterview/useSubmitTechnicalAnswer';
import useTechnicalInterviewSession, {
  getTechnicalSessionStatus,
  TechnicalInterviewFlowStatus,
} from '../../features/technicalInterview/useTechnicalInterviewSession';
import useTechnicalRecorder from '../../features/technicalInterview/useTechnicalRecorder';
import useTechnicalInterviewTranscript, {
  TechnicalTranscriptItemStatus,
} from '../../features/technicalInterview/useTechnicalInterviewTranscript';
import { navigate } from '../../routes/navigation';
import { getCodingInterviewRoomPath, getInterviewResultPath, getInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import interviewSessionService from '../../services/InterviewSessionService';
import codingService from '../../services/codingService';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  getNextOpenSession,
  saveActiveInterviewContext,
} from '../../utils/interviewContext';
import useInterviewStrategy from '../../features/interviewStrategy/useInterviewStrategy';
import { InterviewMode } from '../../features/interviewStrategy/InterviewMode';
import '../../styles/user/TechnicalInterview.css';
import '../../styles/user/BehavioralInterview.css';

const getDefaultTranscriptVisibility = () => (
  typeof window === 'undefined' || window.innerWidth >= 1024
);

const TECHNICAL_RECORDER_COPY = {
  answerReady: 'room.recordingReady',
  answerReview: 'room.answerTitle',
  audioPreserved: 'room.sttError',
  audioUnsupported: 'room.audioFallback',
  microphoneHelp: 'room.microphoneError',
  microphoneUnavailable: 'room.microphoneError',
  processingTranscript: 'room.processingTranscript',
  processingTranscriptDescription: 'room.evaluatingDescription',
  recordAgain: 'room.startRecording',
  recording: 'room.recordingNow',
  recordingControls: 'room.answerTitle',
  recordingPrivacy: 'room.recordingHint',
  requestingMicrophone: 'room.transcriptRequestingPermission',
  retryTranscription: 'common.retry',
  reviewBeforeSubmit: 'room.transcriptHelper',
  startRecording: 'room.startRecording',
  stopRecording: 'room.stopRecording',
  submitAnswer: 'room.submitAnswer',
  submitting: 'room.submitting',
  tapToAnswer: 'room.recordingIdle',
  transcriptionFailed: 'room.sttError',
  transcriptHelper: 'room.transcriptHelper',
  transcriptPlaceholder: 'room.transcriptPlaceholder',
  tryAgain: 'common.retry',
  yourTranscript: 'room.transcriptLabel',
};

function TechnicalInterviewPage({ sessionId }) {
  const activeContext = useMemo(() => getActiveInterviewContext(), []);
  const setupDraft = useMemo(() => getInterviewSetupDraft(), []);
  const resolvedSessionId = sessionId || activeContext?.activeSessionId || null;
  const room = useTechnicalInterviewSession(resolvedSessionId);
  const interviewLanguage = (room.session?.language
    || activeContext?.campaign?.language
    || setupDraft?.language) === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => (
    translate(key, { ...options, lng: interviewLanguage })
  ), [interviewLanguage, translate]);
  const recorderT = useCallback((key, options = {}) => (
    t(TECHNICAL_RECORDER_COPY[key] || `room.${key}`, options)
  ), [t]);
  const {
    error: roomLoadError,
    isLoading: isRoomLoading,
    startSession: startRoomSession,
  } = room;
  const submitMutation = useSubmitTechnicalAnswer(resolvedSessionId);
  const recorder = useTechnicalRecorder(interviewLanguage);
  const transcriptLedger = useTechnicalInterviewTranscript(resolvedSessionId);
  const {
    items: transcriptItems,
    markCandidateError,
    markCandidateFinal,
    markCandidateProcessing,
    syncCandidate,
    syncQuestion,
    syncServerTranscript,
  } = transcriptLedger;

  const {
    mode,
    strategy,
    remainingSeconds,
    stopTimer,
    handleQuestionAudioEnded,
    handleTimerExpired,
  } = useInterviewStrategy(
    room.session?.mode || activeContext?.campaign?.mode || setupDraft?.mode
  );

  const handleSubmitRef = useRef(null);

  const handleAudioEnded = useCallback(() => {
    handleQuestionAudioEnded({
      startRecording: recorder.startRecording,
      stopRecording: recorder.stopRecording,
      submitAnswer: () => handleSubmitRef.current?.(),
    });
  }, [handleQuestionAudioEnded, recorder.startRecording, recorder.stopRecording]);

  const questionAudio = useQuestionAudio({
    question: room.currentQuestion,
    sessionId: resolvedSessionId,
    language: interviewLanguage,
    onEnded: handleAudioEnded,
    forceAutoPlay: strategy.forceAutoPlay,
  });
  const setRecorderTranscript = recorder.setTranscript;
  const [localError, setLocalError] = useState(null);
  const [activeDraftAttempt, setActiveDraftAttempt] = useState(null);
  const [isCompleting, setIsCompleting] = useState(false);
  const [isEndConfirmOpen, setIsEndConfirmOpen] = useState(false);
  const [isTranscriptOpen, setIsTranscriptOpen] = useState(getDefaultTranscriptVisibility);

  const status = getTechnicalSessionStatus(room.session);
  const attemptId = room.currentQuestion?.attemptId || null;

  // ── Pre-Generation: Kích hoạt chọn trước bài thi Coding khi câu hỏi Technical #1 hiển thị ──
  const codingSessionId = useMemo(() => {
    const sessions = activeContext?.campaign?.sessions || [];
    const codingSession = sessions.find(
      (s) => (s.interviewRoundType === 'Code' || s.interviewRoundType === 'Coding')
        && (s.status === 'Pending' || s.status === 'Active'),
    );
    return codingSession?.interviewSessionId || null;
  }, [activeContext?.campaign?.sessions]);

  const codingPreGenRef = useRef(false);
  useEffect(() => {
    if (!codingPreGenRef.current && codingSessionId && room.currentQuestion) {
      codingPreGenRef.current = true;
      codingService.getQuestions(codingSessionId).catch(() => {});
    }
  }, [codingSessionId, room.currentQuestion]);
  const processingDraft = status === TechnicalSessionStatus.EVALUATING
    ? readTechnicalInterviewSessionDraft(resolvedSessionId)
    : null;
  const visibleTranscript = recorder.transcript || processingDraft?.transcript || '';

  const closeTranscript = useCallback(() => {
    setIsTranscriptOpen(false);
  }, []);

  const openTranscript = useCallback(() => setIsTranscriptOpen(true), []);

  useEffect(() => {
    if (status === TechnicalSessionStatus.COMPLETED && resolvedSessionId) {
      if (mode === InterviewMode.REAL) {
        const navigateToNextRound = async () => {
          const campaignId = room.session?.interviewCampaignId
            || activeContext?.campaign?.interviewCampaignId;
          if (!campaignId) {
            navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
            return;
          }
          try {
            const campaign = await interviewSessionService.getCampaign(campaignId);
            const nextSession = getNextOpenSession(campaign, resolvedSessionId);
            saveActiveInterviewContext({
              campaign,
              activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
              configurationKey: activeContext?.configurationKey || null,
            });
            if (nextSession) {
              const targetPath = nextSession.interviewRoundType === 'Coding' || nextSession.interviewRoundType === 'Code'
                ? getCodingInterviewRoomPath(nextSession.interviewSessionId)
                : getInterviewRoomPath(nextSession.interviewSessionId);
              navigate(targetPath, { replace: true });
              return;
            }
          } catch {
            // Fallback
          }
          navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
        };
        navigateToNextRound();
      } else {
        navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
      }
    }
  }, [activeContext, mode, resolvedSessionId, room.session?.interviewCampaignId, status]);

  useEffect(() => {
    syncQuestion(room.currentQuestion);
  }, [room.currentQuestion, syncQuestion]);

  useEffect(() => {
    syncServerTranscript(room.session?.transcript);
  }, [room.session?.transcript, syncServerTranscript]);

  useEffect(() => {
    if (!attemptId || !recorder.transcript.trim()) return;
    const transcriptStatus = recorder.sttError
      ? TechnicalTranscriptItemStatus.ERROR
      : recorder.sttStatus === SttStatus.PROCESSING
        ? TechnicalTranscriptItemStatus.PROCESSING
        : TechnicalTranscriptItemStatus.DRAFT;
    syncCandidate(attemptId, recorder.transcript, transcriptStatus);
  }, [
    attemptId,
    recorder.sttError,
    recorder.sttStatus,
    recorder.transcript,
    syncCandidate,
  ]);

  useEffect(() => {
    if (!processingDraft?.attemptId || !processingDraft.transcript) return;
    syncCandidate(
      processingDraft.attemptId,
      processingDraft.transcript,
      TechnicalTranscriptItemStatus.PROCESSING,
    );
  }, [processingDraft?.attemptId, processingDraft?.transcript, syncCandidate]);

  useEffect(() => {
    const sessionIsActive = Boolean(status)
      && status !== TechnicalSessionStatus.COMPLETED
      && status !== TechnicalSessionStatus.FAILED;
    if (!sessionIsActive) return undefined;
    const warnBeforeUnload = (event) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warnBeforeUnload);
    return () => window.removeEventListener('beforeunload', warnBeforeUnload);
  }, [status]);

  useEffect(() => {
    if (isRoomLoading || roomLoadError || status !== TechnicalSessionStatus.CREATED) return;
    startRoomSession()
      .then(() => setLocalError(null))
      .catch(setLocalError);
  }, [isRoomLoading, roomLoadError, startRoomSession, status]);

  useEffect(() => {
    if (!resolvedSessionId || !attemptId) {
      setActiveDraftAttempt(null);
      return;
    }
    const draft = readTechnicalInterviewDraft(resolvedSessionId, attemptId);
    setRecorderTranscript(draft);
    setActiveDraftAttempt(attemptId);
  }, [attemptId, resolvedSessionId, setRecorderTranscript]);

  useEffect(() => {
    if (!resolvedSessionId || !attemptId || activeDraftAttempt !== attemptId) return;
    saveTechnicalInterviewDraft(resolvedSessionId, attemptId, recorder.transcript);
  }, [activeDraftAttempt, attemptId, recorder.transcript, resolvedSessionId]);

  useEffect(() => {
    if (!resolvedSessionId || !attemptId || room.isProcessing) return;
    clearStaleTechnicalInterviewDrafts(resolvedSessionId, attemptId);
  }, [attemptId, resolvedSessionId, room.isProcessing]);

  const getErrorMessage = useCallback((error) => (
    error?.messageKey
      ? t(error.messageKey)
      : t(getTechnicalInterviewErrorKey(error), { defaultValue: t('errors.UNKNOWN_ERROR') })
  ), [t]);

  const handleStart = async () => {
    setLocalError(null);
    try {
      await room.startSession();
    } catch (error) {
      setLocalError(error);
    }
  };

  const handleSubmit = async () => {
    stopTimer();
    const transcript = recorder.transcript.trim();
    if (!transcript) {
      setLocalError({ code: 'TRANSCRIPT_REQUIRED' });
      return;
    }
    if (!attemptId) {
      setLocalError({ messageKey: 'room.attemptIdMissing' });
      return;
    }

    setLocalError(null);
    recorder.stopForSubmission();
    markCandidateProcessing(attemptId, transcript);
    room.markProcessing(attemptId);
    try {
      const response = await submitMutation.submitAnswer({
        attemptId,
        transcript,
        audioId: recorder.audioId || undefined,
      });
      if (!response) return;

      markCandidateFinal(attemptId, transcript);
      clearTechnicalInterviewDraft(resolvedSessionId, attemptId);
      recorder.reset();
      room.applyAnswerResponse(response);
      const nextStatus = getTechnicalSessionStatus(response?.session) || response?.sessionStatus;
      if (nextStatus === TechnicalSessionStatus.COMPLETED) {
        setIsCompleting(true);
        setTimeout(() => {
          navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
        }, 1200);
        return;
      }
      if (!response.nextQuestion && !response.currentQuestion && !response.question) await room.reload();
    } catch (error) {
      const recovery = await room.reconcileAfterSubmission(attemptId);
      if (recovery.state === 'ACCEPTED_COMPLETED') {
        markCandidateFinal(attemptId, transcript);
        clearTechnicalInterviewDraft(resolvedSessionId, attemptId);
        recorder.reset();
        setIsCompleting(true);
        setTimeout(() => {
          navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
        }, 1200);
        return;
      }
      if (recovery.state === 'ACCEPTED_NEXT_QUESTION') {
        markCandidateFinal(attemptId, transcript);
        clearTechnicalInterviewDraft(resolvedSessionId, attemptId);
        recorder.reset();
        return;
      }
      if (recovery.state === 'PROCESSING') {
        markCandidateProcessing(attemptId, transcript);
        setLocalError(null);
        return;
      }
      markCandidateError(attemptId, transcript);
      setLocalError(error);
    }
  };

  useEffect(() => {
    handleSubmitRef.current = handleSubmit;
  });

  const autoSubmittingRef = useRef(false);

  useEffect(() => {
    if (
      recorder.recordingStatus === RecordingStatus.READY
      && recorder.transcript.trim()
      && !submitMutation.isSubmitting
      && !autoSubmittingRef.current
    ) {
      autoSubmittingRef.current = true;
      handleSubmit().finally(() => {
        autoSubmittingRef.current = false;
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recorder.recordingStatus, recorder.transcript, submitMutation.isSubmitting]);

  const handleCompleteEarly = async () => {
    if (!resolvedSessionId || isCompleting) return;
    setIsCompleting(true);
    setLocalError(null);
    try {
      try {
        const response = await technicalInterviewApi.completeSession(resolvedSessionId);
        room.applyAnswerResponse(response);
        setIsEndConfirmOpen(false);
        navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
      } catch {
        // Fallback to force end session and navigate to next round / result
        setIsEndConfirmOpen(false);
        await handleForceEndSession();
      }
    } catch (error) {
      setLocalError(error);
    } finally {
      setIsCompleting(false);
    }
  };

  const handleForceEndSession = async () => {
    if (!resolvedSessionId || isCompleting) return;
    setIsCompleting(true);
    setLocalError(null);
    try {
      const campaign = await interviewSessionService.completeSession(resolvedSessionId);
      const nextSession = getNextOpenSession(campaign, resolvedSessionId);
      saveActiveInterviewContext({
        campaign,
        activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
        configurationKey: activeContext?.configurationKey || null,
      });
      navigate(nextSession?.status === 'Active'
        ? getInterviewRoomPath(nextSession.interviewSessionId)
        : USER_ROUTES.INTERVIEW_SETUP, { replace: true });
    } catch (error) {
      setLocalError(error);
    } finally {
      setIsCompleting(false);
    }
  };

  let content;
  if (!resolvedSessionId) {
    content = (
      <TechnicalInterviewErrorState
        title={t('room.openFailed')}
        message={t('room.sessionIdMissing')}
        onBack={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
        backLabel={t('room.backToSetup')}
      />
    );
  } else if (room.isLoading) {
    content = (
      <InterviewInitializationLoading phase={TechnicalInterviewFlowStatus.INITIALIZING_SESSION} t={t} />
    );
  } else if (room.error) {
    const canRetryQuestionGeneration = room.error?.code === 'QUESTION_GENERATION_TIMEOUT'
      || status === TechnicalSessionStatus.SELECTING_QUESTION
      || status === TechnicalSessionStatus.FAILED;
    content = (
      <TechnicalInterviewErrorState
        title={t('room.openFailed')}
        message={getErrorMessage(localError || room.error)}
        onRetry={canRetryQuestionGeneration ? handleStart : room.reload}
        onBack={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
        onEnd={handleForceEndSession}
        retryLabel={t('common.retry')}
        backLabel={t('room.backToSetup')}
        endLabel={isCompleting ? t('room.ending') : t('room.endEarly')}
      />
    );
  } else if (status === TechnicalSessionStatus.CREATED
    || status === TechnicalSessionStatus.SELECTING_QUESTION) {
    content = (
      <InterviewInitializationLoading phase={TechnicalInterviewFlowStatus.GENERATING_QUESTION} t={t} />
    );
  } else if (status === TechnicalSessionStatus.FAILED) {
    content = (
      <TechnicalInterviewErrorState
        title={t('room.failedTitle')}
        message={localError ? getErrorMessage(localError) : t('room.failedDescription')}
        onRetry={handleStart}
        retryLabel={t('common.retry')}
        onBack={() => navigate(USER_ROUTES.DASHBOARD)}
        backLabel={t('room.backToDashboard')}
        onEnd={handleForceEndSession}
        endLabel={isCompleting ? t('room.ending') : t('room.endEarly')}
      />
    );
  } else if ((status === TechnicalSessionStatus.EVALUATING || submitMutation.isSubmitting)
    && !room.currentQuestion) {
    content = (
      <TechnicalEvaluationState
        finalizing={false}
        t={t}
      />
    );
  } else if (room.currentQuestion) {
    const numericCurrent = Number(room.currentQuestion.mainQuestionIndex
      || ((room.session?.completedMainQuestionCount ?? 0) + 1));
    const numericTotal = Number(room.currentQuestion.totalMainQuestions
      || room.session?.lockedMainQuestions?.length
      || room.session?.targetMainQuestionCount);
    const hasProgress = Number.isFinite(numericCurrent)
      && Number.isFinite(numericTotal)
      && numericTotal > 0;
    const progressPercentage = hasProgress
      ? Math.min(100, Math.max(0, (numericCurrent / numericTotal) * 100))
      : 0;
    const isEvaluating = status === TechnicalSessionStatus.EVALUATING
      || submitMutation.isSubmitting;
    const questionType = room.currentQuestion.questionType;
    const contextKey = questionType === 'CLARIFICATION'
      ? 'room.clarificationContext'
      : questionType === 'FOLLOW_UP'
        ? 'room.followUpContext'
        : null;

    content = (
      <section className="behavior-stage technical-behavior-stage" aria-label={t('room.title')}>
        <header className="behavior-stage__topbar">
          <div className="behavior-stage__session">
            <span>{t('room.title')}</span>
            <strong>{room.session?.jobRole || setupDraft?.jobRole || 'AI-SPEIS'}</strong>
            <button
              type="button"
              className="technical-transcript-toggle technical-transcript-toggle--topbar"
              onClick={isTranscriptOpen ? closeTranscript : openTranscript}
              aria-expanded={isTranscriptOpen}
            >
              <FileText size={15} aria-hidden="true" />
              {t('room.transcript')}
            </button>
          </div>
          <div className="behavior-stage__top-actions">
            <button
              type="button"
              className="behavior-stage__end"
              onClick={() => setIsEndConfirmOpen(true)}
              disabled={isCompleting}
            >
              {isCompleting ? t('room.ending') : t('room.endEarly')}
            </button>
          </div>
        </header>

        <div
          className="behavior-stage__progress"
          role="progressbar"
          aria-label={hasProgress
            ? t('room.questionProgress', { current: numericCurrent, total: numericTotal })
            : t('room.questionProgressUnknown')}
          aria-valuemin={0}
          aria-valuemax={hasProgress ? numericTotal : undefined}
          aria-valuenow={hasProgress ? numericCurrent : undefined}
        >
          <span style={{ width: `${progressPercentage}%` }} />
        </div>

        <section className="behavior-question" aria-labelledby="technical-stage-question">
          <div className="behavior-question__eyebrow">
            {t(`room.questionTypes.${questionType}`, { defaultValue: t('room.interviewerAsks') })}
          </div>
          <h1 id="technical-stage-question" tabIndex={-1}>{room.currentQuestion.content}</h1>
          {(room.currentQuestion.skill || room.currentQuestion.difficulty) ? (
            <p className="behavior-question__hint">
              {[room.currentQuestion.skill, room.currentQuestion.difficulty].filter(Boolean).join(' / ')}
            </p>
          ) : null}
          {contextKey ? (
            <p className="technical-behavior-stage__context">
              {t(contextKey, { current: numericCurrent })}
            </p>
          ) : null}
          <div
            className={`behavior-interviewer ${recorder.recordingStatus === RecordingStatus.RECORDING ? 'behavior-interviewer--listening' : ''}`}
            aria-hidden="true"
          >
            <span className="behavior-interviewer__ring" />
            <span className="behavior-interviewer__ring" />
            <span className="behavior-interviewer__core"><Bot size={28} /></span>
          </div>
          {strategy.showAudioControls ? (
            <div className="behavior-audio-controls" aria-label={t('room.playQuestionAudio')}>
              {questionAudio.status === QuestionAudioStatus.LOADING
                || questionAudio.status === QuestionAudioStatus.IDLE ? (
                  <Loader2 size={18} className="behavior-spin" />
                ) : null}
              {questionAudio.status === QuestionAudioStatus.READY ? (
                <>
                  <button
                    type="button"
                    onClick={questionAudio.isPlaying ? questionAudio.pause : questionAudio.play}
                    disabled={recorder.recordingStatus === RecordingStatus.RECORDING
                      || recorder.sttStatus === SttStatus.PROCESSING}
                  >
                    {questionAudio.isPlaying ? <Pause size={17} /> : <Volume2 size={17} />}
                    {questionAudio.isPlaying ? t('room.pauseAudio') : t('room.playAudio')}
                  </button>
                  {strategy.allowReplayAudio ? (
                    <button type="button" onClick={questionAudio.replay} aria-label={t('room.replayQuestionAudio')}>
                      <RotateCcw size={17} />
                    </button>
                  ) : null}
                </>
              ) : null}
              {questionAudio.status === QuestionAudioStatus.ERROR ? (
                <button type="button" onClick={questionAudio.retry}>
                  <RefreshCw size={17} />{t('room.retryAudio')}
                </button>
              ) : null}
              <button
                type="button"
                onClick={questionAudio.toggleAutoPlay}
                aria-pressed={questionAudio.autoPlay}
              >
                {t('room.autoPlayAudio')}: {questionAudio.autoPlay ? t('room.on') : t('room.off')}
              </button>
            </div>
          ) : null}
        </section>

        <footer className="behavior-stage__controls">
          {localError ? (
            <div className="behavior-inline-error" role="alert">
              <AlertCircle size={18} />
              <span>{getErrorMessage(localError)}</span>
            </div>
          ) : null}
          {isEvaluating ? (
            <div className="behavior-evaluating" role="status" aria-live="polite">
              <Loader2 size={22} className="behavior-spin" />
              <div>
                <strong>{t('room.evaluatingTitle')}</strong>
                <p>{t('room.evaluatingDescription')}</p>
              </div>
            </div>
          ) : (
            <BehavioralRecorderControls
              recorder={recorder}
              disabled={questionAudio.isPlaying}
              isSubmitting={submitMutation.isSubmitting}
              timeLimitSeconds={strategy.defaultCountdownSeconds || room.currentQuestion.timeLimitSeconds}
              remainingSeconds={remainingSeconds}
              strategy={strategy}
              isAudioPlaying={questionAudio.isPlaying}
              onSubmit={handleSubmit}
              t={recorderT}
            />
          )}
        </footer>
      </section>
    );
  } else {
    content = (
      <TechnicalInterviewErrorState
        title={t('room.questionUnavailableTitle')}
        message={localError
          ? getErrorMessage(localError)
          : room.questionError?.status === 404
          ? t('room.questionApiUnavailable')
          : room.questionError
            ? getErrorMessage(room.questionError)
            : t('room.questionUnavailableDescription')}
        onRetry={room.reload}
        retryLabel={t('common.retry')}
        onEnd={handleForceEndSession}
        endLabel={isCompleting ? t('room.ending') : t('room.endEarly')}
      />
    );
  }

  return (
    <InterviewRoomShell
      language={interviewLanguage}
      mainFlush
      isTranscriptOpen={isTranscriptOpen}
      onCloseTranscript={closeTranscript}
      transcriptCloseLabel={t('room.closeTranscript')}
      transcriptLabel={t('room.transcript')}
      transcript={(
        <TechnicalTranscriptPanel
          items={transcriptItems}
          recorder={recorder}
          currentTranscript={visibleTranscript}
          hasActiveAttempt={Boolean(attemptId || processingDraft?.attemptId)}
          transcriptEditable
          disabled={status === TechnicalSessionStatus.EVALUATING || submitMutation.isSubmitting}
          isOpen={isTranscriptOpen}
          onClose={closeTranscript}
          t={t}
        />
      )}
      dialog={(
        <>
          <EndSessionConfirmDialog
            action={isEndConfirmOpen ? 'session' : null}
            isSubmitting={isCompleting}
            onConfirm={handleCompleteEarly}
            onCancel={() => setIsEndConfirmOpen(false)}
            t={t}
          />
          <EvaluatingAnalysisModal isOpen={isCompleting} />
        </>
      )}
    >
      {content}
    </InterviewRoomShell>
  );
}

export default TechnicalInterviewPage;
