import React, { useEffect, useMemo, useState } from 'react';
import { ArrowLeft, Loader2, LogOut, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import UserLayout from '../../layouts/user/UserLayout';
import interviewSessionService from '../../services/InterviewSessionService';
import { getActiveInterviewContext, getRoundOrder } from '../../utils/interviewContext';
import BehavioralInterviewPage from './BehavioralInterviewPage';
import TechnicalInterviewPage from './TechnicalInterviewPage';
import { navigate } from '../../routes/navigation';
import { getCodingInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';

function AIInterviewRoomPage({ sessionId }) {
  const { t } = useTranslation('interview');
  const storedSession = useMemo(() => {
    const context = getActiveInterviewContext();
    const targetId = sessionId || context?.activeSessionId;
    return context?.campaign?.sessions?.find((session) => (
      String(session.interviewSessionId) === String(targetId)
    )) || null;
  }, [sessionId]);
  const resolvedSessionId = sessionId || storedSession?.interviewSessionId || null;
  const [roundType, setRoundType] = useState(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [isLoading, setIsLoading] = useState(Boolean(resolvedSessionId));
  const [isEnding, setIsEnding] = useState(false);
  const [error, setError] = useState(() => (
    resolvedSessionId ? null : new Error('Interview session ID is missing')
  ));

  useEffect(() => {
    if (!resolvedSessionId) return undefined;
    let active = true;
    setIsLoading(true);
    setError(null);
    interviewSessionService.getSession(resolvedSessionId)
      .then((session) => {
        if (!active) return;
        const nextRoundType = session?.interviewRoundType;
        const order = getRoundOrder(nextRoundType);
        if (order === Number.MAX_SAFE_INTEGER) {
          throw new Error('Interview round type is not supported');
        }
        if (order === 2) {
          if (session?.status !== 'Active') {
            interviewSessionService.startSession(resolvedSessionId).catch(() => {});
          }
          navigate(getCodingInterviewRoomPath(resolvedSessionId), { replace: true });
          return;
        }
        setRoundType(order === 0 ? 'Behavior' : 'Technical');
      })
      .catch((loadError) => {
        if (active) setError(loadError);
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => { active = false; };
  }, [reloadKey, resolvedSessionId]);

  const handleEndSession = async () => {
    if (!resolvedSessionId || isEnding) return;
    setIsEnding(true);
    setError(null);
    try {
      await interviewSessionService.completeSession(resolvedSessionId);
      navigate(USER_ROUTES.INTERVIEW_SETUP, { replace: true });
    } catch (endError) {
      setError(endError);
    } finally {
      setIsEnding(false);
    }
  };

  if (roundType === 'Behavior') return <BehavioralInterviewPage sessionId={resolvedSessionId} />;
  if (roundType === 'Technical') return <TechnicalInterviewPage sessionId={resolvedSessionId} />;

  return (
    <UserLayout compactSidebar immersive>
      <section className="flex h-full items-center justify-center bg-surface-1 p-6 text-center" role="status">
        <div>
          {!error && isLoading ? <Loader2 className="mx-auto mb-4 animate-spin text-primary-dark" size={32} /> : null}
          <h1 className="text-xl font-semibold text-text-primary">
            {error ? t('room.unableToOpen', 'Unable to open the interview room') : t('room.preparing', 'Preparing your interview room...')}
          </h1>
          {error ? (
            <>
              <p className="mt-2 text-text-secondary">{error.message || 'Please try loading this session again.'}</p>
              <div className="mt-5 flex flex-wrap justify-center gap-3">
                <button type="button" className="technical-secondary-button" onClick={() => setReloadKey((key) => key + 1)}>
                  <RefreshCw size={18} />Retry
                </button>
                <button type="button" className="technical-secondary-button" onClick={handleEndSession} disabled={isEnding || !resolvedSessionId}>
                  {isEnding ? <Loader2 size={18} className="animate-spin" /> : <LogOut size={18} />}
                  End current round
                </button>
                <button type="button" className="technical-secondary-button" onClick={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}>
                  <ArrowLeft size={18} />Back to setup
                </button>
              </div>
            </>
          ) : null}
        </div>
      </section>
    </UserLayout>
  );
}

export default AIInterviewRoomPage;
