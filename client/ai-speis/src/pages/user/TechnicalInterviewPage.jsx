import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import EndSessionConfirmDialog from '../../components/technicalInterview/EndSessionConfirmDialog';
import InterviewInitializationLoading from '../../components/technicalInterview/InterviewInitializationLoading';
import TechnicalAnswerPanel from '../../components/technicalInterview/TechnicalAnswerPanel';
import TechnicalEvaluationState from '../../components/technicalInterview/TechnicalEvaluationState';
import TechnicalInterviewErrorState from '../../components/technicalInterview/TechnicalInterviewErrorState';
import TechnicalInterviewHeader from '../../components/technicalInterview/TechnicalInterviewHeader';
import TechnicalInterviewProgress from '../../components/technicalInterview/TechnicalInterviewProgress';
import TechnicalQuestionPanel from '../../components/technicalInterview/TechnicalQuestionPanel';
import TechnicalTranscriptPanel from '../../components/technicalInterview/TechnicalTranscriptPanel';
import TechnicalTranscriptToggle from '../../components/technicalInterview/TechnicalTranscriptToggle';
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
import useQuestionAudio from '../../features/technicalInterview/useQuestionAudio';
import useSubmitTechnicalAnswer from '../../features/technicalInterview/useSubmitTechnicalAnswer';
import useTechnicalInterviewSession, {
  getTechnicalSessionStatus,
  TechnicalInterviewFlowStatus,
} from '../../features/technicalInterview/useTechnicalInterviewSession';
import useTechnicalRecorder from '../../features/technicalInterview/useTechnicalRecorder';
import useTechnicalInterviewTranscript, {
  TechnicalTranscriptItemStatus,
} from '../../features/technicalInterview/useTechnicalInterviewTranscript';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { getInterviewResultPath, USER_ROUTES } from '../../routes/routePaths';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import { getActiveInterviewContext, getInterviewSetupDraft } from '../../utils/interviewContext';
import '../../styles/user/TechnicalInterview.css';

const getDefaultTranscriptVisibility = () => (
  typeof window === 'undefined' || window.innerWidth >= 1024
);

function TechnicalInterviewPage({ sessionId }) {
  const activeContext = useMemo(() => getActiveInterviewContext(), []);
  const setupDraft = useMemo(() => getInterviewSetupDraft(), []);
  const resolvedSessionId = sessionId || activeContext?.activeSessionId || null;
  const interviewLanguage = (activeContext?.campaign?.language || setupDraft?.language) === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => (
    translate(key, { ...options, lng: interviewLanguage })
  ), [interviewLanguage, translate]);
  const room = useTechnicalInterviewSession(resolvedSessionId);
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
  } = transcriptLedger;
  const questionAudio = useQuestionAudio({
    question: room.currentQuestion,
    sessionId: resolvedSessionId,
    language: interviewLanguage,
  });
  const setRecorderTranscript = recorder.setTranscript;
  const [localError, setLocalError] = useState(null);
  const [activeDraftAttempt, setActiveDraftAttempt] = useState(null);
  const [isCompleting, setIsCompleting] = useState(false);
  const [isEndConfirmOpen, setIsEndConfirmOpen] = useState(false);
  const [isTranscriptOpen, setIsTranscriptOpen] = useState(getDefaultTranscriptVisibility);
  const transcriptToggleRef = useRef(null);

  const status = getTechnicalSessionStatus(room.session);
  const attemptId = room.currentQuestion?.attemptId || null;
  const transcriptEditable = room.session?.transcriptEditable !== false;
  const processingDraft = status === TechnicalSessionStatus.EVALUATING
    ? readTechnicalInterviewSessionDraft(resolvedSessionId)
    : null;
  const visibleTranscript = recorder.transcript || processingDraft?.transcript || '';

  const closeTranscript = useCallback(() => {
    setIsTranscriptOpen(false);
    window.requestAnimationFrame(() => transcriptToggleRef.current?.focus());
  }, []);

  const openTranscript = useCallback(() => setIsTranscriptOpen(true), []);

  useEffect(() => {
    if (status === TechnicalSessionStatus.COMPLETED && resolvedSessionId) {
      navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
    }
  }, [resolvedSessionId, status]);

  useEffect(() => {
    syncQuestion(room.currentQuestion);
  }, [room.currentQuestion, syncQuestion]);

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
        navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
        return;
      }
      if (!response.nextQuestion && !response.currentQuestion && !response.question) await room.reload();
    } catch (error) {
      const recovery = await room.reconcileAfterSubmission(attemptId);
      if (recovery.state === 'ACCEPTED_COMPLETED') {
        markCandidateFinal(attemptId, transcript);
        clearTechnicalInterviewDraft(resolvedSessionId, attemptId);
        recorder.reset();
        navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
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

  const handleCompleteEarly = async () => {
    if (!resolvedSessionId || isCompleting) return;
    setIsCompleting(true);
    setLocalError(null);
    try {
      const response = await technicalInterviewApi.completeSession(resolvedSessionId);
      room.applyAnswerResponse(response);
      setIsEndConfirmOpen(false);
      navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
    } catch (error) {
      setLocalError(error);
    } finally {
      setIsCompleting(false);
    }
  };

  const header = room.session && (
    <TechnicalInterviewHeader
      t={t}
      jobRole={room.session.jobRole || setupDraft?.jobRole}
      experienceLevel={room.session.experienceLevel || setupDraft?.experienceLevel}
      status={status}
      canCompleteEarly={room.session.canCompleteEarly === true}
      isCompleting={isCompleting}
      onComplete={() => setIsEndConfirmOpen(true)}
    />
  );

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
        message={getErrorMessage(room.error)}
        onRetry={canRetryQuestionGeneration ? handleStart : room.reload}
        onBack={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
        retryLabel={t('common.retry')}
        backLabel={t('room.backToSetup')}
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
        message={t('room.failedDescription')}
        onRetry={handleStart}
        retryLabel={t('common.retry')}
        onBack={() => navigate(USER_ROUTES.DASHBOARD)}
        backLabel={t('room.backToDashboard')}
      />
    );
  } else if (status === TechnicalSessionStatus.EVALUATING || submitMutation.isSubmitting) {
    content = (
      <TechnicalEvaluationState
        t={t}
      />
    );
  } else if (room.currentQuestion) {
    content = (
      <>
        <TechnicalInterviewProgress
          question={room.currentQuestion}
          t={t}
        />
        <div className="technical-room-grid">
          <TechnicalQuestionPanel
            question={room.currentQuestion}
            audio={questionAudio}
            audioDisabled={recorder.recordingStatus === RecordingStatus.RECORDING
              || recorder.sttStatus === SttStatus.PROCESSING}
            stageMode
            t={t}
          />
          <TechnicalAnswerPanel
            recorder={recorder}
            transcriptEditable={transcriptEditable}
            disabled={questionAudio.isPlaying}
            isSubmitting={submitMutation.isSubmitting}
            errorMessage={localError ? getErrorMessage(localError) : ''}
            onSubmit={handleSubmit}
            showTranscriptEditor={false}
            stageMode
            t={t}
          />
        </div>
      </>
    );
  } else {
    content = (
      <TechnicalInterviewErrorState
        title={t('room.questionUnavailableTitle')}
        message={room.questionError?.status === 404
          ? t('room.questionApiUnavailable')
          : room.questionError
            ? getErrorMessage(room.questionError)
            : t('room.questionUnavailableDescription')}
        onRetry={room.reload}
        retryLabel={t('common.retry')}
      />
    );
  }

  return (
    <UserLayout collapseSidebar immersive>
      <div className="technical-page technical-page--room animate-pageEntrance" lang={interviewLanguage}>
        <div className={`technical-interview-workspace${isTranscriptOpen ? ' technical-interview-workspace--transcript-open' : ''}`}>
          {isTranscriptOpen && (
            <>
              <button
                type="button"
                className="technical-transcript-backdrop"
                onClick={closeTranscript}
                aria-label={t('room.closeTranscript')}
              />
              <TechnicalTranscriptPanel
                items={transcriptItems}
                recorder={recorder}
                currentTranscript={visibleTranscript}
                hasActiveAttempt={Boolean(attemptId || processingDraft?.attemptId)}
                transcriptEditable={transcriptEditable}
                disabled={status === TechnicalSessionStatus.EVALUATING || submitMutation.isSubmitting}
                isOpen={isTranscriptOpen}
                onClose={closeTranscript}
                t={t}
              />
            </>
          )}
          <section className="technical-main-stage" aria-label={t('room.interviewWorkspace')}>
            {header}
            <div className="technical-main-stage__body">
              {content}
            </div>
            <div className="technical-main-stage__actions">
              <TechnicalTranscriptToggle
                ref={transcriptToggleRef}
                isOpen={isTranscriptOpen}
                onClick={isTranscriptOpen ? closeTranscript : openTranscript}
                t={t}
              />
            </div>
          </section>
        </div>
        <EndSessionConfirmDialog
          action={isEndConfirmOpen ? 'session' : null}
          isSubmitting={isCompleting}
          onConfirm={handleCompleteEarly}
          onCancel={() => setIsEndConfirmOpen(false)}
          t={t}
        />
      </div>
    </UserLayout>
  );
}

export default TechnicalInterviewPage;
