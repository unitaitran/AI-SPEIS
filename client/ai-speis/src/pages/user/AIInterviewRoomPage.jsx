import React, { useEffect, useMemo, useState } from 'react';
import { Loader2 } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import interviewSessionService from '../../services/InterviewSessionService';
import { getActiveInterviewContext } from '../../utils/interviewContext';
import BehavioralInterviewPage from './BehavioralInterviewPage';
import TechnicalInterviewPage from './TechnicalInterviewPage';

function AIInterviewRoomPage({ sessionId }) {
  const storedSession = useMemo(() => {
    const context = getActiveInterviewContext();
    const targetId = sessionId || context?.activeSessionId;
    return context?.campaign?.sessions?.find((session) => (
      String(session.interviewSessionId) === String(targetId)
    )) || null;
  }, [sessionId]);
  const resolvedSessionId = sessionId || storedSession?.interviewSessionId || null;
  const [roundType, setRoundType] = useState(storedSession?.interviewRoundType || null);
  const [error, setError] = useState(() => (
    resolvedSessionId ? null : new Error('Interview session ID is missing')
  ));

  useEffect(() => {
    if (roundType || !resolvedSessionId) return undefined;
    let active = true;
    interviewSessionService.getSession(resolvedSessionId)
      .then((session) => {
        if (active) setRoundType(session?.interviewRoundType || 'Technical');
      })
      .catch((loadError) => {
        if (active) setError(loadError);
      });
    return () => { active = false; };
  }, [resolvedSessionId, roundType]);

  if (roundType === 'Behavior') return <BehavioralInterviewPage sessionId={resolvedSessionId} />;
  if (roundType) return <TechnicalInterviewPage sessionId={resolvedSessionId} />;

  return (
    <UserLayout compactSidebar immersive>
      <section className="flex h-full items-center justify-center bg-surface-1 p-6 text-center" role="status">
        <div>
          {error ? null : <Loader2 className="mx-auto mb-4 animate-spin text-primary-dark" size={32} />}
          <h1 className="text-xl font-semibold text-text-primary">
            {error ? 'Unable to open the interview room' : 'Preparing your interview room...'}
          </h1>
          {error ? <p className="mt-2 text-text-secondary">Please return to Interview Setup and try again.</p> : null}
        </div>
      </section>
    </UserLayout>
  );
}

export default AIInterviewRoomPage;
