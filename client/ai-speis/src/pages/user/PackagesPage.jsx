import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  AlertTriangle,
  CheckCircle2,
  Crown,
  RefreshCw,
  Star,
  Sparkles,
  X,
  Calendar,
  RotateCcw,
  Ticket,
  Zap,
  ShieldCheck,
  ArrowLeft
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import paymentService from '../../services/PaymentService';
import notify from '../../utils/notification';
import { useTranslation } from 'react-i18next';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import { API_BASE_URL } from '../../config/api';
import '../../styles/user/PackagesPage.css';

function formatVnd(amount, t) {
  if (amount === 0) return t ? t('free', 'Miễn phí') : 'Miễn phí';
  return `${amount.toLocaleString('vi-VN')} VND`;
}

function formatDate(dateStr) {
  if (!dateStr) return '—';
  const d = new Date(dateStr);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function PackagesPage() {
  const { t } = useTranslation('packages');
  const [isCreating, setIsCreating] = useState(false);
  const [loadingPackageId, setLoadingPackageId] = useState(null);
  const [error, setError] = useState('');
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);
  const [successModalMode, setSuccessModalMode] = useState('upgrade'); // 'renew' | 'upgrade'
  const [availablePlans, setAvailablePlans] = useState([]);
  const [subscriptionData, setSubscriptionData] = useState(null);
  const [selectedPackage, setSelectedPackage] = useState(null);
  const [useRewardPoints, setUseRewardPoints] = useState(false);

  const fallbackPackages = [
    {
      id: 'free',
      isFree: true,
      name: t('freePlanName', 'Gói Cơ Bản (Free)'),
      amount: 0,
      subtitle: t('freeSubtitle', 'Bắt đầu hành trình của bạn'),
      features: [
        t('freeFeature1', 'Trải nghiệm phỏng vấn AI cơ bản'),
        t('freeFeature2', 'Đánh giá kỹ năng tổng quan'),
        t('freeFeature3', '3 lượt phỏng vấn dùng thử miễn phí'),
      ],
    },
    {
      id: 1,
      priceId: 1,
      billingCycle: 1,
      isFree: false,
      name: t('premiumMonthly', 'Premium 1 Tháng'),
      amount: 59000,
      subtitle: t('premiumMonthlySubtitle', 'Lựa chọn phổ biến'),
      features: [
        t('premiumMonthlyFeature1', '15 lượt phỏng vấn AI toàn diện'),
        t('benefit3', 'Phân tích & Đánh giá chuyên sâu'),
        t('premiumMonthlyFeature3', 'Làm mới 15 lượt sau mỗi 30 ngày'),
      ],
    },
    {
      id: 2,
      priceId: 2,
      billingCycle: 2,
      isFree: false,
      name: t('premiumYearly', 'Premium 1 Năm'),
      amount: 599000,
      subtitle: t('premiumYearlySubtitle', 'Tiết kiệm nhất'),
      features: [
        t('premiumYearlyFeature1', '15 lượt phỏng vấn mỗi chu kỳ 30 ngày'),
        t('premiumYearlyFeature2', 'Làm mới 15 lượt ưu tiên mỗi tháng'),
        t('premiumYearlyFeature3', 'Báo cáo kỹ năng nâng cao'),
      ],
    }
  ];
  const PACKAGES = availablePlans.length > 0
    ? availablePlans.flatMap((plan) => {
      const baseFeatures = [
        plan.isFree
          ? `${plan.interviewQuota} ${t('freeInterviewQuotaText', 'lượt phỏng vấn miễn phí')}`
          : `${plan.interviewQuota} ${t('monthlyQuotaText', 'lượt phỏng vấn mỗi chu kỳ 30 ngày')}`,
        plan.aiTier === 'STANDARD'
          ? t('basicAiInterview', 'Phỏng vấn AI cơ bản')
          : t('comprehensiveAiInterview', 'Phỏng vấn AI toàn diện'),
        ...(plan.aiTier === 'STANDARD'
          ? [t('generalSkillAssessment', 'Đánh giá kỹ năng tổng quan')]
          : []),
        ...(plan.advancedAnalyticsEnabled
          ? [t('advancedAnalysis', 'Phân tích & Đánh giá nâng cao')]
          : []),
        ...(plan.quotaResetDays
          ? [t('quotaRefreshDays', 'Làm mới {{quota}} lượt sau mỗi {{days}} ngày', {
            quota: plan.interviewQuota,
            days: plan.quotaResetDays,
          })]
          : []),
      ];

      const planName = plan.isFree
        ? t('freePlanName', 'Gói Cơ Bản (Free)')
        : t('premiumPlanName', 'Premium');

      if (plan.isFree) {
        return [{
          id: `plan-${plan.planId}`,
          isFree: true,
          name: planName,
          subtitle: t('freeSubtitle', 'Bắt đầu hành trình của bạn'),
          amount: 0,
          features: baseFeatures
        }];
      }
      return (plan.prices || []).map((price) => ({
        id: `price-${price.priceId}`,
        priceId: price.priceId,
        billingCycle: price.billingCycle,
        isFree: false,
        name: `${planName} ${price.billingCycle === 2 ? t('yearlyCycleName', '1 Năm') : t('monthlyCycleName', '1 Tháng')}`,
        subtitle: price.billingCycle === 2 ? t('premiumYearlySubtitle', 'Tiết kiệm nhất') : t('premiumMonthlySubtitle', 'Lựa chọn phổ biến'),
        amount: price.amount,
        features: baseFeatures,
      }));
    })
    : fallbackPackages;
  const [, setIsVerifying] = useState(false);

  const [profileData, setProfileData] = useState(null);
  const [isPremiumUser, setIsPremiumUser] = useState(false);
  const [showPurchaseView, setShowPurchaseView] = useState(() => {
    return new URLSearchParams(window.location.search).get('purchase') === 'true';
  });

  const showQuotaResetCard = useMemo(() => {
    if (!isPremiumUser) return false;
    
    // Explicitly hide for Monthly / 1 Month packages
    const cycle = subscriptionData?.billingCycle;
    if (cycle === 'Monthly' || cycle === 1 || cycle === '1') return false;

    const planName = profileData?.subscriptionPlanName || profileData?.planName || '';
    if (planName.toLowerCase().includes('1 month') || planName.toLowerCase().includes('monthly')) return false;

    if (!subscriptionData?.quotaPeriodEndsAt || !subscriptionData?.subscriptionExpiresAt) return false;
    const quotaEnd = new Date(subscriptionData.quotaPeriodEndsAt).getTime();
    const subExpire = new Date(subscriptionData.subscriptionExpiresAt).getTime();
    if (Number.isNaN(quotaEnd) || Number.isNaN(subExpire)) return false;

    return (subExpire - quotaEnd) > (5 * 24 * 60 * 60 * 1000);
  }, [isPremiumUser, subscriptionData, profileData]);

  useEffect(() => {
    const handleUrlChange = () => {
      const isPurchase = new URLSearchParams(window.location.search).get('purchase') === 'true';
      if (isPurchase) {
        setShowPurchaseView(true);
      }
    };

    window.addEventListener('popstate', handleUrlChange);
    window.addEventListener('app:navigate', handleUrlChange);
    return () => {
      window.removeEventListener('popstate', handleUrlChange);
      window.removeEventListener('app:navigate', handleUrlChange);
    };
  }, []);

  const fetchSubscriptionInfo = async () => {
    try {
      const token = localStorage.getItem('token');
      if (!token) return null;
      let latestSubscription = null;

      const [profileRes, quotaRes, plansRes, subscriptionRes] = await Promise.all([
        fetch(`${API_BASE_URL}/api/users/me`, { headers: { Authorization: `Bearer ${token}` } }),
        fetch(`${API_BASE_URL}/api/InterviewSession/quota`, { headers: { Authorization: `Bearer ${token}` } }),
        fetch(`${API_BASE_URL}/api/subscription-plans`),
        fetch(`${API_BASE_URL}/api/subscription/me`, { headers: { Authorization: `Bearer ${token}` } })
      ]);

      if (profileRes.ok) {
        const pData = await profileRes.json();
        setProfileData(pData);
      }

      if (quotaRes.ok) {
        const qData = await quotaRes.json();
        if (qData.planName === 'Premium') {
          setIsPremiumUser(true);
        }
      }
      if (plansRes.ok) setAvailablePlans(await plansRes.json());
      if (subscriptionRes.ok) {
        const data = await subscriptionRes.json();
        latestSubscription = data;
        setSubscriptionData(data);
        setIsPremiumUser(data.planCode === 'PREMIUM');
      }
      return latestSubscription;
    } catch {
      // Ignore errors
      return null;
    }
  };

  useEffect(() => {
    fetchSubscriptionInfo();
  }, []);

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const applyTheme = () => setIsDarkMode(media.matches);
    applyTheme();
    media.addEventListener('change', applyTheme);

    return () => {
      media.removeEventListener('change', applyTheme);
    };
  }, []);

  // Check for MoMo payment callback in URL params
  useEffect(() => {
    const processPaymentCallback = async () => {
      const urlParams = new URLSearchParams(window.location.search);
      const orderId = urlParams.get('orderId') || urlParams.get('orderCode');
      const resultCode = urlParams.get('resultCode');
      const momoMessage = urlParams.get('message');

      if (!orderId) return;

      setIsVerifying(true);

      // Clean query string from browser bar immediately to prevent duplicate requests on F5
      window.history.replaceState({}, document.title, window.location.pathname);

      if (resultCode && resultCode !== '0') {
        setError(momoMessage || `${t('paymentError', 'Thanh toán không thành công')} hoặc đã bị hủy.`);
        notify.error(momoMessage || 'Thanh toán không thành công.', { title: t('paymentFailed', 'Thanh toán thất bại') });
        setIsVerifying(false);
        return;
      }

      try {
        const wasAlreadyPremium = isPremiumUser;
        const data = await paymentService.verifyPaymentResult(orderId, resultCode);

        if (data.success || data.Success) {
          setIsPremiumUser(true);
          setSuccessModalMode(wasAlreadyPremium ? 'renew' : 'upgrade');
          setShowSuccessModal(true);
          notify.success(
            wasAlreadyPremium
              ? t('renewSuccessBadge', 'Gia hạn gói Premium thành công!')
              : t('upgradeSuccessBadge', 'Nâng cấp gói Premium thành công!'),
            { title: t('paymentSuccess', 'Thành công 🎉') }
          );
          const latest = await fetchSubscriptionInfo();
          window.dispatchEvent(new CustomEvent('interview:quota-changed', {
            detail: {
              remainingInterviewQuota: latest?.remainingInterviewQuota,
              maxInterviewQuota: latest?.maxInterviewQuota,
              planName: latest?.planCode === 'PREMIUM' ? 'Premium' : 'Free',
            }
          }));
        } else {
          setError(data.message || data.Message || 'Xác minh giao dịch thất bại.');
          notify.error(data.message || data.Message || 'Xác minh giao dịch thất bại.', { title: t('paymentFailed', 'Lỗi') });
        }
      } catch (err) {
        setError(err.message || 'Lỗi khi kiểm tra kết quả thanh toán.');
        notify.error(err.message || 'Lỗi khi kiểm tra kết quả thanh toán.');
      } finally {
        setIsVerifying(false);
      }
    };

    processPaymentCallback();
  }, [t, isPremiumUser]);

  const openCheckout = (pkg) => {
    setError('');
    setUseRewardPoints(false);
    setSelectedPackage(pkg);
    fetchSubscriptionInfo();
  };

  const handleCheckout = useCallback(async () => {
    if (!selectedPackage) return;
    setError('');
    setIsCreating(true);
    setLoadingPackageId(selectedPackage.priceId);

    try {
      const response = await paymentService.createPayment(selectedPackage.priceId, useRewardPoints);
      if (response && response.payUrl) {
        window.location.href = response.payUrl;
      } else if (response?.status === 'PaidByReward') {
        const wasAlreadyPremium = isPremiumUser;
        setSelectedPackage(null);
        setSuccessModalMode(wasAlreadyPremium ? 'renew' : 'upgrade');
        setShowSuccessModal(true);
        setIsPremiumUser(true);
        const latest = await fetchSubscriptionInfo();
        window.dispatchEvent(new CustomEvent('interview:quota-changed', {
          detail: {
            remainingInterviewQuota: latest?.remainingInterviewQuota,
            maxInterviewQuota: latest?.maxInterviewQuota,
            planName: latest?.planCode === 'PREMIUM' ? 'Premium' : 'Free',
          }
        }));
        setIsCreating(false);
        setLoadingPackageId(null);
      } else {
        throw new Error('URL thanh toán MoMo không hợp lệ.');
      }
    } catch (apiError) {
      setError(apiError.message || 'Không thể tạo phiên thanh toán.');
      notify.error(apiError.message || 'Không thể tạo phiên thanh toán.', { title: t('paymentFailed', 'Lỗi thanh toán') });
      setIsCreating(false);
      setLoadingPackageId(null);
    }
  }, [selectedPackage, useRewardPoints, isPremiumUser, t]);

  const availableRewardPoints = Number(subscriptionData?.rewardPoints ?? 0);
  const checkoutOriginalAmount = Number(selectedPackage?.amount ?? 0);
  const checkoutDiscount = useRewardPoints
    ? Math.min(availableRewardPoints, checkoutOriginalAmount)
    : 0;
  const checkoutFinalAmount = Math.max(0, checkoutOriginalAmount - checkoutDiscount);

  const getUserTier = () => {
    if (!isPremiumUser || subscriptionData?.planCode === 'FREE') {
      return 0; // Free
    }
    const cycle = subscriptionData?.billingCycle;
    if (cycle === 'Yearly' || cycle === 2 || cycle === '2') {
      return 2; // Premium 1 Year
    }
    return 1; // Premium 1 Month
  };
  const userTier = getUserTier();

  return (
    <UserLayout>
      <div className={`payment-page payment-page-expand space-y-8 pb-12 ${isDarkMode ? 'payment-page--dark' : ''}`}>

        {/* Render Subscription Dashboard if not toggled to Purchase View */}
        {!showPurchaseView ? (
          <div className="max-w-4xl mx-auto space-y-6 animate-fadeIn">
            {/* Subscription Status Hero Card */}
            <div className={`relative overflow-hidden rounded-3xl border ${isPremiumUser ? 'border-amber-500/30 bg-gradient-to-br from-amber-500/10 via-amber-500/5 to-transparent' : 'border-border bg-gradient-to-br from-surface-2 to-surface-1'} p-6 md:p-8 shadow-xl`}>
              {isPremiumUser && <div className="absolute top-0 right-0 -mt-8 -mr-8 w-48 h-48 rounded-full bg-amber-500/10 blur-3xl pointer-events-none" />}

              <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-6 relative z-10">
                <div className="flex items-center gap-4">
                  <div className={`w-16 h-16 rounded-2xl ${isPremiumUser ? 'bg-gradient-to-br from-amber-400 to-amber-600 shadow-amber-500/30 text-white' : 'bg-surface-3 shadow-sm text-text-secondary'} flex items-center justify-center shadow-lg shrink-0`}>
                    {isPremiumUser ? <Crown size={36} /> : <Star size={36} />}
                  </div>
                  <div>
                    <div className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full ${isPremiumUser ? 'bg-amber-500/20 text-amber-600 dark:text-amber-400' : 'bg-surface-3 text-text-secondary'} text-xs font-bold uppercase tracking-wider mb-1`}>
                      <Sparkles size={14} /> {t('currentPlan', 'Gói Đang Sử Dụng')}
                    </div>
                    <h1 className="text-2xl md:text-3xl font-extrabold text-text-primary">
                      {isPremiumUser
                        ? (userTier === 2 ? t('premiumYearly', 'Premium 1 Năm') + ' 👑' : t('premiumMonthly', 'Premium 1 Tháng') + ' 👑')
                        : t('freePlan', 'Gói Cơ Bản (Free)')}
                    </h1>
                    <p className="text-sm text-text-secondary">
                      {isPremiumUser ? t('premiumDesc', 'Tài khoản của bạn đang có 15 lượt mỗi chu kỳ 30 ngày.') : t('freeDesc', 'Nâng cấp để nhận 15 lượt phỏng vấn mỗi chu kỳ 30 ngày.')}
                    </p>
                  </div>
                </div>

                <button
                  type="button"
                  onClick={() => setShowPurchaseView(true)}
                  className={`px-5 py-2.5 rounded-xl border ${isPremiumUser ? 'border-amber-500/40 bg-amber-500/10 hover:bg-amber-500/20 text-amber-600 dark:text-amber-400' : 'border-primary bg-primary hover:bg-primary-dark text-white'} text-sm font-bold transition-all shadow-sm shrink-0 cursor-pointer`}
                >
                  {isPremiumUser ? t('renewOrChange', 'Gia hạn / Chọn gói khác') : t('upgradePro', 'Nâng cấp Pro')}
                </button>
              </div>
            </div>

            {/* Quota & Subscription Timeline Grid */}
            <div className={`grid grid-cols-1 ${showQuotaResetCard ? 'md:grid-cols-3' : 'md:grid-cols-2'} gap-6`}>
              {/* Card 1: Remaining Quota */}
              <div className="rounded-2xl border border-border bg-surface-1 p-6 shadow-sm flex flex-col justify-between">
                <div className="flex items-center justify-between mb-4">
                  <span className="text-xs font-bold uppercase tracking-wider text-text-secondary">{t('quotaRemaining', 'Số lượt còn lại trong tháng')}</span>
                  <div className="p-2 rounded-xl bg-primary-xlight text-primary-dark">
                    <Ticket size={20} />
                  </div>
                </div>
                <div>
                  <div className="text-3xl font-extrabold text-text-primary mb-1">
                    {subscriptionData?.remainingInterviewQuota ?? profileData?.remainingInterviewQuota ?? (isPremiumUser ? 15 : 3)} <span className="text-base font-medium text-text-secondary">/ {subscriptionData?.maxInterviewQuota ?? (isPremiumUser ? 15 : 3)}</span>
                  </div>
                  <div className="w-full bg-surface-2 h-2.5 rounded-full overflow-hidden mt-3">
                    <div
                      className="bg-gradient-to-r from-amber-500 to-orange-500 h-full rounded-full transition-all duration-500"
                      style={{ width: `${Math.min(100, Math.max(0, ((subscriptionData?.remainingInterviewQuota ?? profileData?.remainingInterviewQuota ?? (isPremiumUser ? 15 : 3)) / (subscriptionData?.maxInterviewQuota ?? (isPremiumUser ? 15 : 3))) * 100))}%` }}
                    />
                  </div>
                  <p className="text-xs text-text-secondary mt-2">{isPremiumUser ? t('quotaDescPremium', '15 lượt, làm mới theo chu kỳ cố định 30 ngày') : t('quotaDescFree', '3 lượt dùng thử miễn phí')}</p>
                </div>
              </div>

              {/* Card 2: Next Quota Reset Date (Only shown when subscription duration is > 1 month) */}
              {showQuotaResetCard && (
                <div className="rounded-2xl border border-border bg-surface-1 p-6 shadow-sm flex flex-col justify-between">
                  <div className="flex items-center justify-between mb-4">
                    <span className="text-xs font-bold uppercase tracking-wider text-text-secondary">{t('quotaReset', 'Ngày sạc lại 15 lượt tiếp')}</span>
                    <div className="p-2 rounded-xl bg-amber-500/10 text-amber-500">
                      <RotateCcw size={20} />
                    </div>
                  </div>
                  <div>
                    <div className="text-2xl font-extrabold text-text-primary mb-1">
                      {isPremiumUser ? formatDate(subscriptionData?.quotaPeriodEndsAt) : t('noReset', 'Không reset')}
                    </div>
                    <p className="text-xs text-text-secondary mt-2">{t('autoResetDesc', 'Hệ thống sẽ tự động làm mới 15 lượt vào ngày này.')}</p>
                  </div>
                </div>
              )}

              {/* Card 3: Premium Expiration Date */}
              <div className="rounded-2xl border border-border bg-surface-1 p-6 shadow-sm flex flex-col justify-between">
                <div className="flex items-center justify-between mb-4">
                  <span className="text-xs font-bold uppercase tracking-wider text-text-secondary">{t('premiumExpire', 'Hạn gói đăng ký')}</span>
                  <div className="p-2 rounded-xl bg-blue-500/10 text-blue-500">
                    <Calendar size={20} />
                  </div>
                </div>
                <div>
                  <div className="text-2xl font-extrabold text-text-primary mb-1">
                    {formatDate(subscriptionData?.subscriptionExpiresAt ?? profileData?.premiumExpireAt)}
                  </div>
                  <p className="text-xs text-text-secondary mt-2">{t('expireDesc', 'Thời gian hết hạn sử dụng các đặc quyền Premium.')}</p>
                </div>
              </div>
            </div>

            {/* Premium Benefits Summary */}
            {isPremiumUser && (
              <div className="rounded-2xl border border-border bg-surface-1 p-6 shadow-sm">
                <h3 className="text-lg font-bold text-text-primary mb-4 flex items-center gap-2">
                  <ShieldCheck className="text-amber-500" size={22} /> {t('benefitsTitle', 'Đặc quyền gói Premium của bạn')}
                </h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="flex items-start gap-3 p-3.5 rounded-xl bg-surface-2/60 border border-border">
                    <Zap className="text-amber-500 shrink-0 mt-0.5" size={20} />
                    <div>
                      <p className="text-sm font-bold text-text-primary">{t('benefit1', '15 lượt phỏng vấn mỗi tháng')}</p>
                      <p className="text-xs text-text-secondary">{t('benefit1Desc', 'Luyện tập phỏng vấn AI chuyên sâu mọi chủ đề.')}</p>
                    </div>
                  </div>
                  <div className="flex items-start gap-3 p-3.5 rounded-xl bg-surface-2/60 border border-border">
                    <RotateCcw className="text-amber-500 shrink-0 mt-0.5" size={20} />
                    <div>
                      <p className="text-sm font-bold text-text-primary">{t('benefit2', 'Tự động sạc lại lượt hàng tháng')}</p>
                      <p className="text-xs text-text-secondary">{t('benefit2Desc', 'Mỗi 30 ngày số lượt sẽ được sạc lại 15 lượt.')}</p>
                    </div>
                  </div>
                  <div className="flex items-start gap-3 p-3.5 rounded-xl bg-surface-2/60 border border-border">
                    <Sparkles className="text-amber-500 shrink-0 mt-0.5" size={20} />
                    <div>
                      <p className="text-sm font-bold text-text-primary">{t('benefit3', 'Phân tích & Đánh giá chuyên sâu')}</p>
                      <p className="text-xs text-text-secondary">{t('benefit3Desc', 'Chấm điểm tiêu chuẩn STAR & gợi ý cải thiện chi tiết.')}</p>
                    </div>
                  </div>
                  <div className="flex items-start gap-3 p-3.5 rounded-xl bg-surface-2/60 border border-border">
                    <Crown className="text-amber-500 shrink-0 mt-0.5" size={20} />
                    <div>
                      <p className="text-sm font-bold text-text-primary">{t('benefit4', 'Đã kích hoạt toàn bộ tính năng')}</p>
                      <p className="text-xs text-text-secondary">{t('benefit4Desc', 'Trải nghiệm sớm nhất các mô hình AI mới.')}</p>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        ) : (
          /* Package Purchase Selection Grid */
          <>
            <button
              type="button"
              onClick={() => {
                setShowPurchaseView(false);
                window.history.replaceState({}, '', window.location.pathname);
              }}
              className="inline-flex items-center gap-2 px-4 py-2 rounded-xl border border-border bg-surface-1 hover:bg-surface-2 text-sm font-semibold text-text-primary transition-all shadow-sm mb-4 cursor-pointer"
            >
              <ArrowLeft size={18} /> {t('backToPackages', 'Quay lại Quản Lý Gói')}
            </button>

            <section className="payment-hero relative overflow-hidden rounded-2xl border border-border p-6 md:p-10 text-center">
              <div className="payment-hero__glow payment-hero__glow--a" />
              <div className="payment-hero__glow payment-hero__glow--b" />

              <div className="relative z-10 flex flex-col items-center justify-center max-w-3xl mx-auto">
                <p className="mb-4 inline-flex rounded-full border border-white/20 bg-white/15 px-4 py-1.5 text-xs font-semibold uppercase tracking-widest text-white">
                  {t('upgradeBadge', 'Nâng Cấp Trải Nghiệm AI')}
                </p>
                <h1 className="text-3xl font-extrabold leading-tight text-white md:text-4xl lg:text-5xl mb-4">
                  {t('title', 'Chọn Gói Phù Hợp Với Bạn')}
                </h1>
                <p className="text-base text-white/90 md:text-lg">
                  {t('subtitle', 'Thanh toán nhanh chóng, an toàn. Kích hoạt tính năng Premium ngay lập tức để mở khoá toàn bộ sức mạnh AI.')}
                </p>
              </div>
            </section>

            {error && (
              <div className="rounded-xl border border-error bg-error-light p-4 text-center max-w-2xl mx-auto">
                <div className="flex items-center justify-center gap-2">
                  <AlertTriangle size={24} className="text-error" />
                  <h3 className="text-base font-bold text-error">{t('paymentFailed', 'Thanh toán không thành công')}</h3>
                </div>
                <p className="mt-1 text-sm text-text-secondary">{error}</p>
              </div>
            )}

            <section className="grid grid-cols-1 md:grid-cols-3 gap-6 max-w-6xl mx-auto">
              {PACKAGES.map((pkg) => {
                const isFree = pkg.isFree;
                const isLoadingThis = isCreating && loadingPackageId === pkg.priceId;
                const pkgTier = isFree ? 0 : (pkg.billingCycle === 2 ? 2 : 1);
                const isCurrentPackage = pkgTier === userTier;
                const isLowerPackage = pkgTier < userTier;

                let yearlyDiscountPercent = null;
                if (pkg.billingCycle === 2) {
                  const monthlyPkg = PACKAGES.find((p) => p.billingCycle === 1);
                  const monthlyAmount = monthlyPkg ? monthlyPkg.amount : 59000;
                  if (monthlyAmount > 0 && pkg.amount > 0) {
                    const fullYearMonthlyCost = monthlyAmount * 12;
                    if (fullYearMonthlyCost > pkg.amount) {
                      yearlyDiscountPercent = Math.round(((fullYearMonthlyCost - pkg.amount) / fullYearMonthlyCost) * 100);
                    }
                  }
                }

                return (
                  <article
                    key={pkg.id}
                    className={`flex flex-col rounded-2xl border p-6 shadow-sm animate-pageEntrance transition-all hover:shadow-md ${
                      isCurrentPackage
                        ? 'border-2 border-amber-500 bg-amber-500/5 relative overflow-hidden ring-1 ring-amber-500/20 shadow-amber-500/10'
                        : !isFree
                        ? 'border-primary/30 relative overflow-hidden bg-surface-1'
                        : 'border-border bg-surface-1'
                    } ${isLowerPackage ? 'opacity-75' : ''}`}
                  >
                    {isCurrentPackage ? (
                      <div className="absolute top-0 right-0">
                        <div className="bg-amber-500 text-white text-[11px] font-bold uppercase tracking-wider py-1.5 px-3 rounded-bl-xl flex items-center gap-1 shadow-sm">
                          <CheckCircle2 size={14} /> {t('currentlyUsing', 'Đang sử dụng')}
                        </div>
                      </div>
                    ) : !isFree ? (
                      <div className="absolute top-0 right-0">
                        <div className="bg-primary text-white text-[10px] font-bold uppercase tracking-wider py-1 px-3 rounded-bl-lg flex items-center gap-1">
                          {yearlyDiscountPercent ? <span className="font-extrabold text-amber-300">-{yearlyDiscountPercent}%</span> : null} Premium
                        </div>
                      </div>
                    ) : null}

                    <div className="mb-6 flex items-center gap-4">
                      <div className={`rounded-xl p-3 ${isFree ? 'bg-surface-2 text-text-secondary' : isCurrentPackage ? 'bg-amber-500/15 text-amber-600 dark:text-amber-400' : 'bg-primary-xlight text-primary-dark'}`}>
                        {isFree ? <Star size={24} /> : <Crown size={24} />}
                      </div>
                      <div>
                        <h2 className="text-xl font-bold text-text-primary">{pkg.name}</h2>
                        <p className="text-sm text-text-secondary">{pkg.subtitle}</p>
                      </div>
                    </div>

                    <div className={`rounded-xl border p-4 mb-6 ${isFree ? 'border-border bg-surface-2' : isCurrentPackage ? 'border-amber-500/30 bg-amber-500/10' : 'border-primary-light bg-primary-xlight/55'}`}>
                      <div className="flex items-center justify-between">
                        <p className={`text-xs uppercase tracking-wide ${isFree ? 'text-text-secondary' : isCurrentPackage ? 'text-amber-700 dark:text-amber-300 font-semibold' : 'text-primary-dark'}`}>{t('cost', 'Chi phí')}</p>
                        {yearlyDiscountPercent && (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-black bg-rose-500/15 text-rose-600 dark:text-rose-400 border border-rose-500/30 shadow-xs">
                            -{yearlyDiscountPercent}%
                          </span>
                        )}
                      </div>
                      <p className={`mt-1 text-3xl font-extrabold ${isFree ? 'text-text-primary' : isCurrentPackage ? 'text-amber-600 dark:text-amber-400' : 'text-primary-dark'}`}>
                        {formatVnd(pkg.amount, t)}
                      </p>
                      {!isFree && (
                        <div className="flex items-center justify-between text-xs mt-1 text-primary-dark/70 opacity-80">
                          <span>{pkg.billingCycle === 1 ? t('perMonth', '/ tháng') : t('perYear', '/ năm')}</span>
                        </div>
                      )}
                    </div>

                    <ul className="space-y-3 mb-8 flex-grow">
                      {pkg.features.map((feature) => (
                        <li key={feature} className="flex items-start gap-2 text-sm text-text-secondary">
                          <CheckCircle2 size={18} className={isCurrentPackage ? 'text-amber-500 shrink-0 mt-0.5' : 'text-success shrink-0 mt-0.5'} />
                          <span>{feature}</span>
                        </li>
                      ))}
                    </ul>

                    {isCurrentPackage ? (
                      isFree ? (
                        <button
                          type="button"
                          className="w-full rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3.5 text-sm font-bold text-amber-600 dark:text-amber-400 cursor-not-allowed flex items-center justify-center gap-2"
                          disabled
                        >
                          <CheckCircle2 size={18} /> {t('currentlyUsing', 'Đang sử dụng')}
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-500 to-amber-600 hover:from-amber-600 hover:to-amber-700 text-white px-4 py-3.5 text-sm font-bold shadow-md shadow-amber-500/20 transition-all hover:shadow-lg hover:-translate-y-0.5 disabled:opacity-70 disabled:cursor-not-allowed disabled:transform-none cursor-pointer"
                          onClick={() => openCheckout(pkg)}
                          disabled={isCreating}
                        >
                          {isLoadingThis ? <RefreshCw size={18} className="animate-spin" /> : <RotateCcw size={18} />}
                          {t('renewCurrent', 'Gia hạn gói đang dùng')}
                        </button>
                      )
                    ) : isLowerPackage ? (
                      <button
                        type="button"
                        className="w-full rounded-xl border border-border bg-surface-2 px-4 py-3.5 text-sm font-semibold text-text-secondary/70 cursor-not-allowed"
                        disabled
                      >
                        {t('cannotDowngrade', 'Không thể chọn gói thấp hơn')}
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3.5 text-sm font-bold text-white transition-all hover:bg-primary-dark hover:shadow-lg hover:-translate-y-0.5 disabled:opacity-70 disabled:cursor-not-allowed disabled:transform-none cursor-pointer"
                        onClick={() => openCheckout(pkg)}
                        disabled={isCreating}
                      >
                        {isLoadingThis && <RefreshCw size={18} className="animate-spin" />}
                        {pkg.billingCycle === 2 ? t('upgradeYearly', 'Nâng cấp gói 1 Năm') : t('upgradeNow', 'Nâng cấp ngay')}
                      </button>
                    )}
                  </article>
                );
              })}
            </section>
          </>
        )}

        {/* Checkout Modal Portal */}
        {selectedPackage && createPortal(
          <div className="fixed inset-0 z-[9998] flex items-center justify-center bg-black/60 backdrop-blur-md p-4 animate-fadeIn">
            <div
              role="dialog"
              aria-modal="true"
              aria-labelledby="checkout-title"
              className="relative w-full max-w-lg rounded-3xl border border-border bg-surface-1 p-6 shadow-2xl animate-scaleUp"
            >
              <button
                type="button"
                aria-label={t('close', 'Đóng')}
                disabled={isCreating}
                onClick={() => setSelectedPackage(null)}
                className="absolute right-4 top-4 rounded-full p-2 text-text-secondary hover:bg-surface-2 disabled:opacity-50 cursor-pointer"
              >
                <X size={20} />
              </button>

              <div className="pr-10">
                <p className="text-xs font-bold uppercase tracking-wider text-primary">{t('checkoutConfirmTitle', 'Xác nhận thanh toán')}</p>
                <h2 id="checkout-title" className="mt-1 text-2xl font-extrabold text-text-primary">{selectedPackage.name}</h2>
                <p className="mt-2 text-sm text-text-secondary">
                  {t('rewardPointsInfo', 'Bạn đang có {{points}} điểm thưởng. Mỗi điểm giảm đúng 1 VND và không hết hạn.', { points: availableRewardPoints.toLocaleString('vi-VN') })}
                </p>
              </div>

              <fieldset className="mt-6 space-y-3">
                <legend className="mb-2 text-sm font-bold text-text-primary">{t('chooseRewardOption', 'Chọn cách sử dụng điểm')}</legend>
                <label className={`flex cursor-pointer items-center gap-3 rounded-2xl border p-4 transition ${!useRewardPoints ? 'border-primary bg-primary-xlight/50' : 'border-border bg-surface-2'}`}>
                  <input
                    type="radio"
                    name="reward-option"
                    checked={!useRewardPoints}
                    onChange={() => setUseRewardPoints(false)}
                    className="h-4 w-4 accent-primary"
                  />
                  <span>
                    <strong className="block text-sm text-text-primary">{t('noPoints', 'Không dùng điểm')}</strong>
                    <span className="text-xs text-text-secondary">{t('noPointsDesc', 'Thanh toán toàn bộ bằng MoMo.')}</span>
                  </span>
                </label>
                <label className={`flex items-center gap-3 rounded-2xl border p-4 transition ${availableRewardPoints > 0 ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'} ${useRewardPoints ? 'border-primary bg-primary-xlight/50' : 'border-border bg-surface-2'}`}>
                  <input
                    type="radio"
                    name="reward-option"
                    checked={useRewardPoints}
                    disabled={availableRewardPoints <= 0}
                    onChange={() => setUseRewardPoints(true)}
                    className="h-4 w-4 accent-primary"
                  />
                  <span>
                    <strong className="block text-sm text-text-primary">{t('usePoints', 'Dùng hết điểm thưởng')}</strong>
                    <span className="text-xs text-text-secondary">
                      {t('usePointsDesc', 'Áp dụng {{discount}} điểm cho đơn hàng này.', { discount: Math.min(availableRewardPoints, checkoutOriginalAmount).toLocaleString('vi-VN') })}
                    </span>
                  </span>
                </label>
              </fieldset>

              <div className="mt-6 space-y-3 rounded-2xl border border-border bg-surface-2 p-4">
                <div className="flex justify-between text-sm text-text-secondary"><span>{t('planPrice', 'Giá gói')}</span><span>{formatVnd(checkoutOriginalAmount, t)}</span></div>
                <div className="flex justify-between text-sm text-text-secondary"><span>{t('pointsDiscount', 'Giảm bằng điểm')}</span><span>- {checkoutDiscount.toLocaleString('vi-VN')} VND</span></div>
                <div className="border-t border-border pt-3 flex items-end justify-between">
                  <span className="font-bold text-text-primary">{t('finalPayAmount', 'Cần thanh toán')}</span>
                  <span className="text-2xl font-extrabold text-primary">{formatVnd(checkoutFinalAmount, t)}</span>
                </div>
              </div>

              <div className="mt-6 flex gap-3">
                <button
                  type="button"
                  disabled={isCreating}
                  onClick={() => setSelectedPackage(null)}
                  className="flex-1 rounded-xl border border-border px-4 py-3 text-sm font-bold text-text-secondary disabled:opacity-50 cursor-pointer"
                >
                  {t('cancel', 'Hủy')}
                </button>
                <button
                  type="button"
                  disabled={isCreating}
                  onClick={handleCheckout}
                  className="flex flex-[2] items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3 text-sm font-bold text-white disabled:opacity-60 cursor-pointer"
                >
                  {isCreating && <RefreshCw size={18} className="animate-spin" />}
                  {checkoutFinalAmount === 0 ? t('payWithPoints', 'Thanh toán bằng điểm') : t('continueWithMomo', 'Tiếp tục với MoMo · {{amount}}', { amount: formatVnd(checkoutFinalAmount, t) })}
                </button>
              </div>
            </div>
          </div>,
          document.body
        )}

        {/* Success Modal using Portal to cover full screen */}
        {showSuccessModal && createPortal(
          <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-md p-4 animate-fadeIn">
            <div className="relative w-full max-w-md overflow-hidden rounded-3xl bg-surface-1 border border-border p-6 shadow-2xl animate-scaleUp">
              <button
                type="button"
                onClick={() => setShowSuccessModal(false)}
                className="absolute top-4 right-4 text-text-secondary hover:text-text-primary p-2 rounded-full hover:bg-surface-2 transition-all cursor-pointer"
              >
                <X size={20} />
              </button>

              <div className="flex flex-col items-center text-center">
                <div className="w-16 h-16 rounded-full bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-500 mb-4 shadow-inner">
                  <Sparkles size={36} className="animate-pulse" />
                </div>

                <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-amber-500/15 border border-amber-500/30 text-amber-600 dark:text-amber-400 text-xs font-bold uppercase tracking-wider mb-2">
                  <Crown size={14} /> {successModalMode === 'renew' ? t('renewSuccessBadge', 'Gia Hạn Premium Thành Công') : t('upgradeSuccessBadge', 'Đăng Ký Premium Thành Công')}
                </div>

                <h2 className="text-2xl font-extrabold text-text-primary mb-2">
                  {successModalMode === 'renew' ? t('renewSuccessTitle', 'Gia hạn gói Premium thành công! 🎉') : t('upgradeSuccessTitle', 'Chúc mừng bạn đã đăng ký thành công Premium! 🎉')}
                </h2>

                <p className="text-sm text-text-secondary mb-6 leading-relaxed">
                  {successModalMode === 'renew' ? t('renewSuccessDesc', 'Chúc mừng bạn đã gia hạn thành công gói Premium! Hạn sử dụng và lượt phỏng vấn đã được tự động cập nhật.') : t('upgradeSuccessDesc', 'Chúc mừng bạn đã đăng ký thành công gói Premium! Tài khoản của bạn đã được mở khóa toàn bộ đặc quyền Premium.')}
                </p>

                <div className="flex flex-col w-full gap-2.5">
                  <button
                    type="button"
                    onClick={() => {
                      setShowSuccessModal(false);
                      setShowPurchaseView(false);
                    }}
                    className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-500 to-orange-500 px-5 py-3 text-sm font-bold text-white shadow-lg shadow-amber-500/25 transition-all hover:opacity-95 hover:scale-[1.02] cursor-pointer"
                  >
                    <CheckCircle2 size={18} /> {t('stayInPackages', 'Về trang Quản lý gói')}
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setShowSuccessModal(false);
                      navigate(USER_ROUTES.INTERVIEW_MODE);
                    }}
                    className="w-full rounded-xl border border-border bg-surface-2 px-5 py-2.5 text-sm font-semibold text-text-secondary transition-all hover:bg-surface-3 cursor-pointer"
                  >
                    {t('interviewNow', 'Phỏng Vấn Ngay')}
                  </button>
                </div>
              </div>
            </div>
          </div>,
          document.body
        )}
      </div>
    </UserLayout>
  );
}

export default PackagesPage;
