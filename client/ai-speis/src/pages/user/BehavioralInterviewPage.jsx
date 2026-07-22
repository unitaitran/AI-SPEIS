import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  Bot,
  Loader2,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  Volume2,
} from 'lucide-react';
import BehavioralCompletion from '../../components/behavioralInterview/BehavioralCompletion';
import BehavioralRecorderControls from '../../components/behavioralInterview/BehavioralRecorderControls';
import BehavioralRoomDialog from '../../components/behavioralInterview/BehavioralRoomDialog';
import InterviewRoomShell from '../../components/interviewRoom/InterviewRoomShell';
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
import interviewSessionService from '../../services/InterviewSessionService';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  getNextOpenSession,
  saveActiveInterviewContext,
} from '../../utils/interviewContext';
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

  const recorder = useTechnicalRecorder(interviewLanguage);
  const questionAudio = useQuestionAudio({
    question: room.currentQuestion,
    sessionId: resolvedSessionId,
    language: interviewLanguage,
    preferenceKey: 'ai-speis:behavioral-interview:auto-play-question',
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
  const [latestCampaign, setLatestCampaign] = useState(initialContext?.campaign || null);
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
      statusLabel: message.questionType && message.questionType !== 'Main'
        ? t(`questionType.${message.questionType}`)
        : '',
    }));
    const draft = !isSubmitting && recorder.transcript.trim()
      ? [{
        id: `draft-${room.currentQuestion?.sessionQuestionId || 'current'}`,
        role: 'CANDIDATE',
        content: recorder.transcript,
        statusLabel: t('draft'),
      }]
      : [];
    return [...restored, ...draft];
  }, [isSubmitting, recorder.transcript, room.currentQuestion?.sessionQuestionId, room.transcriptMessages, t]);

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

  useEffect(() => {
    if (room.phase !== BehavioralFlowPhase.COMPLETED) return;
    resetRecorder();
    pauseQuestionAudio();
    refreshCompletedCampaign();
  }, [pauseQuestionAudio, refreshCompletedCampaign, resetRecorder, room.phase]);

  const getErrorMessage = useCallback((requestError) => {
    const code = requestError?.code || 'UNKNOWN_ERROR';
    return t(`error.${code}`, { defaultValue: t('error.UNKNOWN_ERROR') });
  }, [t]);

  const handleSubmit = async () => {
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
  };

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
  }, [recorder.recordingStatus, recorder.transcript, isSubmitting]);

  const requestNavigation = useCallback((path) => {
    if (!phaseIsActive) return true;
    setPendingNavigation(path);
    setDialog({ type: 'leave' });
    return false;
  }, [phaseIsActive]);

  const handleDialogConfirm = async () => {
    if (dialog?.type === 'leave') {
      cleanupRecorder();
      pauseQuestionAudio();
      const target = pendingNavigation || USER_ROUTES.INTERVIEW_SETUP;
      setDialog(null);
      setPendingNavigation(null);
      navigate(target);
      return;
    }

    if (dialog?.type === 'end') {
      setLocalError(null);
      try {
        await room.completeInterview();
        setDialog(null);
      } catch (completeError) {
        setLocalError(completeError);
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

  const handleForceEndSession = async () => {
    if (!resolvedSessionId) return;
    setLocalError(null);
    try {
      const campaign = await interviewSessionService.completeSession(resolvedSessionId);
      const nextSession = getNextOpenSession(campaign, resolvedSessionId);
      saveActiveInterviewContext({
        campaign,
        activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
        configurationKey: initialContext?.configurationKey || null,
      });
      navigate(nextSession?.status === 'Active'
        ? getInterviewRoomPath(nextSession.interviewSessionId)
        : USER_ROUTES.INTERVIEW_SETUP, { replace: true });
    } catch (endError) {
      setLocalError(endError);
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
      onEnd={handleForceEndSession}
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
  const isContinuation = questionType && questionType !== 'Main';

  return (
    <InterviewRoomShell
      language={interviewLanguage}
      mainFlush
      onBeforeNavigate={requestNavigation}
      isTranscriptOpen={transcriptOpen}
      onCloseTranscript={() => setTranscriptOpen(false)}
      onToggleTranscript={() => setTranscriptOpen((open) => !open)}
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
        <BehavioralRoomDialog
          dialog={dialog}
          busy={room.phase === BehavioralFlowPhase.COMPLETING}
          onCancel={() => { setDialog(null); setPendingNavigation(null); }}
          onConfirm={handleDialogConfirm}
          t={t}
        />
      )}
    >
      <section className="behavior-stage" aria-label={t('behavioralInterview')}>
          {room.phase === BehavioralFlowPhase.COMPLETED ? (
            <div className="behavior-completion-stack">
              <BehavioralCompletion
                result={room.completionResult}
                answeredCount={room.completionResult?.mainQuestions?.length || room.session?.completedMainQuestionCount || 0}
                hasNextRound={Boolean(nextRoundSession)}
                onContinue={handleContinue}
                onOverview={() => navigate(campaignResultPath)}
                t={t}
              />
              {localError ? (
                <div className="behavior-inline-error" role="alert">
                  <AlertCircle size={18} />
                  <span>{getErrorMessage(localError)}</span>
                  <button type="button" onClick={refreshCompletedCampaign}>{t('retry')}</button>
                </div>
              ) : null}
            </div>
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
                  {isContinuation ? t(`questionType.${questionType}`) : t('interviewerAsks')}
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
                <div className="behavior-audio-controls" aria-label={t('questionAudio')}>
                  {questionAudio.status === 'LOADING' ? <Loader2 size={18} className="behavior-spin" /> : null}
                  {questionAudio.status === 'READY' ? (
                    <>
                      <button type="button" onClick={questionAudio.isPlaying ? questionAudio.pause : questionAudio.play}>
                        {questionAudio.isPlaying ? <Pause size={17} /> : <Volume2 size={17} />}
                        {questionAudio.isPlaying ? t('pauseQuestion') : t('playQuestion')}
                      </button>
                      <button type="button" onClick={questionAudio.replay} title={t('replayQuestion')} aria-label={t('replayQuestion')}>
                        <RotateCcw size={17} />
                      </button>
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
                    timeLimitSeconds={room.currentQuestion.timeLimitSeconds}
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
    </InterviewRoomShell>
  );
}

export default BehavioralInterviewPage;
