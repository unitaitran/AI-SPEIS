import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ArrowRight, CalendarDays, TrendingUp, Target } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import { beginNewInterviewCampaign } from '../../utils/interviewContext';
import SkillHistoryModal from '../../components/dashboard/SkillHistoryModal';
import QuickActionsSection from '../../components/dashboard/QuickActionsSection';

function DashboardPage() {
  const { t, i18n } = useTranslation('dashboard');
  const [user, setUser] = useState(null);
  const [remainingInterviewQuota, setRemainingInterviewQuota] = useState(null);
  const [maxInterviewQuota, setMaxInterviewQuota] = useState(null);
  const [planName, setPlanName] = useState('Basic');
  const [selectedSkillForModal, setSelectedSkillForModal] = useState(null);
  const [capabilities, setCapabilities] = useState([
    { code: 'PROFESSIONAL_KNOWLEDGE', label: 'Kiến thức chuyên môn', labelEn: 'Professional Knowledge', score: 0 },
    { code: 'COMMUNICATION_SKILLS', label: 'Kỹ năng giao tiếp', labelEn: 'Communication Skills', score: 0 },
    { code: 'CV_UNDERSTANDING', label: 'Hiểu biết về CV', labelEn: 'CV Understanding', score: 0 },
    { code: 'PROBLEM_SOLVING', label: 'Giải quyết vấn đề', labelEn: 'Problem Solving', score: 0 },
  ]);

  useEffect(() => {
    // Try to load user from localStorage
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        setUser(JSON.parse(userStr));
      } catch (e) { }
    }
  }, []);

  useEffect(() => {
    let isMounted = true;
    const loadCapabilities = async () => {
      try {
        const data = await interviewSessionService.getUserCapabilities();
        if (isMounted && Array.isArray(data) && data.length > 0) {
          const map = {};
          const historyMap = {};
          data.forEach((item) => {
            const rawCode = item.code || item.Code;
            const itemCode = rawCode ? String(rawCode).toUpperCase() : '';
            const itemScore = item.score ?? item.Score;
            const itemHistory = item.history || item.History || [];
            if (itemCode && itemScore != null) {
              map[itemCode] = Number(itemScore);
              historyMap[itemCode] = itemHistory;
            }
          });
          setCapabilities([
            { code: 'PROFESSIONAL_KNOWLEDGE', label: 'Kiến thức chuyên môn', labelEn: 'Professional Knowledge', score: map.PROFESSIONAL_KNOWLEDGE ?? 0, history: historyMap.PROFESSIONAL_KNOWLEDGE || [] },
            { code: 'COMMUNICATION_SKILLS', label: 'Kỹ năng giao tiếp', labelEn: 'Communication Skills', score: map.COMMUNICATION_SKILLS ?? 0, history: historyMap.COMMUNICATION_SKILLS || [] },
            { code: 'CV_UNDERSTANDING', label: 'Hiểu biết về CV', labelEn: 'CV Understanding', score: map.CV_UNDERSTANDING ?? 0, history: historyMap.CV_UNDERSTANDING || [] },
            { code: 'PROBLEM_SOLVING', label: 'Giải quyết vấn đề', labelEn: 'Problem Solving', score: map.PROBLEM_SOLVING ?? 0, history: historyMap.PROBLEM_SOLVING || [] },
          ]);
        }
      } catch (err) {
        console.log('Capabilities load info:', err?.message || err);
      }
    };
    loadCapabilities();
    return () => { isMounted = false; };
  }, []);

  const [subscriptionExpiresAt, setSubscriptionExpiresAt] = useState(null);

  useEffect(() => {
    let isMounted = true;
    const loadQuota = async () => {
      try {
        const quota = await interviewSessionService.getQuota();
        if (isMounted) {
          setRemainingInterviewQuota(quota.remainingInterviewQuota);
          setMaxInterviewQuota(quota.maxInterviewQuota ?? null);
          setPlanName(quota.planName || 'Basic');
          setSubscriptionExpiresAt(quota.subscriptionExpiresAt || null);
        }
      } catch {
        if (isMounted) {
          try {
            const userStr = localStorage.getItem('user');
            const user = userStr ? JSON.parse(userStr) : null;
            if (user?.isPremium) {
              setPlanName('Premium');
              setRemainingInterviewQuota((prev) => prev ?? user.remainingInterviewQuota ?? 15);
              setMaxInterviewQuota(15);
              setSubscriptionExpiresAt(user.premiumExpiresAt || null);
            }
          } catch {
            // Best effort fallback
          }
        }
      }
    };
    const handleQuotaChanged = (event) => {
      const nextQuota = event.detail?.remainingInterviewQuota;
      if (Number.isInteger(nextQuota)) {
        setRemainingInterviewQuota(nextQuota);
        if (Number.isInteger(event.detail?.maxInterviewQuota)) setMaxInterviewQuota(event.detail.maxInterviewQuota);
        if (typeof event.detail?.planName === 'string' && event.detail.planName.trim()) setPlanName(event.detail.planName);
        if (event.detail?.subscriptionExpiresAt) setSubscriptionExpiresAt(event.detail.subscriptionExpiresAt);
      }
      else loadQuota();
    };
    loadQuota();
    window.addEventListener('interview:quota-changed', handleQuotaChanged);
    return () => {
      isMounted = false;
      window.removeEventListener('interview:quota-changed', handleQuotaChanged);
    };
  }, []);

  const [totalSessionCount, setTotalSessionCount] = useState(0);
  const [latestInterview, setLatestInterview] = useState(null);

  useEffect(() => {
    let isMounted = true;
    const loadTotalSessions = async () => {
      try {
        const campaigns = await interviewSessionService.getMyCampaigns();
        if (isMounted && Array.isArray(campaigns)) {
          setTotalSessionCount(campaigns.length);
          if (campaigns.length > 0) {
            const sorted = [...campaigns].sort((a, b) => {
              const dateA = new Date(a.createdAt || a.startedAt || 0).getTime();
              const dateB = new Date(b.createdAt || b.startedAt || 0).getTime();
              return dateB - dateA;
            });
            const latest = sorted[0];
            const dateObj = new Date(latest.createdAt || latest.startedAt || Date.now());
            const locale = (i18n?.language || '').toLowerCase().startsWith('en') ? 'en-US' : 'vi-VN';
            const formattedDate = dateObj.toLocaleDateString(locale, { day: '2-digit', month: '2-digit', year: 'numeric' });

            const jobTitle = latest.jobTitle || latest.JobTitle || latest.roleTarget || latest.positionTitle || latest.jdTitle || (latest.mode === 'RealTest' ? 'Phỏng vấn mô phỏng' : 'Luyện tập theo kỹ năng');
            const scoreVal = latest.overallScore ?? latest.OverallScore ?? latest.score;

            setLatestInterview({
              jobTitle,
              score: scoreVal != null && Number(scoreVal) > 0 ? Number(scoreVal).toFixed(1) : null,
              date: formattedDate,
              campaignId: latest.interviewCampaignId || latest.id
            });
          }
        }
      } catch { }
    };
    loadTotalSessions();
    return () => { isMounted = false; };
  }, [i18n?.language]);

  const validScores = capabilities.filter((c) => c.score > 0);
  const avgScore = validScores.length > 0
    ? (validScores.reduce((sum, c) => sum + c.score, 0) / validScores.length).toFixed(1)
    : '0.0';

  const formatExpirationDate = (dateStr) => {
    if (!dateStr) return null;
    try {
      const d = new Date(dateStr);
      if (Number.isNaN(d.getTime())) return null;
      const locale = (i18n?.language || '').toLowerCase().startsWith('en') ? 'en-US' : 'vi-VN';
      return d.toLocaleDateString(locale, { day: '2-digit', month: '2-digit', year: 'numeric' });
    } catch {
      return null;
    }
  };

  const expDateFormatted = formatExpirationDate(subscriptionExpiresAt);
  const quotaSubtext = planName === 'Premium'
    ? (expDateFormatted ? `${t('stats.quota_expires', 'Hạn gói Premium')}: ${expDateFormatted}` : t('stats.premium_active', 'Gói Premium đang kích hoạt'))
    : t('stats.free_plan', 'Gói Miễn phí');

  const stats = [
    {
      label: t('stats.interviews', 'SỐ BUỔI PHỎNG VẤN'),
      value: String(totalSessionCount),
      unit: t('stats.unit_session', 'buổi'),
      icon: CalendarDays,
      color: 'text-blue-500',
      bg: 'bg-blue-50',
      onClick: () => navigate(USER_ROUTES.INTERVIEW_HISTORY)
    },
    {
      label: t('stats.avg_score', 'ĐIỂM TRUNG BÌNH'),
      value: avgScore,
      unit: '/ 10',
      icon: Target,
      color: 'text-primary-dark',
      bg: 'bg-primary-xlight',
      onClick: () => navigate(USER_ROUTES.INTERVIEW_HISTORY)
    },
    {
      label: t('stats.quota', 'QUOTA CÒN LẠI'),
      value: remainingInterviewQuota ?? '—',
      unit: `/ ${maxInterviewQuota ?? '—'} ${t('stats.unit_times', 'lượt')}`,
      subtext: quotaSubtext,
      icon: TrendingUp,
      color: 'text-green-500',
      bg: 'bg-green-50',
      onClick: () => navigate(USER_ROUTES.PACKAGES)
    },
  ];

  const quotaExhausted = remainingInterviewQuota === 0;

  const isEnglish = (i18n?.language || '').toLowerCase().startsWith('en');

  return (
    <UserLayout>
      <div className="space-y-8 pb-10 animate-pageEntrance">

        {/* Page Header */}
        <section>
          <h1 className="text-3xl font-bold text-text-primary tracking-tight mb-1">Dashboard</h1>
          <p className="text-base text-text-secondary">
            {t('greeting_prefix', 'Chào buổi sáng,')} <span className="font-semibold text-primary-dark">{user ? user.fullName : 'User'}</span> 👋
          </p>
        </section>

        {/* Stats Row */}
        <section className="grid grid-cols-1 sm:grid-cols-3 gap-5">
          {stats.map((stat, idx) => (
            <div
              key={idx}
              onClick={stat.onClick}
              className="bg-surface-2 p-5 rounded-xl border border-border shadow-sm flex flex-col justify-center relative overflow-hidden group hover:border-primary hover:shadow-md hover:-translate-y-1 transition-all duration-300 cursor-pointer"
            >
              <div className={`absolute top-0 right-0 w-16 h-16 -mr-4 -mt-4 rounded-full opacity-20 transition-transform duration-500 group-hover:scale-[1.8] ${stat.bg}`}></div>
              <div className="flex items-center justify-between mb-3">
                <span className="text-[11px] font-bold text-text-secondary uppercase tracking-widest line-clamp-1">
                  {stat.label}
                </span>
                <div className={`p-1.5 rounded-lg ${stat.bg} ${stat.color}`}>
                  <stat.icon size={16} />
                </div>
              </div>
              <div className="flex items-baseline relative z-10">
                <span className="text-3xl font-bold text-text-primary mr-1.5">{stat.value}</span>
                <span className="text-sm font-medium text-text-secondary">{stat.unit}</span>
              </div>
              {stat.subtext && (
                <div className="mt-2.5 text-xs font-semibold text-text-secondary relative z-10 flex items-center gap-1.5">
                  <span className="inline-block w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                  <span>{stat.subtext}</span>
                </div>
              )}
            </div>
          ))}
        </section>

        {/* Content Row 1: CTA and Chart */}
        <section className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Frameless Interactive Mascot CTA (Speech Bubble + Mascot Avatar) */}
          <div
            onClick={() => {
              if (quotaExhausted) return;
              beginNewInterviewCampaign();
              navigate(USER_ROUTES.INTERVIEW_MODE);
            }}
            className={`lg:col-span-4 flex flex-col items-center justify-center p-4 relative group transition-all duration-300 ${
              quotaExhausted ? 'cursor-not-allowed opacity-80' : 'cursor-pointer'
            }`}
          >
            {/* Speech Bubble above Mascot */}
            <div className="relative mb-3 bg-surface-2 text-primary-dark dark:bg-slate-800 dark:text-blue-400 font-extrabold text-sm px-5 py-2.5 rounded-2xl shadow-sm border border-border/80 group-hover:bg-primary group-hover:text-white group-hover:border-primary transition-all duration-300 flex items-center gap-2">
              <span>{t('banner.mascot_speech', 'Luyện tập phỏng vấn ngay! 🚀')}</span>
              <ArrowRight size={16} className="transform group-hover:translate-x-1 transition-transform" />
              
              {/* Speech Bubble Tail */}
              <div className="absolute top-full left-1/2 -translate-x-1/2 w-0 h-0 border-l-[7px] border-l-transparent border-r-[7px] border-r-transparent border-t-[8px] border-t-surface-2 dark:border-t-slate-800 group-hover:border-t-primary transition-colors duration-300" />
            </div>

            {/* Circular Mascot Avatar Container */}
            <div className="relative w-44 h-44 sm:w-48 sm:h-48 rounded-full p-2 bg-surface-2 border border-border shadow-md group-hover:shadow-xl group-hover:scale-105 transition-all duration-300 flex items-center justify-center overflow-hidden">
              <img
                src={process.env.PUBLIC_URL + '/ideaing_mascot.jpg'}
                alt="AI Mascot Practice"
                className="w-full h-full object-cover rounded-full"
              />
            </div>

            {quotaExhausted && (
              <p className="mt-3 text-xs text-rose-500 font-semibold text-center">
                {t('banner.quota_exhausted', 'Bạn đã hết lượt phỏng vấn. Vui lòng nâng cấp gói.')}
              </p>
            )}
          </div>

          {/* Skill Progress Chart */}
          <div className="lg:col-span-8 bg-surface-2 p-6 rounded-2xl border border-border shadow-sm flex flex-col">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-text-primary">{t('chart.title', 'Năng lực tổng hợp')}</h3>
              <button
                className="text-xs font-semibold tracking-wider text-text-secondary hover:text-primary transition-colors border-b border-transparent hover:border-primary uppercase cursor-pointer"
                onClick={() => setSelectedSkillForModal(capabilities[0])}
              >
                {t('chart.view_details', 'XEM CHI TIẾT')}
              </button>
            </div>

            {/* Simple CSS Bar Chart implementation matching the mockup */}
            <div className="flex-1 min-h-[220px] flex items-end pt-4 relative">
              {/* Y-axis grid lines */}
              <div className="absolute inset-0 flex flex-col justify-between z-0 pb-8 pointer-events-none">
                <div className="w-full border-b border-dashed border-border flex items-end justify-start">
                  <span className="text-[10px] text-text-disabled -mb-2 -ml-4 bg-surface-2 pr-1">10</span>
                </div>
                <div className="w-full border-b border-dashed border-border flex items-end justify-start">
                  <span className="text-[10px] text-text-disabled -mb-2 -ml-4 bg-surface-2 pr-1">5</span>
                </div>
                <div className="w-full border-b border-solid border-border flex items-end justify-start">
                  <span className="text-[10px] text-text-disabled -mb-2 -ml-4 bg-surface-2 pr-1">0</span>
                </div>
              </div>

              {/* Bars */}
              <div className="w-full h-full flex justify-around items-end z-10 pb-8 pl-4">
                {capabilities.map((item, idx) => {
                  const numScore = Number(item.score) || 0;
                  const fallbackAvg = Number(avgScore) || 0;
                  const displayScore = numScore > 0 ? numScore : fallbackAvg;
                  const barHeightPercent = Math.min(100, Math.max(6, (displayScore / 10) * 100));
                  return (
                    <div
                      key={idx}
                      className="h-full flex flex-col justify-end items-center w-1/4 relative group px-2 cursor-pointer"
                      onClick={() => setSelectedSkillForModal({ ...item, score: displayScore })}
                      title={isEnglish ? 'Click to view skill fluctuation line chart' : 'Bấm để xem biểu đồ đường diễn biến độ lên xuống'}
                    >
                      {/* Score Label above bar */}
                      <span className="mb-1 text-[12px] font-extrabold text-[#0284c7] group-hover:scale-110 transition-transform">
                        {displayScore > 0 ? displayScore.toFixed(1) : '0.0'}
                      </span>
                      {/* Tooltip */}
                      <div className="absolute bottom-full mb-2 opacity-0 group-hover:opacity-100 transition-opacity bg-[#0f172a] text-white text-[11px] font-bold py-1 px-2.5 rounded shadow pointer-events-none whitespace-nowrap z-20">
                        {isEnglish ? 'Click to view trend' : 'Bấm để xem chi tiết độ lên xuống'}
                      </div>
                      <div
                        className="w-full max-w-[48px] rounded-t-lg transition-all duration-300 shadow-sm group-hover:brightness-110 group-hover:shadow-lg group-hover:-translate-y-1"
                        style={{
                          height: `${barHeightPercent}%`,
                          background: 'linear-gradient(180deg, #38bdf8 0%, #0284c7 100%)',
                          minHeight: '8px',
                        }}
                      />
                    </div>
                  );
                })}
              </div>

              {/* X-axis labels */}
              <div className="absolute bottom-0 left-4 right-0 flex justify-around">
                {capabilities.map((item, idx) => {
                  const numScore = Number(item.score) || 0;
                  const fallbackAvg = Number(avgScore) || 0;
                  const displayScore = numScore > 0 ? numScore : fallbackAvg;
                  return (
                    <span
                      key={idx}
                      className="text-[11px] font-medium text-text-secondary hover:text-primary-dark w-1/4 text-center truncate px-1 cursor-pointer transition-colors"
                      title={isEnglish ? item.labelEn : item.label}
                      onClick={() => setSelectedSkillForModal({ ...item, score: displayScore })}
                    >
                      {isEnglish ? item.labelEn : item.label}
                    </span>
                  );
                })}
              </div>
            </div>
          </div>
        </section>

        {/* Quick Actions Section (3 Activity Cards) */}
        <QuickActionsSection latestInterview={latestInterview} />

        {/* Skill Trend Modal */}
        <SkillHistoryModal
          skill={selectedSkillForModal}
          onClose={() => setSelectedSkillForModal(null)}
        />
      </div>
    </UserLayout>
  );
}

export default DashboardPage;
