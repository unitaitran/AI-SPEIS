import React, { useEffect, useState } from 'react';
import { AlertCircle, ArrowLeft, Loader2, Clock, FileText } from 'lucide-react';
import InterviewProgressStepper from '../../components/user/InterviewProgressStepper/InterviewProgressStepper';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import { getActiveInterviewContext, saveActiveInterviewContext } from '../../utils/interviewContext';

function AIInterviewRoomPage() {
  const [interviewContext, setInterviewContext] = useState(() => getActiveInterviewContext());
  const [session, setSession] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [timer, setTimer] = useState(9 * 60 + 18);

  useEffect(() => {
    const interval = setInterval(() => {
      setTimer((prev) => (prev > 0 ? prev - 1 : 0));
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    const storedContext = getActiveInterviewContext();
    const activeSessionId = storedContext?.activeSessionId;

    if (!storedContext?.campaign?.interviewCampaignId || !activeSessionId) {
      setError('Không tìm thấy campaign hoặc phiên phỏng vấn đang hoạt động.');
      setIsLoading(false);
      return undefined;
    }

    let isMounted = true;
    interviewSessionService.getSession(activeSessionId)
      .then((activeSession) => {
        if (!isMounted) return;

        const updatedCampaign = {
          ...storedContext.campaign,
          sessions: (storedContext.campaign.sessions || []).map((campaignSession) => (
            campaignSession.interviewSessionId === activeSession.interviewSessionId
              ? activeSession
              : campaignSession
          )),
        };
        const updatedContext = {
          campaign: updatedCampaign,
          activeSessionId: activeSession.interviewSessionId,
        };

        saveActiveInterviewContext(updatedContext);
        setInterviewContext(updatedContext);
        setSession(activeSession);
        setError('');
      })
      .catch((requestError) => {
        if (!isMounted) return;
        setError(requestError.message || 'Không thể tải phiên phỏng vấn.');
      })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const formatTime = (seconds) => {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  };

  if (isLoading) {
    return (
      <UserLayout>
        <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10">
          <InterviewProgressStepper activeStep={3} />
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
        <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10">
          <InterviewProgressStepper activeStep={3} />
          <div className="min-h-[400px] bg-surface-2 border border-border rounded-2xl p-10 flex flex-col items-center justify-center text-center">
            <AlertCircle size={48} className="text-error mb-4" />
            <h2 className="text-xl font-semibold text-text-primary mb-2">Không thể mở phòng phỏng vấn</h2>
            <p className="text-text-secondary mb-8 max-w-md">{error}</p>
            <button
              type="button"
              onClick={() => navigate(error ? USER_ROUTES.INTERVIEW_SETUP : USER_ROUTES.DEVICE_CHECK)}
              className="bg-primary-dark hover:bg-primary text-white px-6 py-3 rounded-xl transition-colors flex items-center gap-2 text-sm font-medium"
            >
              <ArrowLeft size={18} /> Quay lại thiết lập
            </button>
          </div>
        </div>
      </UserLayout>
    );
  }

  return (
    <UserLayout>
      <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10 flex flex-col min-h-[calc(100vh-100px)]">
        <InterviewProgressStepper activeStep={3} />
        
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
              Send feedback
            </button>
          </header>

          {/* Main Content */}
          <main className="flex-1 flex flex-col items-center justify-center w-full px-6 py-12">
            <p className="text-text-secondary text-sm mb-5 font-medium">The interviewer asks...</p>
            <h1 className="text-2xl md:text-3xl lg:text-4xl font-semibold text-center text-text-primary mb-16 leading-snug tracking-tight max-w-2xl">
              {session?.currentQuestion || "Can you provide an example of how you manage conflict?"}
            </h1>
            
            {/* Pulsing Dot */}
            <div className="relative flex items-center justify-center w-24 h-24 mt-4">
              <div className="absolute w-16 h-16 bg-primary rounded-full animate-ping opacity-40"></div>
              <div className="relative w-12 h-12 bg-primary-dark rounded-full shadow-[0_0_24px_rgba(111,182,232,0.6)]"></div>
            </div>
          </main>

          {/* Footer */}
          <footer className="flex items-center justify-between p-6 border-t border-border bg-surface-1">
            {/* Left Timer */}
            <div className="flex items-center gap-2 bg-surface-2 border border-border text-text-primary px-4 py-2.5 rounded-full text-sm font-medium">
              <Clock size={16} className="text-primary-dark" />
              <span className="w-10 text-center tracking-wide">{formatTime(timer)}</span>
            </div>

            {/* Center Controls */}
            <div className="flex items-center gap-2 md:gap-4">
              <button 
                type="button"
                className="text-sm font-medium text-text-secondary hover:text-text-primary bg-transparent hover:bg-border/50 px-5 py-2.5 rounded-full transition-colors"
              >
                Try a different question
              </button>
              <button 
                type="button"
                className="text-sm font-medium text-white bg-primary-dark hover:bg-primary px-6 py-2.5 rounded-full transition-colors shadow-sm"
              >
                Submit interview
              </button>
            </div>

            {/* Right Action */}
            <button 
              type="button"
              className="hidden md:flex items-center gap-2 text-sm font-medium text-text-primary bg-surface-2 hover:bg-border border border-border px-5 py-2.5 rounded-full transition-colors"
            >
              <FileText size={16} className="text-primary-dark" />
              <span>Transcript</span>
            </button>
            
            {/* Mobile Right Action */}
            <button 
              type="button"
              className="flex md:hidden items-center justify-center w-10 h-10 text-text-primary bg-surface-2 hover:bg-border border border-border rounded-full transition-colors"
              aria-label="Transcript"
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

