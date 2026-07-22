import React, { useCallback, useEffect, useState } from 'react';
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

function formatVnd(amount, locale, t) {
  if (amount === 0) return t('freePrice', 'Miễn phí');
  return `${amount.toLocaleString(locale)} VND`;
}

function formatDate(dateStr, locale) {
  if (!dateStr) return '—';
  const d = new Date(dateStr);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleDateString(locale, { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function getNextResetDate(lastResetStr, expireStr, locale) {
  let baseDate = lastResetStr ? new Date(lastResetStr) : new Date();
  if (isNaN(baseDate.getTime())) baseDate = new Date();

  const nextReset = new Date(baseDate);
  nextReset.setDate(nextReset.getDate() + 30);

  if (expireStr) {
    const expireDate = new Date(expireStr);
    if (!isNaN(expireDate.getTime()) && nextReset > expireDate) {
      return formatDate(expireDate, locale);
    }
  }

  return formatDate(nextReset, locale);
}

function PackagesPage() {
  const { t, i18n } = useTranslation('packages');
  const locale = i18n.language.startsWith('vi') ? 'vi-VN' : 'en-US';
  const [isCreating, setIsCreating] = useState(false);
  const [loadingPackageId, setLoadingPackageId] = useState(null);
  const [error, setError] = useState('');
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);

  const PACKAGES = [
    {
      id: 0,
      name: t('freePlanName', 'Gói Cơ Bản (Free)'),
      amount: 0,
      subtitle: t('freeSubtitle', 'Bắt đầu hành trình của bạn'),
      features: [
        t('freeFeature1', 'Trải nghiệm phỏng vấn AI cơ bản'),
        t('freeFeature2', 'Đánh giá kỹ năng tổng quan'),
        t('freeFeature3', 'Giới hạn 5 câu hỏi mỗi phiên'),
      ],
    },
    {
      id: 1,
      name: t('premiumMonthly', 'Premium 1 Tháng'),
      amount: 59000,
      subtitle: t('premiumMonthlySubtitle', 'Lựa chọn phổ biến'),
      features: [
        t('premiumMonthlyFeature1', '15 lượt phỏng vấn AI toàn diện'),
        t('benefit3', 'Phân tích & Đánh giá chuyên sâu'),
        t('premiumMonthlyFeature3', 'Tự động xoá lượt sau 1 tháng'),
      ],
    },
    {
      id: 2,
      name: t('premiumYearly', 'Premium 1 Năm'),
      amount: 599000,
      subtitle: t('premiumYearlySubtitle', 'Tiết kiệm nhất'),
      features: [
        t('premiumYearlyFeature1', 'Lượt phỏng vấn không giới hạn'),
        t('premiumYearlyFeature2', 'Làm mới 15 lượt ưu tiên mỗi tháng'),
        t('premiumYearlyFeature3', 'Báo cáo kỹ năng nâng cao'),
      ],
    }
  ];
  const [isVerifying, setIsVerifying] = useState(false);

  const [profileData, setProfileData] = useState(null);
  const [isPremiumUser, setIsPremiumUser] = useState(false);
  const [showPurchaseView, setShowPurchaseView] = useState(() => {
    return new URLSearchParams(window.location.search).get('purchase') === 'true';
  });

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
  }, [t]);

  const fetchSubscriptionInfo = async () => {
    try {
      const token = localStorage.getItem('token');
      if (!token) return;

      const [profileRes, quotaRes] = await Promise.all([
        fetch(`${API_BASE_URL}/api/users/me`, { headers: { Authorization: `Bearer ${token}` } }),
        fetch(`${API_BASE_URL}/api/InterviewSession/quota`, { headers: { Authorization: `Bearer ${token}` } })
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
    } catch {
      // Ignore errors
    }
  };

  useEffect(() => {
    fetchSubscriptionInfo();
  }, [t]);

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
        setError(momoMessage || t('paymentErrorCancelled', 'Thanh toán không thành công hoặc đã bị hủy.'));
        notify.error(momoMessage || t('paymentErrorMessage', 'Thanh toán không thành công.'), { title: t('paymentFailed', 'Thanh toán thất bại') });
        setIsVerifying(false);
        return;
      }

      try {
        const data = await paymentService.verifyPaymentResult(orderId, resultCode);

        if (data.success || data.Success) {
          setIsPremiumUser(true);
          setShowSuccessModal(true);
          notify.success(t('upgradeSuccessMessage', 'Nâng cấp gói Premium thành công! Hóa đơn đã được gửi qua email.'), { title: t('successTitle', 'Thành công 🎉') });
          // Dispatch window event so UserTopbar updates state dynamically to Premium & 15 quota
          window.dispatchEvent(new CustomEvent('interview:quota-changed', {
            detail: { remainingInterviewQuota: 15, maxInterviewQuota: 15, planName: 'Premium' }
          }));
        } else {
          setError(data.message || data.Message || t('verifyFailed', 'Xác minh giao dịch thất bại.'));
          notify.error(data.message || data.Message || t('verifyFailed', 'Xác minh giao dịch thất bại.'), { title: t('errorTitle', 'Lỗi') });
        }
      } catch (err) {
        setError(err.message || t('verifyResultFailed', 'Lỗi khi kiểm tra kết quả thanh toán.'));
        notify.error(err.message || t('verifyResultFailed', 'Lỗi khi kiểm tra kết quả thanh toán.'));
      } finally {
        setIsVerifying(false);
      }
    };

    processPaymentCallback();
  }, [t]);

  const handleMomoPayment = useCallback(async (packageId) => {
    setError('');
    setIsCreating(true);
    setLoadingPackageId(packageId);

    try {
      const response = await paymentService.createPayment(packageId);
      if (response && response.payUrl) {
        window.location.href = response.payUrl;
      } else {
        throw new Error(t('invalidPayUrl', 'URL thanh toán MoMo không hợp lệ.'));
      }
    } catch (apiError) {
      setError(apiError.message || t('createPaymentFailed', 'Không thể tạo phiên thanh toán.'));
      notify.error(apiError.message || t('createPaymentFailed', 'Không thể tạo phiên thanh toán.'), { title: t('paymentErrorTitle', 'Lỗi thanh toán') });
      setIsCreating(false);
      setLoadingPackageId(null);
    }
  }, [t]);

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
                      {isPremiumUser ? t('premiumPlan', 'Gói Premium AI-SPEIS 👑') : t('freePlan', 'Gói Cơ Bản (Free)')}
                    </h1>
                    <p className="text-sm text-text-secondary">
                      {isPremiumUser ? t('premiumDesc', 'Tài khoản của bạn đã được nâng cấp đầy đủ quyền lợi cao cấp nhất.') : t('freeDesc', 'Khám phá thêm các đặc quyền không giới hạn khi nâng cấp tài khoản.')}
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
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
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
                    {profileData?.remainingInterviewQuota ?? (isPremiumUser ? 15 : 5)} <span className="text-base font-medium text-text-secondary">{t('quotaUnit', { max: isPremiumUser ? 15 : 5 })}</span>
                  </div>
                  <div className="w-full bg-surface-2 h-2.5 rounded-full overflow-hidden mt-3">
                    <div
                      className="bg-gradient-to-r from-amber-500 to-orange-500 h-full rounded-full transition-all duration-500"
                      style={{ width: `${Math.min(100, Math.max(0, ((profileData?.remainingInterviewQuota ?? (isPremiumUser ? 15 : 5)) / (isPremiumUser ? 15 : 5)) * 100))}%` }}
                    />
                  </div>
                  <p className="text-xs text-text-secondary mt-2">{isPremiumUser ? t('unlimitedQuota', 'Đã mở khoá phỏng vấn AI không giới hạn') : t('limitedQuota', 'Giới hạn số lượng câu hỏi trong mỗi phiên')}</p>
                </div>
              </div>

              {/* Card 2: Next Quota Reset Date */}
              <div className="rounded-2xl border border-border bg-surface-1 p-6 shadow-sm flex flex-col justify-between">
                <div className="flex items-center justify-between mb-4">
                  <span className="text-xs font-bold uppercase tracking-wider text-text-secondary">{t('quotaReset', 'Ngày sạc lại 15 lượt tiếp')}</span>
                  <div className="p-2 rounded-xl bg-amber-500/10 text-amber-500">
                    <RotateCcw size={20} />
                  </div>
                </div>
                <div>
                  <div className="text-2xl font-extrabold text-text-primary mb-1">
                    {getNextResetDate(profileData?.lastQuotaResetAt, profileData?.premiumExpireAt, locale)}
                  </div>
                  <p className="text-xs text-text-secondary mt-2">{t('autoResetDesc', 'Hệ thống sẽ tự động làm mới 15 lượt vào ngày này.')}</p>
                </div>
              </div>

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
                    {formatDate(profileData?.premiumExpireAt, locale)}
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
            {isVerifying && (
              <div className="rounded-xl border border-primary-light bg-primary-xlight p-4 text-center max-w-2xl mx-auto">
                <p className="text-sm font-semibold text-primary-dark">{t('paymentProcessing')}</p>
                <p className="text-xs text-text-secondary mt-1">{t('paymentProcessingDesc')}</p>
              </div>
            )}

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
                  <h3 className="text-base font-bold text-error">{t('paymentError')}</h3>
                </div>
                <p className="mt-1 text-sm text-text-secondary">{error}</p>
              </div>
            )}

            <section className="grid grid-cols-1 md:grid-cols-3 gap-6 max-w-6xl mx-auto">
              {PACKAGES.map((pkg) => {
                const isFree = pkg.id === 0;
                const isLoadingThis = isCreating && loadingPackageId === pkg.id;

                return (
                  <article
                    key={pkg.id}
                    className={`flex flex-col rounded-2xl border bg-surface-1 p-6 shadow-sm animate-pageEntrance transition-all hover:shadow-md ${!isFree ? 'border-primary/30 relative overflow-hidden' : 'border-border'
                      }`}
                  >
                    {!isFree && (
                      <div className="absolute top-0 right-0">
                        <div className="bg-primary text-white text-[10px] font-bold uppercase tracking-wider py-1 px-3 rounded-bl-lg">
                          Premium
                        </div>
                      </div>
                    )}

                    <div className="mb-6 flex items-center gap-4">
                      <div className={`rounded-xl p-3 ${isFree ? 'bg-surface-2 text-text-secondary' : 'bg-primary-xlight text-primary-dark'}`}>
                        {isFree ? <Star size={24} /> : <Crown size={24} />}
                      </div>
                      <div>
                        <h2 className="text-xl font-bold text-text-primary">{pkg.name}</h2>
                        <p className="text-sm text-text-secondary">{pkg.subtitle}</p>
                      </div>
                    </div>

                    <div className={`rounded-xl border p-4 mb-6 ${isFree ? 'border-border bg-surface-2' : 'border-primary-light bg-primary-xlight/55'}`}>
                      <p className={`text-xs uppercase tracking-wide ${isFree ? 'text-text-secondary' : 'text-primary-dark'}`}>{t('cost', 'Chi phí')}</p>
                      <p className={`mt-1 text-3xl font-extrabold ${isFree ? 'text-text-primary' : 'text-primary-dark'}`}>
                        {formatVnd(pkg.amount, locale, t)}
                      </p>
                      {!isFree && <p className="text-xs mt-1 text-primary-dark/70 opacity-80">{pkg.id === 1 ? t('perMonth', '/ tháng') : t('perYear', '/ năm')}</p>}
                    </div>

                    <ul className="space-y-3 mb-8 flex-grow">
                      {pkg.features.map((feature) => (
                        <li key={feature} className="flex items-start gap-2 text-sm text-text-secondary">
                          <CheckCircle2 size={18} className="text-success shrink-0 mt-0.5" />
                          <span>{feature}</span>
                        </li>
                      ))}
                    </ul>

                    {isFree ? (
                      <button
                        type="button"
                        className="w-full rounded-xl border border-border bg-surface-2 px-4 py-3.5 text-sm font-semibold text-text-secondary cursor-not-allowed"
                        disabled
                      >
                        {isPremiumUser ? t('usedBefore', 'Đã từng sử dụng') : t('used', 'Đang sử dụng')}
                      </button>
                    ) : pkg.id === 1 && isPremiumUser ? (
                      <button
                        type="button"
                        className="w-full rounded-xl border border-border bg-surface-2 px-4 py-3.5 text-sm font-semibold text-text-secondary cursor-not-allowed"
                        disabled
                      >
                        {t('used', 'Đang sử dụng')}
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3.5 text-sm font-bold text-white transition-all hover:bg-primary-dark hover:shadow-lg hover:-translate-y-0.5 disabled:opacity-70 disabled:cursor-not-allowed disabled:transform-none cursor-pointer"
                        onClick={() => handleMomoPayment(pkg.id)}
                        disabled={isCreating || isVerifying}
                      >
                        {isLoadingThis && <RefreshCw size={18} className="animate-spin" />}
                        {t('renewUpgrade', 'Gia hạn / Nâng cấp')}
                      </button>
                    )}
                  </article>
                );
              })}
            </section>
          </>
        )}

        {/* Success Modal using Portal to cover full screen including Sidebar & Topbar */}
        {showSuccessModal && createPortal(
          <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-md p-4 animate-fadeIn">
            <div className="relative w-full max-w-md overflow-hidden rounded-2xl bg-surface-1 border border-border p-6 shadow-2xl animate-scaleUp">
              <button
                type="button"
                onClick={() => setShowSuccessModal(false)}
                className="absolute top-4 right-4 text-text-secondary hover:text-text-primary p-1 rounded-full hover:bg-surface-2 transition-all"
              >
                <X size={20} />
              </button>

              <div className="flex flex-col items-center text-center">
                <div className="w-16 h-16 rounded-full bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-500 mb-4 shadow-inner">
                  <Sparkles size={36} className="animate-pulse" />
                </div>

                <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-amber-500/15 border border-amber-500/30 text-amber-600 dark:text-amber-400 text-xs font-bold uppercase tracking-wider mb-2">
                  <Crown size={14} /> {t('premiumActivated', 'Gói Premium Đã Kích Hoạt')}
                </div>

                <h2 className="text-2xl font-extrabold text-text-primary mb-2">
                  {t('successTitle', 'Nâng Cấp Thành Công! 🎉')}
                </h2>

                <p className="text-sm text-text-secondary mb-6 leading-relaxed">
                  {t('successDescriptionPrefix', 'Cảm ơn bạn đã nâng cấp dịch vụ! Tài khoản của bạn đã được kích hoạt tính năng')} <strong className="text-text-primary">{t('premiumLabel', 'Premium')}</strong>. {t('successDescriptionSuffix', 'Email xác nhận và hóa đơn chi tiết đã được gửi tới hộp thư của bạn.')}
                </p>

                <div className="flex flex-col w-full gap-2.5">
                  <button
                    type="button"
                    onClick={() => {
                      setShowSuccessModal(false);
                      navigate(USER_ROUTES.INTERVIEW_MODE);
                    }}
                    className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-500 to-orange-500 px-5 py-3 text-sm font-bold text-white shadow-lg shadow-amber-500/25 transition-all hover:opacity-95 hover:scale-[1.02]"
                  >
                    <Crown size={18} /> {t('interviewNow', 'Phỏng Vấn Ngay')}
                  </button>

                  <button
                    type="button"
                    onClick={() => setShowSuccessModal(false)}
                    className="w-full rounded-xl border border-border bg-surface-2 px-5 py-2.5 text-sm font-semibold text-text-secondary transition-all hover:bg-surface-3"
                  >
                    {t('close', 'Đóng')}
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