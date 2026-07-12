import React, { useEffect, useState } from 'react';
import { AlertCircle, ArrowLeft, Clock3, Gauge, Layers3, Loader2, Mic, Sparkles } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import { getActiveInterviewContext, saveActiveInterviewContext } from '../../utils/interviewContext';

const ROUND_LABELS = {
  Behavior: 'Behavioral',
  Technical: 'Technical',
  Code: 'Coding',
};

const DIFFICULTY_LABELS = {
  Easy: 'Dễ',
  Medium: 'Trung bình',
  Hard: 'Khó',
};

function AIInterviewRoomPage() {
  const [interviewContext, setInterviewContext] = useState(() => getActiveInterviewContext());
  const [session, setSession] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

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

  const campaign = interviewContext?.campaign;

  return (
    <UserLayout>
      <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10">
        <section className="bg-surface-2 border border-border rounded-2xl shadow-sm p-6 md:p-10">
          <div className="flex items-start gap-4">
            <div className="w-12 h-12 rounded-xl bg-primary-xlight text-primary-dark flex items-center justify-center shrink-0">
              <Mic size={24} />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase text-primary-dark mb-2">AI Interview Room</p>
              <h1 className="text-2xl md:text-3xl font-bold text-text-primary mb-3">
                Phòng phỏng vấn AI
              </h1>
              <p className="text-sm text-text-secondary leading-relaxed max-w-2xl">
                Phiên phỏng vấn được tải từ campaign đã tạo ở bước Thiết lập.
              </p>
            </div>
          </div>

          {isLoading ? (
            <div className="mt-8 min-h-[160px] rounded-xl bg-surface-1 border border-border flex items-center justify-center gap-3 text-text-secondary">
              <Loader2 size={22} className="animate-spin text-primary-dark" />
              Đang tải phiên phỏng vấn...
            </div>
          ) : error ? (
            <div className="mt-8 p-5 rounded-xl bg-error-light border border-error flex items-start gap-3" role="alert">
              <AlertCircle size={20} className="text-error shrink-0 mt-0.5" />
              <div>
                <strong className="block text-sm text-text-primary mb-1">Không thể mở phòng phỏng vấn</strong>
                <p className="m-0 text-sm text-text-secondary">{error}</p>
              </div>
            </div>
          ) : (
            <>
              <div className="mt-8 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3" aria-label="Interview session context">
                <div className="p-4 rounded-xl bg-surface-1 border border-border">
                  <Layers3 size={18} className="text-primary-dark mb-2" />
                  <span className="block text-xs text-text-secondary mb-1">Vòng hiện tại</span>
                  <strong className="text-sm text-text-primary">{ROUND_LABELS[session?.interviewRoundType] || session?.interviewRoundType}</strong>
                </div>
                <div className="p-4 rounded-xl bg-surface-1 border border-border">
                  <Gauge size={18} className="text-primary-dark mb-2" />
                  <span className="block text-xs text-text-secondary mb-1">Độ khó</span>
                  <strong className="text-sm text-text-primary">{DIFFICULTY_LABELS[session?.difficulty] || session?.difficulty}</strong>
                </div>
                <div className="p-4 rounded-xl bg-surface-1 border border-border">
                  <Clock3 size={18} className="text-primary-dark mb-2" />
                  <span className="block text-xs text-text-secondary mb-1">Thời lượng</span>
                  <strong className="text-sm text-text-primary">{campaign?.durationMinutes} phút</strong>
                </div>
                <div className="p-4 rounded-xl bg-surface-1 border border-border">
                  <Sparkles size={18} className="text-primary-dark mb-2" />
                  <span className="block text-xs text-text-secondary mb-1">Campaign</span>
                  <strong className="text-sm text-text-primary">#{campaign?.interviewCampaignId}</strong>
                </div>
              </div>

              <div className="mt-6 p-5 rounded-xl bg-primary-xlight border border-primary-light flex items-start gap-3">
                <Sparkles size={20} className="text-primary-dark shrink-0 mt-0.5" />
                <p className="m-0 text-sm text-text-primary leading-relaxed">
                  Session #{session?.interviewSessionId} đang ở trạng thái {session?.status}. Phần hỏi và ghi nhận câu trả lời sẽ tiếp tục được triển khai trên session này.
                </p>
              </div>
            </>
          )}

          <button
            type="button"
            className="mt-8 inline-flex items-center gap-2 min-h-[44px] px-5 rounded-xl border border-border bg-surface-2 text-text-primary font-bold hover:bg-primary-xlight hover:border-primary-light transition-colors"
            onClick={() => navigate(error ? USER_ROUTES.INTERVIEW_SETUP : USER_ROUTES.DEVICE_CHECK)}
          >
            <ArrowLeft size={18} />
            {error ? 'Quay lại thiết lập' : 'Quay lại kiểm tra thiết bị'}
          </button>
        </section>
      </div>
    </UserLayout>
  );
}

export default AIInterviewRoomPage;
