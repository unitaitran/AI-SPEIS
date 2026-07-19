import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, Play } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import TechnicalAnswerPanel from '../../components/technicalInterview/TechnicalAnswerPanel';
import TechnicalEvaluationState from '../../components/technicalInterview/TechnicalEvaluationState';
import TechnicalInterviewErrorState from '../../components/technicalInterview/TechnicalInterviewErrorState';
import TechnicalInterviewHeader from '../../components/technicalInterview/TechnicalInterviewHeader';
import TechnicalInterviewProgress from '../../components/technicalInterview/TechnicalInterviewProgress';
import TechnicalQuestionPanel from '../../components/technicalInterview/TechnicalQuestionPanel';
import { clearTechnicalInterviewDraft, readTechnicalInterviewDraft, saveTechnicalInterviewDraft } from '../../features/technicalInterview/technicalInterviewDraft';
import { getTechnicalInterviewErrorKey } from '../../features/technicalInterview/technicalInterviewErrors';
import { TechnicalSessionStatus } from '../../features/technicalInterview/technicalInterview.types';
import useSubmitTechnicalAnswer from '../../features/technicalInterview/useSubmitTechnicalAnswer';
import useTechnicalInterviewSession, { getTechnicalSessionStatus } from '../../features/technicalInterview/useTechnicalInterviewSession';
import useTechnicalRecorder from '../../features/technicalInterview/useTechnicalRecorder';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { getInterviewResultPath, USER_ROUTES } from '../../routes/routePaths';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import { getActiveInterviewContext, getInterviewSetupDraft } from '../../utils/interviewContext';
import '../../styles/user/TechnicalInterview.css';

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
  const submitMutation = useSubmitTechnicalAnswer(resolvedSessionId);
  const recorder = useTechnicalRecorder(interviewLanguage);
  const setRecorderTranscript = recorder.setTranscript;
  const [localError, setLocalError] = useState(null);
  const [activeDraftAttempt, setActiveDraftAttempt] = useState(null);
  const [isCompleting, setIsCompleting] = useState(false);

  const status = getTechnicalSessionStatus(room.session);
  const attemptId = room.currentQuestion?.attemptId || null;
  const transcriptEditable = room.session?.transcriptEditable !== false;

  useEffect(() => {
    if (status === TechnicalSessionStatus.COMPLETED && resolvedSessionId) {
      navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
    }
  }, [resolvedSessionId, status]);

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
    try {
      const response = await submitMutation.submitAnswer({
        attemptId,
        transcript,
        audioId: recorder.audioId || undefined,
      });
      if (!response) return;

      clearTechnicalInterviewDraft(resolvedSessionId, attemptId);
      recorder.reset();
      room.applyAnswerResponse(response);
      const nextStatus = getTechnicalSessionStatus(response?.session) || response?.sessionStatus;
      if (nextStatus === TechnicalSessionStatus.COMPLETED) {
        navigate(getInterviewResultPath(resolvedSessionId), { replace: true });
        return;
      }
      if (!response.currentQuestion && !response.question) await room.reload();
    } catch (error) {
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
      onComplete={handleCompleteEarly}
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
      <div className="technical-loading" aria-label={t('common.loading')}>
        <div className="technical-skeleton" />
        <div className="technical-skeleton technical-skeleton--large" />
      </div>
    );
  } else if (room.error) {
    content = (
      <TechnicalInterviewErrorState
        title={t('room.openFailed')}
        message={getErrorMessage(room.error)}
        onRetry={room.reload}
        onBack={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
        retryLabel={t('common.retry')}
        backLabel={t('room.backToSetup')}
      />
    );
  } else if (status === TechnicalSessionStatus.CREATED) {
    content = (
      <section className="technical-ready technical-card">
        <div className="technical-ready__icon"><Play size={30} aria-hidden="true" /></div>
        <h2>{t('room.readyTitle')}</h2>
        <p>{t('room.readyDescription')}</p>
        {localError && <p className="technical-inline-error" role="alert">{getErrorMessage(localError)}</p>}
        <div className="technical-ready__actions">
          <button type="button" className="technical-secondary-button" onClick={handleStart}>
            <Play size={18} aria-hidden="true" />{t('room.startInterview')}
          </button>
        </div>
      </section>
    );
  } else if (status === TechnicalSessionStatus.SELECTING_QUESTION) {
    content = (
      <section className="technical-evaluation technical-card" aria-live="polite">
        <div className="technical-evaluation__icon">
          <Loader2 size={30} className="animate-spin" aria-hidden="true" />
        </div>
        <h2>{t('room.selectingQuestionTitle')}</h2>
        <p>{t('room.selectingQuestionDescription')}</p>
      </section>
    );
  } else if (status === TechnicalSessionStatus.FAILED) {
    content = (
      <TechnicalInterviewErrorState
        title={t('room.failedTitle')}
        message={t('room.failedDescription')}
        onBack={() => navigate(USER_ROUTES.DASHBOARD)}
        backLabel={t('room.backToDashboard')}
      />
    );
  } else if (room.currentQuestion) {
    const evaluating = status === TechnicalSessionStatus.EVALUATING;
    content = (
      <>
        <TechnicalInterviewProgress
          current={room.currentQuestion.mainQuestionIndex}
          total={room.currentQuestion.totalMainQuestions}
          t={t}
        />
        <div className="technical-room-grid">
          <TechnicalQuestionPanel question={room.currentQuestion} t={t} />
          <TechnicalAnswerPanel
            recorder={recorder}
            transcriptEditable={transcriptEditable}
            disabled={evaluating}
            isSubmitting={submitMutation.isSubmitting}
            errorMessage={localError ? getErrorMessage(localError) : ''}
            onSubmit={handleSubmit}
            t={t}
          />
        </div>
        {evaluating && <div style={{ marginTop: 'var(--spacing-md)' }}><TechnicalEvaluationState t={t} /></div>}
      </>
    );
  } else {
    content = (
      <TechnicalInterviewErrorState
        title={t('room.questionUnavailableTitle')}
        message={t('room.questionUnavailableDescription')}
        onRetry={room.reload}
        retryLabel={t('common.retry')}
      />
    );
  }

  return (
    <UserLayout>
      <div className="technical-page animate-pageEntrance" lang={interviewLanguage}>
        {header}
        {content}
      </div>
    </UserLayout>
  );
}

export default TechnicalInterviewPage;
