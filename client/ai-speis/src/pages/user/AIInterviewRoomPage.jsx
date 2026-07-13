import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle, ArrowLeft, CheckCircle2, Loader2, Clock, FileText } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  saveActiveInterviewContext,
} from '../../utils/interviewContext';

const getTimestamp = (value) => {
  if (!value) return null;
  const normalizedValue = typeof value === 'string'
    && !/(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
    ? `${value}Z`
    : value;
  const timestamp = new Date(normalizedValue).getTime();
  return Number.isFinite(timestamp) ? timestamp : null;
};

function AIInterviewRoomPage() {
  const [interviewContext, setInterviewContext] = useState(() => getActiveInterviewContext());
  const configuredLanguage = interviewContext?.campaign?.language || getInterviewSetupDraft()?.language;
  const interviewLanguage = configuredLanguage === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => (
    translate(key, { ...options, lng: interviewLanguage })
  ), [interviewLanguage, translate]);
  const [session, setSession] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [timer, setTimer] = useState(0);
  const [isCompleting, setIsCompleting] = useState(false);
  const [roundMessage, setRoundMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const campaign = interviewContext?.campaign;

  useEffect(() => {
    const durationMinutes = Number(campaign?.durationMinutes);
    if (!Number.isFinite(durationMinutes) || durationMinutes <= 0) {
      setTimer(0);
      return undefined;
    }

    if (campaign.status === 'Completed' || campaign.status === 'Cancelled' || campaign.status === 'Expired') {
      setTimer(0);
      return undefined;
    }

    const configuredDurationSeconds = Math.round(durationMinutes * 60);
    const configuredDurationMs = configuredDurationSeconds * 1000;
    const startedAt = getTimestamp(campaign.startedAt);
    const expiresAt = getTimestamp(campaign.expiresAt);
    const calculatedDeadline = startedAt ? startedAt + configuredDurationMs : null;
    const deadline = startedAt && expiresAt
      ? Math.min(expiresAt, calculatedDeadline)
      : calculatedDeadline || (Date.now() + configuredDurationMs);

    const updateTimer = () => {
      const remainingSeconds = Math.min(
        configuredDurationSeconds,
        Math.max(0, Math.ceil((deadline - Date.now()) / 1000)),
      );
      setTimer(remainingSeconds);
    };

    updateTimer();
    const interval = window.setInterval(updateTimer, 1000);
    return () => window.clearInterval(interval);
  }, [campaign]);

  useEffect(() => {
    const storedContext = getActiveInterviewContext();
    const activeSessionId = storedContext?.activeSessionId;

    if (!storedContext?.campaign?.interviewCampaignId
      || (!activeSessionId && storedContext.campaign.status !== 'Completed')) {
      setError(t('room.campaignMissing'));
      setIsLoading(false);
      return undefined;
    }

    let isMounted = true;
    interviewSessionService.getCampaign(storedContext.campaign.interviewCampaignId)
      .then((campaign) => {
        if (!isMounted) return;
        if (campaign.status === 'Expired') {
          throw new Error(t('room.campaignExpired'));
        }
        const activeSession = (campaign.sessions || []).find((candidate) => (
          candidate.status === 'Active'
          && (candidate.interviewSessionId === activeSessionId || !activeSessionId)
        )) || (campaign.sessions || []).find((candidate) => candidate.status === 'Active');
        if (!activeSession && campaign.status !== 'Completed') {
          throw new Error(t('room.sessionMissing'));
        }
        const updatedContext = {
          campaign,
          activeSessionId: activeSession?.interviewSessionId || null,
          configurationKey: storedContext.configurationKey,
        };

        saveActiveInterviewContext(updatedContext);
        setInterviewContext(updatedContext);
        setSession(activeSession);
        setError('');
      })
      .catch((requestError) => {
        if (!isMounted) return;
        setError(requestError.message || t('room.loadFailed'));
      })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [t]);

  const handleCompleteSession = async () => {
    if (!session || isCompleting) return;

    setIsCompleting(true);
    setActionError('');
    setRoundMessage('');

    try {
      const campaign = await interviewSessionService.completeSession(session.interviewSessionId);
      const nextSession = (campaign.sessions || []).find((candidate) => candidate.status === 'Active') || null;
      const updatedContext = {
        campaign,
        activeSessionId: nextSession?.interviewSessionId || null,
        configurationKey: interviewContext?.configurationKey,
      };

      saveActiveInterviewContext(updatedContext);
      setInterviewContext(updatedContext);
      setSession(nextSession);
      setRoundMessage(nextSession
        ? t('room.previousRoundCompleted', {
          round: t(`rounds.${nextSession.interviewRoundType}`, nextSession.interviewRoundType),
        })
        : t('room.allRoundsCompleted'));
    } catch (requestError) {
      setActionError(requestError.message || t('room.completeFailed'));
    } finally {
      setIsCompleting(false);
    }
  };

  const formatTime = (seconds) => {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  };

  if (isLoading) {
    return (
      <UserLayout>
        <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10" lang={interviewLanguage}>
          <div className="min-h-[400px] bg-surface-2 border border-border rounded-2xl flex items-center justify-center">
            <Loader2 size={32} className="animate-spin text-primary-dark" />
          </div>
        </div>
      </UserLayout>
    );
  }

  if (error) {
    return (
      <UserLayout>
        <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10" lang={interviewLanguage}>
          <div className="min-h-[400px] bg-surface-2 border border-border rounded-2xl p-10 flex flex-col items-center justify-center text-center">
            <AlertCircle size={48} className="text-error mb-4" />
            <h2 className="text-xl font-semibold text-text-primary mb-2">{t('room.openFailed')}</h2>
            <p className="text-text-secondary mb-8 max-w-md">{error}</p>
            <button
              type="button"
              onClick={() => navigate(error ? USER_ROUTES.INTERVIEW_SETUP : USER_ROUTES.DEVICE_CHECK)}
              className="bg-primary-dark hover:bg-primary text-white px-6 py-3 rounded-xl transition-colors flex items-center gap-2 text-sm font-medium"
            >
              <ArrowLeft size={18} /> {t('room.backToSetup')}
            </button>
          </div>
        </div>
      </UserLayout>
    );
  }

  return (
    <UserLayout>
      <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10 flex flex-col min-h-[calc(100vh-100px)]" lang={interviewLanguage}>
        {/* Main Interface */}
        <section className="flex-1 flex flex-col bg-surface-2 border border-border rounded-2xl shadow-sm overflow-hidden relative">
          
          {/* Header */}
          <header className="flex items-center justify-between p-6 border-b border-border">
            <div className="text-text-primary font-bold text-lg tracking-wider">
              AI-SPEIS
            </div>
            <button 
              type="button"
              className="text-xs font-semibold text-text-secondary bg-surface-1 hover:bg-border px-4 py-2 rounded-full transition-colors border border-border hover:border-border-strong"
            >
              {t('room.feedback')}
            </button>
          </header>

          {roundMessage && (
            <div className="mx-6 mt-6 rounded-xl border border-primary-light bg-primary-xlight px-4 py-3 text-sm text-primary-dark" role="status">
              {roundMessage}
            </div>
          )}
          {actionError && (
            <div className="mx-6 mt-6 flex items-center gap-2 rounded-xl border border-error/30 bg-error/10 px-4 py-3 text-sm text-error" role="alert">
              <AlertCircle size={18} />
              {actionError}
            </div>
          )}

          {/* Main Content */}
          {interviewContext?.campaign?.status === 'Completed' ? (
            <main className="flex-1 flex flex-col items-center justify-center w-full px-6 py-12 text-center">
              <CheckCircle2 size={56} className="text-success mb-5" />
              <h1 className="text-3xl font-semibold text-text-primary mb-3">{t('room.completedTitle')}</h1>
              <p className="text-text-secondary max-w-lg">{t('room.completedDescription')}</p>
            </main>
          ) : (
            <main className="flex-1 flex flex-col items-center justify-center w-full px-6 py-12">
              <p className="text-text-secondary text-sm mb-2 font-medium">
                {t('room.round', {
                  round: t(`rounds.${session?.interviewRoundType}`, session?.interviewRoundType),
                })}
              </p>
              <p className="text-text-secondary text-sm mb-5 font-medium">{t('room.interviewerAsks')}</p>
              <h1 className="text-2xl md:text-3xl lg:text-4xl font-semibold text-center text-text-primary mb-16 leading-snug tracking-tight max-w-2xl">
                {session?.currentQuestion || t('room.fallbackQuestion')}
              </h1>
              <div className="relative flex items-center justify-center w-24 h-24 mt-4">
                <div className="absolute w-16 h-16 bg-primary rounded-full animate-ping opacity-40"></div>
                <div className="relative w-12 h-12 bg-primary-dark rounded-full shadow-[0_0_24px_rgba(111,182,232,0.6)]"></div>
              </div>
            </main>
          )}

          {/* Footer */}
          <footer className="flex items-center justify-between p-6 border-t border-border bg-surface-1">
            {/* Left Timer */}
            <div className="flex items-center gap-2 bg-surface-2 border border-border text-text-primary px-4 py-2.5 rounded-full text-sm font-medium">
              <Clock size={16} className="text-primary-dark" />
              <span
                className="w-10 text-center tracking-wide"
                title={t('room.durationTitle', { minutes: interviewContext?.campaign?.durationMinutes || 0 })}
              >
                {formatTime(timer)}
              </span>
            </div>

            {/* Center Controls */}
            <div className="flex items-center gap-2 md:gap-4">
              <button 
                type="button"
                className="text-sm font-medium text-text-secondary hover:text-text-primary bg-transparent hover:bg-border/50 px-5 py-2.5 rounded-full transition-colors"
              >
                {t('room.differentQuestion')}
              </button>
              {interviewContext?.campaign?.status === 'Completed' ? (
                <button
                  type="button"
                  onClick={() => navigate(USER_ROUTES.DASHBOARD)}
                  className="text-sm font-medium text-white bg-primary-dark hover:bg-primary px-6 py-2.5 rounded-full transition-colors shadow-sm"
                >
                  {t('room.home')}
                </button>
              ) : (
                <button
                  type="button"
                  onClick={handleCompleteSession}
                  disabled={isCompleting || timer === 0}
                  className="text-sm font-medium text-white bg-primary-dark hover:bg-primary disabled:cursor-not-allowed disabled:opacity-50 px-6 py-2.5 rounded-full transition-colors shadow-sm flex items-center gap-2"
                >
                  {isCompleting && <Loader2 size={16} className="animate-spin" />}
                  {t('room.completeRound')}
                </button>
              )}
            </div>

            {/* Right Action */}
            <button 
              type="button"
              className="hidden md:flex items-center gap-2 text-sm font-medium text-text-primary bg-surface-2 hover:bg-border border border-border px-5 py-2.5 rounded-full transition-colors"
            >
              <FileText size={16} className="text-primary-dark" />
              <span>{t('room.transcript')}</span>
            </button>
            
            {/* Mobile Right Action */}
            <button 
              type="button"
              className="flex md:hidden items-center justify-center w-10 h-10 text-text-primary bg-surface-2 hover:bg-border border border-border rounded-full transition-colors"
              aria-label={t('room.transcript')}
            >
              <FileText size={16} className="text-primary-dark" />
            </button>
          </footer>
        </section>
      </div>
    </UserLayout>
  );
}

export default AIInterviewRoomPage;

