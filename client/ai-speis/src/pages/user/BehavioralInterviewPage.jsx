import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  Bot,
  FileText,
  Flag,
  Loader2,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  Volume2,
} from 'lucide-react';
import BehavioralCompletion from '../../components/behavioralInterview/BehavioralCompletion';
import FeedbackModal from '../../components/feedback/FeedbackModal';
import BehavioralRecorderControls from '../../components/behavioralInterview/BehavioralRecorderControls';
import BehavioralRoomDialog from '../../components/behavioralInterview/BehavioralRoomDialog';
import InterviewRoomShell from '../../components/interviewRoom/InterviewRoomShell';
import EvaluatingAnalysisModal from '../../components/interviewRoom/EvaluatingAnalysisModal';
import InterviewRoomState from '../../components/interviewRoom/InterviewRoomState';
import InterviewRoomTranscriptPanel from '../../components/interviewRoom/InterviewRoomTranscriptPanel';
import {
  BehavioralFlowPhase,
} from '../../features/behavioralInterview/behavioralInterview.types';
import { RecordingStatus } from '../../features/technicalInterview/technicalInterview.types';
import useBehavioralInterviewSession from '../../features/behavioralInterview/useBehavioralInterviewSession';
import useQuestionAudio from '../../features/technicalInterview/useQuestionAudio';
import useTechnicalRecorder from '../../features/technicalInterview/useTechnicalRecorder';
import { navigate } from '../../routes/navigation';
import { getCampaignResultPath, getInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';
import { submitEvaluationFeedback } from '../../services/aiEvaluationFeedbackApi';
import interviewSessionService from '../../services/InterviewSessionService';
import notify from '../../utils/notification';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  getNextOpenSession,
  saveActiveInterviewContext,
  resolveNextInterviewStage,
  InterviewStage,
  QuestionPreparationState,
} from '../../utils/interviewContext';
import useInterviewStrategy from '../../features/interviewStrategy/useInterviewStrategy';
import { InterviewMode } from '../../features/interviewStrategy/InterviewMode';
import useTechnicalPreGenerator from '../../features/technicalInterview/useTechnicalPreGenerator';
import '../../styles/user/BehavioralInterview.css';
import '../../styles/user/TechnicalInterview.css';

const ACTIVE_PHASES = new Set([
  BehavioralFlowPhase.READY_TO_ANSWER,
  BehavioralFlowPhase.SUBMITTING_ANSWER,
  BehavioralFlowPhase.EVALUATING_ANSWER,
  BehavioralFlowPhase.RECOVERABLE_ERROR,
]);

const PHASE_COPY = {
  [BehavioralFlowPhase.CHECKING_SESSION]: 'checkingSession',
  [BehavioralFlowPhase.INITIALIZING]: 'initializingInterview',
  [BehavioralFlowPhase.LOADING_QUESTION]: 'preparingQuestion',
  [BehavioralFlowPhase.SUBMITTING_ANSWER]: 'submittingAnswer',
  [BehavioralFlowPhase.EVALUATING_ANSWER]: 'evaluatingAnswer',
  [BehavioralFlowPhase.COMPLETING]: 'completingInterview',
};

function BehavioralInterviewPage({ sessionId }) {
  const initialContext = useMemo(() => getActiveInterviewContext(), []);
  const setupDraft = useMemo(() => getInterviewSetupDraft(), []);
  const resolvedSessionId = sessionId || initialContext?.activeSessionId || null;
  const room = useBehavioralInterviewSession(resolvedSessionId);
  const interviewLanguage = (room.session?.language
    || initialContext?.campaign?.language
    || setupDraft?.language) === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => translate(`behavioralRoom.${key}`, {
    ...options,
    lng: interviewLanguage,
    defaultValue: options.defaultValue || key,
  }), [interviewLanguage, translate]);
  const tf = useCallback((key, options = {}) => translate(key, {
    ...options,
    lng: interviewLanguage,
  }), [interviewLanguage, translate]);

  const recorder = useTechnicalRecorder(interviewLanguage);

  const {
    mode,
    strategy,
    remainingSeconds,
    stopTimer,
    handleQuestionAudioEnded,
  } = useInterviewStrategy(
    room.session?.mode || initialContext?.campaign?.mode || setupDraft?.mode
  );

  // ── Pre-Generation: tìm Technical session ID từ campaign ──
  const technicalSessionId = useMemo(() => {
    const sessions = initialContext?.campaign?.sessions || [];
    const techSession = sessions.find(
      (s) => s.interviewRoundType === 'Technical'
        && (s.status === 'Pending' || s.status === 'Active'),
    );
    return techSession?.interviewSessionId || null;
  }, [initialContext?.campaign?.sessions]);

  const preGenerator = useTechnicalPreGenerator();

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
    preferenceKey: 'ai-speis:behavioral-interview:auto-play-question',
    onEnded: handleAudioEnded,
    forceAutoPlay: strategy.forceAutoPlay,
  });
  const resetRecorder = recorder.reset;
  const setRecorderTranscript = recorder.setTranscript;
  const cleanupRecorder = recorder.cleanup;
  const pauseQuestionAudio = questionAudio.pause;
  const [transcriptOpen, setTranscriptOpen] = useState(() => (
    typeof window === 'undefined' || window.matchMedia('(min-width: 1025px)').matches
  ));
  const [dialog, setDialog] = useState(null);
  const [pendingNavigation, setPendingNavigation] = useState(null);
  const [localError, setLocalError] = useState(null);
  const [feedbackRetryError, setFeedbackRetryError] = useState(null);
  const [latestCampaign, setLatestCampaign] = useState(initialContext?.campaign || null);
  const [isFeedbackModalOpen, setIsFeedbackModalOpen] = useState(false);
  const [isSubmittingFeedback, setIsSubmittingFeedback] = useState(false);
  const previousQuestionRef = useRef(null);
  const hydratingDraftRef = useRef(false);

  const phaseIsActive = ACTIVE_PHASES.has(room.phase);
  const isSubmitting = room.phase === BehavioralFlowPhase.SUBMITTING_ANSWER
    || room.phase === BehavioralFlowPhase.EVALUATING_ANSWER;
  const error = localError || room.error;
  const nextRoundSession = getNextOpenSession(latestCampaign, resolvedSessionId);
  const completedCampaignId = latestCampaign?.interviewCampaignId
    || room.generalSession?.interviewCampaignId
    || initialContext?.campaign?.interviewCampaignId;
  const campaignResultPath = room.phase === BehavioralFlowPhase.COMPLETED
    && !nextRoundSession
    && completedCampaignId
    ? getCampaignResultPath(completedCampaignId)
    : USER_ROUTES.DASHBOARD;
  const transcriptItems = useMemo(() => {
    const restored = room.transcriptMessages.map((message) => ({
      ...message,
      role: message.speaker === 'candidate' ? 'CANDIDATE' : 'INTERVIEWER',
      statusLabel: message.questionType
        ? t(`questionType.${message.questionType}`, { defaultValue: '' })
        : '',
    }));
    const draftText = recorder.transcript.trim();
    const draft = draftText
      ? [{
        id: `draft-${room.currentQuestion?.sessionQuestionId || 'current'}`,
        role: 'CANDIDATE',
        content: draftText,
        statusLabel: isSubmitting ? (t('submitting', { defaultValue: 'Đang gửi...' })) : t('draft'),
      }]
      : [];
    return [...restored, ...draft];
  }, [isSubmitting, recorder.transcript, room.currentQuestion?.sessionQuestionId, room.transcriptMessages, t]);
  const completionResult = room.completionResult;
  const hasCompletionEvaluation = Boolean(
    completionResult
    && (
      completionResult.overallScore != null
      || completionResult.summary?.overallBehavioralAssessment
      || completionResult.summary?.executiveSummary
      || completionResult.mainQuestions?.length
    )
  );
  const behavioralEvaluationId = completionResult?.evaluationId
    ?? completionResult?.behavioralEvaluationId
    ?? completionResult?.resultId
    ?? null;
  const behavioralFeedbackQuestions = useMemo(() => (
    Array.isArray(completionResult?.mainQuestions)
      ? completionResult.mainQuestions.map((question, index) => ({
        id: question?.sessionQuestionId ?? question?.mainQuestionIndex ?? index + 1,
        label: tf('feedback.questionItem', { index: question?.mainQuestionIndex || index + 1 }),
      }))
      : []
  ), [completionResult?.mainQuestions, tf]);

  useEffect(() => {
    const currentId = room.currentQuestion?.sessionQuestionId || null;
    if (currentId && previousQuestionRef.current !== currentId) {
      if (previousQuestionRef.current) resetRecorder();
      try {
        const savedDraft = localStorage.getItem(
          `behavioral-interview:${resolvedSessionId}:${currentId}:draft`,
        );
        if (savedDraft) {
          hydratingDraftRef.current = true;
          setRecorderTranscript(savedDraft);
        }
      } catch {
        // Draft persistence is best-effort; server-owned submitted transcript remains authoritative.
      }
    }
    previousQuestionRef.current = currentId;
  }, [resetRecorder, resolvedSessionId, room.currentQuestion?.sessionQuestionId, setRecorderTranscript]);

  useEffect(() => {
    const currentId = room.currentQuestion?.sessionQuestionId;
    if (!resolvedSessionId || !currentId) return;
    if (hydratingDraftRef.current) {
      hydratingDraftRef.current = false;
      return;
    }
    const draftKey = `behavioral-interview:${resolvedSessionId}:${currentId}:draft`;
    try {
      if (recorder.transcript.trim()) localStorage.setItem(draftKey, recorder.transcript);
      else localStorage.removeItem(draftKey);
    } catch {
      // Storage quota or privacy settings must not interrupt the interview.
    }
  }, [recorder.transcript, resolvedSessionId, room.currentQuestion?.sessionQuestionId]);

  useEffect(() => {
    if (!phaseIsActive) return undefined;
    const warnBeforeUnload = (event) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warnBeforeUnload);
    return () => window.removeEventListener('beforeunload', warnBeforeUnload);
  }, [phaseIsActive]);

  const refreshCompletedCampaign = useCallback(async () => {
    const campaignId = room.generalSession?.interviewCampaignId
      || initialContext?.campaign?.interviewCampaignId;
    if (!campaignId) return;
    setLocalError(null);
    try {
      const campaign = await interviewSessionService.getCampaign(campaignId);
      const nextActiveSession = getNextOpenSession(campaign, resolvedSessionId);
      saveActiveInterviewContext({
        campaign,
        activeSessionId: nextActiveSession?.status === 'Active'
          ? nextActiveSession.interviewSessionId
          : null,
        configurationKey: initialContext?.configurationKey || null,
      });
      setLatestCampaign(campaign);
    } catch (campaignError) {
      setLatestCampaign(initialContext?.campaign || null);
      setLocalError(campaignError);
    }
  }, [initialContext, resolvedSessionId, room.generalSession?.interviewCampaignId]);

  const isEndingAllRef = useRef(false);

  useEffect(() => {
    if (room.phase !== BehavioralFlowPhase.COMPLETED) return;
    resetRecorder();
    pauseQuestionAudio();

    if (mode === InterviewMode.REAL && !isEndingAllRef.current) {
      const navigateToNextRound = async () => {
        const campaignId = room.session?.interviewCampaignId
          || initialContext?.campaign?.interviewCampaignId;
        if (!campaignId) return;
        try {
          const campaign = await interviewSessionService.getCampaign(campaignId);
          const stageResolution = resolveNextInterviewStage({
            campaign,
            currentSessionId: resolvedSessionId,
            technicalPrepState: preGenerator.isCompleted ? QuestionPreparationState.READY : QuestionPreparationState.PREPARING,
          });
          const nextSession = getNextOpenSession(campaign, resolvedSessionId);
          saveActiveInterviewContext({
            campaign,
            activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
            configurationKey: initialContext?.configurationKey || null,
          });
          if (stageResolution.nextStage === InterviewStage.TECHNICAL && stageResolution.targetSessionId) {
            navigate(getInterviewRoomPath(stageResolution.targetSessionId), { replace: true });
            return;
          }
        } catch {
          // Fallback
        }
        refreshCompletedCampaign();
      };
      navigateToNextRound();
    } else {
      refreshCompletedCampaign();
    }
  }, [pauseQuestionAudio, refreshCompletedCampaign, resetRecorder, room.phase, mode, resolvedSessionId, initialContext, room.generalSession?.interviewCampaignId, room.session?.interviewCampaignId, preGenerator.isCompleted]);

  // ── Pre-Generation: Kích hoạt tạo trước Technical khi câu hỏi 1 xuất hiện ──
  const preGenTriggeredRef = useRef(false);
  useEffect(() => {
    if (
      !preGenTriggeredRef.current
      && technicalSessionId
      && room.phase === BehavioralFlowPhase.READY_TO_ANSWER
      && room.currentQuestion
    ) {
      preGenTriggeredRef.current = true;
      preGenerator.trigger(technicalSessionId);
    }
  }, [room.phase, room.currentQuestion, technicalSessionId, preGenerator]);

  const getErrorMessage = useCallback((requestError) => {
    const code = requestError?.code || 'UNKNOWN_ERROR';
    return t(`error.${code}`, { defaultValue: t('error.UNKNOWN_ERROR') });
  }, [t]);

  const handleSubmit = useCallback(async () => {
    stopTimer();
    const transcript = recorder.transcript.trim();
    if (!transcript) {
      setLocalError({ code: 'TRANSCRIPT_REQUIRED' });
      return;
    }
    setLocalError(null);
    recorder.stopForSubmission();
    try {
      const result = await room.submitAnswer({
        transcript,
        audioId: recorder.audioId || undefined,
        durationSeconds: recorder.elapsedSeconds,
      });
      if (result?.accepted) {
        try {
          localStorage.removeItem(
            `behavioral-interview:${resolvedSessionId}:${room.currentQuestion?.sessionQuestionId}:draft`,
          );
        } catch {
          // Best-effort cleanup only.
        }
        recorder.reset();
      }
    } catch (submitError) {
      setLocalError(submitError);
    }
  }, [recorder, room, stopTimer, resolvedSessionId]);

  useEffect(() => {
    handleSubmitRef.current = handleSubmit;
  });

  const autoSubmittingRef = useRef(false);

  useEffect(() => {
    if (
      recorder.recordingStatus === RecordingStatus.READY
      && recorder.transcript.trim()
      && !isSubmitting
      && !autoSubmittingRef.current
    ) {
      autoSubmittingRef.current = true;
      handleSubmit().finally(() => {
        autoSubmittingRef.current = false;
      });
    }
  }, [handleSubmit, isSubmitting, recorder.recordingStatus, recorder.transcript]);

  const requestNavigation = useCallback((path) => {
    if (!phaseIsActive) return true;
    setPendingNavigation(path);
    setDialog({ type: 'leave' });
    return false;
  }, [phaseIsActive]);

  const handleDialogConfirm = async (action = 'endRound') => {
    if (dialog?.type === 'leave') {
      cleanupRecorder();
      pauseQuestionAudio();
      preGenerator.cancel(technicalSessionId);
      const target = pendingNavigation || USER_ROUTES.INTERVIEW_SETUP;
      setDialog(null);
      setPendingNavigation(null);
      navigate(target);
      return;
    }

    if (dialog?.type === 'end') {
      setLocalError(null);
      if (action === 'endAll') {
        isEndingAllRef.current = true;
      }
      try {
        await room.completeInterview();
        setDialog(null);
        if (action === 'endAll') {
          const campaignId = room.session?.interviewCampaignId
            || room.generalSession?.interviewCampaignId
            || initialContext?.campaign?.interviewCampaignId;
          if (campaignId) {
            try {
              await interviewSessionService.finishCampaign(campaignId);
            } catch {
              // best effort
            }
            navigate(getCampaignResultPath(campaignId), { replace: true });
          }
        }
      } catch (completionError) {
        isEndingAllRef.current = false;
        setDialog(null);
        setLocalError(completionError);
      }
    }
  };

  const handleContinue = () => {
    if (!nextRoundSession) return;
    const currentContext = getActiveInterviewContext();
    if (currentContext?.campaign) {
      saveActiveInterviewContext({
        ...currentContext,
        activeSessionId: nextRoundSession.status === 'Active'
          ? nextRoundSession.interviewSessionId
          : null,
      });
    }
    navigate(nextRoundSession.status === 'Active'
      ? getInterviewRoomPath(nextRoundSession.interviewSessionId)
      : USER_ROUTES.DEVICE_CHECK);
  };

  const handleRetryFeedback = async () => {
    setFeedbackRetryError(null);
    try {
      await room.retryFeedback();
    } catch (retryError) {
      setFeedbackRetryError(retryError);
    }
  };

  const handleSubmitFeedback = async (payload) => {
    setIsSubmittingFeedback(true);
    try {
      await submitEvaluationFeedback(payload);
      setIsFeedbackModalOpen(false);
      notify.success(tf('feedback.toastSuccess'));
    } catch (submitError) {
      if (Number(submitError?.status) === 404) {
        notify.warning(tf('feedback.apiNotImplemented'));
      } else {
        notify.error(tf('feedback.toastError'));
      }
    } finally {
      setIsSubmittingFeedback(false);
    }
  };

  const renderLoading = () => {
    const copyKey = PHASE_COPY[room.phase] || 'preparingQuestion';
    return <InterviewRoomState title={t(copyKey)} description={t(`${copyKey}Description`)} />;
  };

  const renderFatalError = () => (
    <InterviewRoomState
      variant="error"
      title={t('openFailed')}
      description={getErrorMessage(error)}
      onRetry={room.reload}
      retryLabel={t('retry')}
      onBack={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
      backLabel={t('backToSetup')}
      onEnd={() => room.completeInterview().catch((completionError) => setLocalError(completionError))}
      endLabel={t('endInterview')}
    />
  );

  const renderConflict = () => (
    <section className="behavior-room-state behavior-room-state--conflict" role="alert">
      <span><AlertCircle size={34} /></span>
      <h1>{t('sessionConflictTitle')}</h1>
      <p>{t('sessionConflictDescription')}</p>
      <div>
        <button type="button" onClick={() => navigate(room.conflict?.resumePath)}>
          <Play size={18} />{t('resumeActiveSession')}
        </button>
        <button type="button" onClick={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}>
          <ArrowLeft size={18} />{t('backToSetup')}
        </button>
      </div>
    </section>
  );

  const renderCompletionRecovery = () => (
    <section className="behavior-room-state behavior-room-state--error" role="alert">
      <span><AlertCircle size={34} /></span>
      <h1>{t('recoveryTitle')}</h1>
      <p>{getErrorMessage(room.error)}</p>
      <div>
        <button type="button" onClick={() => room.completeInterview().catch(() => undefined)}>
          <RefreshCw size={18} />{t('retryCompletion')}
        </button>
        <button type="button" onClick={room.reload}>{t('reloadSession')}</button>
      </div>
    </section>
  );

  const questionType = room.currentQuestion?.questionType;

  return (
    <InterviewRoomShell
      language={interviewLanguage}
      mainFlush
      onBeforeNavigate={requestNavigation}
      isTranscriptOpen={transcriptOpen}
      onCloseTranscript={() => setTranscriptOpen(false)}
      transcriptCloseLabel={t('closeTranscript')}
      transcriptLabel={t('transcript')}
      transcript={(
        <InterviewRoomTranscriptPanel
          candidateLabel={t('candidate')}
          closeLabel={t('closeTranscript')}
          description={t('transcriptDescription')}
          emptyMessage={t('transcriptEmpty')}
          interviewerLabel={t('interviewer')}
          isOpen={transcriptOpen}
          items={transcriptItems}
          liveState={recorder.sttStatus === 'PROCESSING'
            ? { icon: Loader2, label: t('transcribing'), tone: 'processing', spin: true }
            : null}
          onClose={() => setTranscriptOpen(false)}
          title={t('transcriptTitle')}
        />
      )}
      dialog={(
        <>
          <BehavioralRoomDialog
            dialog={dialog}
            mode={mode}
            busy={room.phase === BehavioralFlowPhase.COMPLETING}
            onCancel={() => { setDialog(null); setPendingNavigation(null); }}
            onConfirm={handleDialogConfirm}
            t={t}
          />
          <EvaluatingAnalysisModal isOpen={room.phase === BehavioralFlowPhase.COMPLETING} />
        </>
      )}
    >
      <section className="behavior-stage" aria-label={t('behavioralInterview')}>
          {room.phase === BehavioralFlowPhase.COMPLETED ? (
            mode === InterviewMode.REAL ? (
              <div className="flex h-64 w-full flex-col items-center justify-center space-y-3">
                <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary-main border-t-transparent" />
                <p className="text-sm font-medium text-text-secondary">Preparing next interviewer...</p>
              </div>
            ) : (
              <div className="behavior-completion-stack">
                <BehavioralCompletion
                  result={room.completionResult}
                  answeredCount={room.completionResult?.mainQuestions?.length || room.session?.completedMainQuestionCount || 0}
                  hasNextRound={Boolean(nextRoundSession)}
                  onContinue={handleContinue}
                  onOverview={() => navigate(campaignResultPath)}
                  onRetryFeedback={handleRetryFeedback}
                  feedbackRetrying={room.feedbackRetrying}
                  feedbackError={feedbackRetryError}
                  t={t}
                />
                {hasCompletionEvaluation ? (
                  <div className="technical-feedback-report">
                    <button
                      type="button"
                      className="technical-report-button"
                      onClick={() => setIsFeedbackModalOpen(true)}
                      aria-label={tf('feedback.reportButton')}
                    >
                      <Flag size={16} aria-hidden="true" />
                      {tf('feedback.reportButton')}
                    </button>
                  </div>
                ) : null}
                {localError ? (
                  <div className="behavior-inline-error" role="alert">
                    <AlertCircle size={18} />
                    <span>{getErrorMessage(localError)}</span>
                    <button type="button" onClick={refreshCompletedCampaign}>{t('retry')}</button>
                  </div>
                ) : null}
              </div>
            )
          ) : room.phase === BehavioralFlowPhase.FATAL_ERROR ? (
            renderFatalError()
          ) : room.phase === BehavioralFlowPhase.SESSION_CONFLICT ? (
            renderConflict()
          ) : room.phase === BehavioralFlowPhase.RECOVERABLE_ERROR && !room.currentQuestion ? (
            renderCompletionRecovery()
          ) : room.isBusy && !room.currentQuestion ? (
            renderLoading()
          ) : room.currentQuestion ? (
            <>
              <header className="behavior-stage__topbar">
                <div className="behavior-stage__session">
                  <span>{t('behavioralInterview')}</span>
                  <strong>{room.session?.jobRole || setupDraft?.jobRole || t('interviewSession')}</strong>
                  <button
                    type="button"
                    className="technical-transcript-toggle technical-transcript-toggle--topbar"
                    onClick={() => setTranscriptOpen((open) => !open)}
                    aria-expanded={transcriptOpen}
                  >
                    <FileText size={15} aria-hidden="true" />
                    {t('transcript')}
                  </button>
                </div>
                <div className="behavior-stage__top-actions">
                  <button type="button" className="behavior-stage__end" onClick={() => setDialog({ type: 'end' })}>
                    {t('endInterview')}
                  </button>
                </div>
              </header>

              <div className="behavior-stage__progress" aria-label={t('progressLabel', {
                current: room.currentQuestion.mainQuestionIndex,
                total: room.currentQuestion.totalMainQuestions,
              })}>
                <span style={{ width: `${Math.min(100, (room.currentQuestion.mainQuestionIndex / Math.max(1, room.currentQuestion.totalMainQuestions)) * 100)}%` }} />
              </div>

              <section className="behavior-question" aria-labelledby="behavior-question-text">
                <div className="behavior-question__eyebrow">
                  {questionType ? t(`questionType.${questionType}`, { defaultValue: t('interviewerAsks') }) : t('interviewerAsks')}
                </div>
                <h1 id="behavior-question-text">{room.currentQuestion.content}</h1>
                {room.currentQuestion.hint && initialContext?.campaign?.mode === 'Practice' ? (
                  <p className="behavior-question__hint">{room.currentQuestion.hint}</p>
                ) : null}

                <div className={`behavior-interviewer ${recorder.recordingStatus === 'RECORDING' ? 'behavior-interviewer--listening' : ''}`} aria-hidden="true">
                  <span className="behavior-interviewer__ring" />
                  <span className="behavior-interviewer__ring" />
                  <span className="behavior-interviewer__core"><Bot size={28} /></span>
                </div>
                {strategy.showAudioControls ? (
                  <div className="behavior-audio-controls" aria-label={t('questionAudio')}>
                    {questionAudio.status === 'LOADING' ? <Loader2 size={18} className="behavior-spin" /> : null}
                    {questionAudio.status === 'READY' ? (
                      <>
                        <button type="button" onClick={questionAudio.isPlaying ? questionAudio.pause : questionAudio.play}>
                          {questionAudio.isPlaying ? <Pause size={17} /> : <Volume2 size={17} />}
                          {questionAudio.isPlaying ? t('pauseQuestion') : t('playQuestion')}
                        </button>
                        {strategy.allowReplayAudio ? (
                          <button type="button" onClick={questionAudio.replay} title={t('replayQuestion')} aria-label={t('replayQuestion')}>
                            <RotateCcw size={17} />
                          </button>
                        ) : null}
                      </>
                    ) : null}
                    {questionAudio.status === 'ERROR' ? (
                      <button type="button" onClick={questionAudio.retry}><RefreshCw size={17} />{t('retryAudio')}</button>
                    ) : null}
                    <button
                      type="button"
                      onClick={questionAudio.toggleAutoPlay}
                      aria-pressed={questionAudio.autoPlay}
                    >
                      {questionAudio.autoPlay ? t('autoPlayOn') : t('autoPlayOff')}
                    </button>
                  </div>
                ) : null}
              </section>

              <footer className="behavior-stage__controls">
                {room.resumed ? <div className="behavior-resume-note" role="status">{t('resumedNotice')}</div> : null}
                {error ? (
                  <div className="behavior-inline-error" role="alert">
                    <AlertCircle size={18} />
                    <span>{getErrorMessage(error)}</span>
                    {room.phase === BehavioralFlowPhase.RECOVERABLE_ERROR ? (
                      <button type="button" onClick={() => { setLocalError(null); room.dismissError(); }}>{t('dismiss')}</button>
                    ) : null}
                  </div>
                ) : null}
                {isSubmitting ? (
                  <div className="behavior-evaluating" role="status" aria-live="polite">
                    <Loader2 size={22} className="behavior-spin" />
                    <div><strong>{t('evaluatingAnswer')}</strong><p>{t('evaluatingAnswerDescription')}</p></div>
                  </div>
                ) : (
                  <BehavioralRecorderControls
                    recorder={recorder}
                    disabled={room.phase !== BehavioralFlowPhase.READY_TO_ANSWER && room.phase !== BehavioralFlowPhase.RECOVERABLE_ERROR}
                    isSubmitting={isSubmitting}
                    timeLimitSeconds={strategy.hasCountdownTimer ? (strategy.defaultCountdownSeconds || 120) : room.currentQuestion.timeLimitSeconds}
                    remainingSeconds={remainingSeconds}
                    strategy={strategy}
                    isAudioPlaying={questionAudio.isPlaying}
                    onSubmit={handleSubmit}
                    t={t}
                  />
                )}
              </footer>
            </>
          ) : (
            renderLoading()
          )}
      </section>
      <FeedbackModal
        isOpen={isFeedbackModalOpen}
        onClose={() => {
          if (!isSubmittingFeedback) setIsFeedbackModalOpen(false);
        }}
        onSubmit={handleSubmitFeedback}
        isSubmitting={isSubmittingFeedback}
        questions={behavioralFeedbackQuestions}
        interviewSessionId={resolvedSessionId}
        evaluationId={behavioralEvaluationId}
        t={tf}
      />
    </InterviewRoomShell>
  );
}

export default BehavioralInterviewPage;
