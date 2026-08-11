import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  Bot,
  FileText,
  Loader2,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  Volume2,
} from 'lucide-react';
import BehavioralRecorderControls from '../../components/behavioralInterview/BehavioralRecorderControls';
import BehavioralRoomDialog from '../../components/behavioralInterview/BehavioralRoomDialog';
import EvaluatingAnalysisModal from '../../components/interviewRoom/EvaluatingAnalysisModal';
import InterviewRoomShell from '../../components/interviewRoom/InterviewRoomShell';
import InterviewRoomState from '../../components/interviewRoom/InterviewRoomState';
import InterviewRoomTranscriptPanel from '../../components/interviewRoom/InterviewRoomTranscriptPanel';
import TechnicalV2Completion from '../../components/technicalInterview/TechnicalV2Completion';
import { RecordingStatus, SttStatus } from '../../features/technicalInterview/technicalInterview.types';
import {
  TechnicalV2ErrorCode,
  TechnicalV2FlowPhase,
} from '../../features/technicalInterview/technicalV2Interview.types';
import useQuestionAudio, { QuestionAudioStatus } from '../../features/technicalInterview/useQuestionAudio';
import useTechnicalInterviewSession from '../../features/technicalInterview/useTechnicalInterviewSession';
import useTechnicalRecorder from '../../features/technicalInterview/useTechnicalRecorder';
import useInterviewStrategy from '../../features/interviewStrategy/useInterviewStrategy';
import { InterviewMode } from '../../features/interviewStrategy/InterviewMode';
import { navigate } from '../../routes/navigation';
import { getCampaignResultPath, getCodingInterviewRoomPath, getInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';
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
  TechnicalV2FlowPhase.READY_TO_ANSWER,
  TechnicalV2FlowPhase.SUBMITTING_ANSWER,
  TechnicalV2FlowPhase.EVALUATING_ANSWER,
  TechnicalV2FlowPhase.RECOVERABLE_ERROR,
]);

const PHASE_COPY = {
  [TechnicalV2FlowPhase.CHECKING_SESSION]: 'checkingSession',
  [TechnicalV2FlowPhase.INITIALIZING]: 'initializingInterview',
  [TechnicalV2FlowPhase.LOADING_QUESTION]: 'preparingQuestion',
  [TechnicalV2FlowPhase.SUBMITTING_ANSWER]: 'submittingAnswer',
  [TechnicalV2FlowPhase.EVALUATING_ANSWER]: 'evaluatingAnswer',
  [TechnicalV2FlowPhase.COMPLETING]: 'completingInterview',
};

const getDraftKey = (sessionId, questionId) => `technical-v2-interview:${sessionId}:${questionId}:draft`;

const readDraft = (sessionId, questionId) => {
  if (!sessionId || !questionId) return '';
  try {
    return localStorage.getItem(getDraftKey(sessionId, questionId)) || '';
  } catch {
    return '';
  }
};

const saveDraft = (sessionId, questionId, transcript) => {
  if (!sessionId || !questionId) return;
  try {
    if (transcript.trim()) localStorage.setItem(getDraftKey(sessionId, questionId), transcript);
    else localStorage.removeItem(getDraftKey(sessionId, questionId));
  } catch {
    // Draft storage is best-effort; server state remains authoritative.
  }
};

function TechnicalInterviewPage({ sessionId }) {
  const initialContext = useMemo(() => getActiveInterviewContext(), []);
  const setupDraft = useMemo(() => getInterviewSetupDraft(), []);
  const resolvedSessionId = sessionId || initialContext?.activeSessionId || null;
  const room = useTechnicalInterviewSession(resolvedSessionId);
  const interviewLanguage = (room.session?.language
    || initialContext?.campaign?.language
    || setupDraft?.language) === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => translate(`technicalRoom.${key}`, {
    ...options,
    lng: interviewLanguage,
    defaultValue: options.defaultValue || key,
  }), [interviewLanguage, translate]);
  const recorder = useTechnicalRecorder(interviewLanguage);
  const {
    mode,
    strategy,
    remainingSeconds,
    stopTimer,
    handleQuestionAudioEnded,
  } = useInterviewStrategy(
    room.session?.mode || initialContext?.campaign?.mode || setupDraft?.mode,
  );
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
    preferenceKey: 'ai-speis:technical-v2-interview:auto-play-question',
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
  const previousQuestionRef = useRef(null);
  const hydratingDraftRef = useRef(false);
  const handleSubmitRef = useRef(null);

  const phaseIsActive = ACTIVE_PHASES.has(room.phase);
  const isSubmitting = room.phase === TechnicalV2FlowPhase.SUBMITTING_ANSWER
    || room.phase === TechnicalV2FlowPhase.EVALUATING_ANSWER;
  const error = localError || room.error;
  const nextRoundSession = getNextOpenSession(latestCampaign, resolvedSessionId);
  const completedCampaignId = latestCampaign?.interviewCampaignId
    || room.generalSession?.interviewCampaignId
    || initialContext?.campaign?.interviewCampaignId;
  const campaignResultPath = room.phase === TechnicalV2FlowPhase.COMPLETED
    && !nextRoundSession
    && completedCampaignId
    ? getCampaignResultPath(completedCampaignId)
    : USER_ROUTES.DASHBOARD;

  useEffect(() => {
    const currentId = room.currentQuestion?.sessionQuestionId || null;
    if (currentId && previousQuestionRef.current !== currentId) {
      if (previousQuestionRef.current) resetRecorder();
      hydratingDraftRef.current = true;
      const savedDraft = readDraft(resolvedSessionId, currentId);
      if (savedDraft) {
        setRecorderTranscript(savedDraft);
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
    saveDraft(resolvedSessionId, currentId, recorder.transcript);
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
      saveActiveInterviewContext({
        campaign,
        activeSessionId: getNextOpenSession(campaign, resolvedSessionId)?.status === 'Active'
          ? getNextOpenSession(campaign, resolvedSessionId).interviewSessionId
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
    if (room.phase !== TechnicalV2FlowPhase.COMPLETED) return;
    resetRecorder();
    pauseQuestionAudio();
    if (mode !== InterviewMode.REAL) {
      refreshCompletedCampaign();
      return;
    }
    const navigateToNextRound = async () => {
      const campaignId = room.generalSession?.interviewCampaignId
        || initialContext?.campaign?.interviewCampaignId;
      if (!campaignId) return;
      try {
        const campaign = await interviewSessionService.getCampaign(campaignId);
        const nextSession = getNextOpenSession(campaign, resolvedSessionId);
        saveActiveInterviewContext({
          campaign,
          activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
          configurationKey: initialContext?.configurationKey || null,
        });
        if (nextSession) {
          const targetPath = nextSession.interviewRoundType === 'Coding' || nextSession.interviewRoundType === 'Code'
            ? getCodingInterviewRoomPath(nextSession.interviewSessionId)
            : getInterviewRoomPath(nextSession.interviewSessionId);
          navigate(targetPath, { replace: true });
          return;
        }
      } catch {
        // Completion remains server-owned; the user can retry the campaign refresh.
      }
      refreshCompletedCampaign();
    };
    navigateToNextRound();
  }, [initialContext, mode, pauseQuestionAudio, refreshCompletedCampaign, resetRecorder, resolvedSessionId, room.generalSession?.interviewCampaignId, room.phase]);

  const getErrorMessage = useCallback((requestError) => {
    const code = requestError?.code || TechnicalV2ErrorCode.UNKNOWN_ERROR;
    return t(`error.${code}`, { defaultValue: t('error.UNKNOWN_ERROR') });
  }, [t]);

  const handleSubmit = async () => {
    stopTimer();
    const transcript = recorder.transcript.trim();
    const questionId = room.currentQuestion?.sessionQuestionId;
    if (!transcript) {
      setLocalError({ code: TechnicalV2ErrorCode.TRANSCRIPT_REQUIRED });
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
        saveDraft(resolvedSessionId, questionId, '');
        recorder.reset();
      }
    } catch (submitError) {
      setLocalError(submitError);
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
      && !isSubmitting
      && !autoSubmittingRef.current
    ) {
      autoSubmittingRef.current = true;
      Promise.resolve(handleSubmitRef.current?.()).finally(() => { autoSubmittingRef.current = false; });
    }
  }, [isSubmitting, recorder.recordingStatus, recorder.transcript]);

  const requestNavigation = useCallback((path) => {
    if (!phaseIsActive) return true;
    setPendingNavigation(path);
    setDialog({ type: 'leave' });
    return false;
  }, [phaseIsActive]);

  const handleEndSession = async () => {
    setLocalError(null);
    try {
      await room.completeInterview();
      setDialog(null);
    } catch (endError) {
      setDialog(null);
      setLocalError(endError);
    }
  };

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
    if (dialog?.type === 'end') await handleEndSession();
  };

  const handleContinue = () => {
    if (!nextRoundSession) return;
    saveActiveInterviewContext({
      ...getActiveInterviewContext(),
      activeSessionId: nextRoundSession.status === 'Active' ? nextRoundSession.interviewSessionId : null,
    });
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
      onEnd={handleEndSession}
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

  const currentQuestion = room.currentQuestion;
  const questionIndex = Number(currentQuestion?.mainQuestionIndex || currentQuestion?.questionOrder || 1);
  const questionTotal = Number(currentQuestion?.totalMainQuestions || room.session?.targetMainQuestionCount || 0);
  const isEvaluating = room.phase === TechnicalV2FlowPhase.SUBMITTING_ANSWER
    || room.phase === TechnicalV2FlowPhase.EVALUATING_ANSWER;
  const transcriptItems = useMemo(() => {
    const restored = room.transcriptMessages.map((message) => ({
      ...message,
      role: message.speaker === 'candidate' ? 'CANDIDATE' : 'INTERVIEWER',
      statusLabel: message.questionType ? t(`questionType.${message.questionType}`, { defaultValue: '' }) : '',
    }));
    const draft = !isEvaluating && recorder.transcript.trim()
      ? [{ id: `draft-${currentQuestion?.sessionQuestionId || 'current'}`, role: 'CANDIDATE', content: recorder.transcript, statusLabel: t('draft') }]
      : [];
    return [...restored, ...draft];
  }, [currentQuestion?.sessionQuestionId, isEvaluating, recorder.transcript, room.transcriptMessages, t]);

  let content;
  if (!resolvedSessionId) {
    content = <InterviewRoomState variant="error" title={t('openFailed')} description={t('sessionIdMissing')} onBack={() => navigate(USER_ROUTES.INTERVIEW_SETUP)} backLabel={t('backToSetup')} />;
  } else if (room.phase === TechnicalV2FlowPhase.COMPLETED) {
    content = mode === InterviewMode.REAL
      ? <InterviewRoomState title={t('preparingNextRound')} description={t('preparingNextRoundDescription')} />
      : (
        <TechnicalV2Completion
          result={room.completionResult}
          answeredCount={room.session?.completedMainQuestionCount}
          hasNextRound={Boolean(nextRoundSession)}
          onContinue={handleContinue}
          onOverview={() => navigate(campaignResultPath)}
          feedbackError={feedbackRetryError}
          feedbackRetrying={room.feedbackRetrying}
          onRetryFeedback={handleRetryFeedback}
          t={t}
        />
      );
  } else if (room.phase === TechnicalV2FlowPhase.FATAL_ERROR) {
    content = renderFatalError();
  } else if (room.phase === TechnicalV2FlowPhase.SESSION_CONFLICT) {
    content = renderConflict();
  } else if (room.phase === TechnicalV2FlowPhase.RECOVERABLE_ERROR && !currentQuestion) {
    content = <InterviewRoomState variant="error" title={t('recoveryTitle')} description={getErrorMessage(room.error)} onRetry={() => room.completeInterview().catch(() => undefined)} retryLabel={t('retryCompletion')} onBack={room.reload} backLabel={t('reloadSession')} />;
  } else if (room.isBusy && !currentQuestion) {
    content = renderLoading();
  } else if (currentQuestion) {
    const questionType = currentQuestion.questionType;
    const questionTypeKey = String(questionType || '').toLowerCase();
    content = (
      <section className="behavior-stage" aria-label={t('technicalInterview')}>
        <header className="behavior-stage__topbar">
          <div className="behavior-stage__session">
            <span>{t('technicalInterview')}</span>
            <strong>{room.session?.jobRole || setupDraft?.jobRole || t('interviewSession')}</strong>
            <button type="button" className="technical-transcript-toggle technical-transcript-toggle--topbar" onClick={() => setTranscriptOpen((open) => !open)} aria-expanded={transcriptOpen}>
              <FileText size={15} aria-hidden="true" />{t('transcript')}
            </button>
          </div>
          <div className="behavior-stage__top-actions">
            <button type="button" className="behavior-stage__end" onClick={() => setDialog({ type: 'end' })}>{t('endInterview')}</button>
          </div>
        </header>
        <div className="behavior-stage__progress" role="progressbar" aria-label={t('progressLabel', { current: questionIndex, total: questionTotal })} aria-valuemin={0} aria-valuemax={questionTotal || undefined} aria-valuenow={questionTotal ? questionIndex : undefined}>
          <span style={{ width: `${questionTotal ? Math.min(100, (questionIndex / questionTotal) * 100) : 0}%` }} />
        </div>
        <section className="behavior-question" aria-labelledby="technical-v2-question-text">
          <div className="behavior-question__eyebrow">{t(`questionType.${questionTypeKey}`, { defaultValue: t('interviewerAsks') })}</div>
          <h1 id="technical-v2-question-text">{currentQuestion.content}</h1>
          {(currentQuestion.skill || currentQuestion.difficulty) ? <p className="behavior-question__hint">{[currentQuestion.skill, currentQuestion.difficulty].filter(Boolean).join(' / ')}</p> : null}
          <div className={`behavior-interviewer ${recorder.recordingStatus === RecordingStatus.RECORDING ? 'behavior-interviewer--listening' : ''}`} aria-hidden="true">
            <span className="behavior-interviewer__ring" /><span className="behavior-interviewer__ring" /><span className="behavior-interviewer__core"><Bot size={28} /></span>
          </div>
          {strategy.showAudioControls ? (
            <div className="behavior-audio-controls" aria-label={t('questionAudio')}>
              {questionAudio.status === QuestionAudioStatus.LOADING || questionAudio.status === QuestionAudioStatus.IDLE ? <Loader2 size={18} className="behavior-spin" /> : null}
              {questionAudio.status === QuestionAudioStatus.READY ? (
                <>
                  <button type="button" onClick={questionAudio.isPlaying ? questionAudio.pause : questionAudio.play} disabled={recorder.recordingStatus === RecordingStatus.RECORDING || recorder.sttStatus === SttStatus.PROCESSING}>
                    {questionAudio.isPlaying ? <Pause size={17} /> : <Volume2 size={17} />}{questionAudio.isPlaying ? t('pauseQuestion') : t('playQuestion')}
                  </button>
                  {strategy.allowReplayAudio ? <button type="button" onClick={questionAudio.replay} aria-label={t('replayQuestion')}><RotateCcw size={17} /></button> : null}
                </>
              ) : null}
              {questionAudio.status === QuestionAudioStatus.ERROR ? <button type="button" onClick={questionAudio.retry}><RefreshCw size={17} />{t('retryAudio')}</button> : null}
              <button type="button" onClick={questionAudio.toggleAutoPlay} aria-pressed={questionAudio.autoPlay}>{questionAudio.autoPlay ? t('autoPlayOn') : t('autoPlayOff')}</button>
            </div>
          ) : null}
        </section>
        <footer className="behavior-stage__controls">
          {error ? <div className="behavior-inline-error" role="alert"><AlertCircle size={18} /><span>{getErrorMessage(error)}</span>{room.phase === TechnicalV2FlowPhase.RECOVERABLE_ERROR ? <button type="button" onClick={() => { setLocalError(null); room.dismissError(); }}>{t('dismiss')}</button> : null}</div> : null}
          {isEvaluating ? (
            <div className="behavior-evaluating" role="status" aria-live="polite"><Loader2 size={22} className="behavior-spin" /><div><strong>{t('evaluatingAnswer')}</strong><p>{t('evaluatingAnswerDescription')}</p></div></div>
          ) : (
            <BehavioralRecorderControls
              recorder={recorder}
              disabled={room.phase !== TechnicalV2FlowPhase.READY_TO_ANSWER && room.phase !== TechnicalV2FlowPhase.RECOVERABLE_ERROR}
              isSubmitting={isSubmitting}
              timeLimitSeconds={currentQuestion.timeLimitSeconds}
              remainingSeconds={remainingSeconds}
              strategy={strategy}
              isAudioPlaying={questionAudio.isPlaying}
              onSubmit={handleSubmit}
              t={(key, options = {}) => t(`recorder.${key}`, options)}
            />
          )}
        </footer>
      </section>
    );
  } else {
    content = <InterviewRoomState variant="error" title={t('questionUnavailableTitle')} description={getErrorMessage(error)} onRetry={room.reload} retryLabel={t('retry')} onBack={() => navigate(USER_ROUTES.INTERVIEW_SETUP)} backLabel={t('backToSetup')} />;
  }

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
          liveState={recorder.sttStatus === SttStatus.PROCESSING ? { icon: Loader2, label: t('transcribing'), tone: 'processing', spin: true } : null}
          onClose={() => setTranscriptOpen(false)}
          title={t('transcriptTitle')}
        />
      )}
      dialog={(
        <>
          <BehavioralRoomDialog
            dialog={dialog}
            busy={room.phase === TechnicalV2FlowPhase.COMPLETING}
            onCancel={() => { setDialog(null); setPendingNavigation(null); }}
            onConfirm={handleDialogConfirm}
            t={t}
          />
          <EvaluatingAnalysisModal isOpen={room.phase === TechnicalV2FlowPhase.COMPLETING} />
        </>
      )}
    >
      {content}
    </InterviewRoomShell>
  );
}

export default TechnicalInterviewPage;
