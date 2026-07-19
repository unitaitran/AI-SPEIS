import React, { useCallback, useEffect, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Crown,
  RefreshCw,
  Star
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import paymentService from '../../services/PaymentService';
import notify from '../../utils/notification';
import '../../styles/user/PackagesPage.css';

const PACKAGES = [
  {
    id: 0,
    name: 'Gói Cơ Bản (Free)',
    amount: 0,
    subtitle: 'Bắt đầu hành trình của bạn',
    features: [
      'Trải nghiệm phỏng vấn AI cơ bản',
      'Đánh giá kỹ năng tổng quan',
      'Giới hạn 5 câu hỏi mỗi phiên',
    ],
  },
  {
    id: 1,
    name: 'Premium 1 Tháng',
    amount: 59000,
    subtitle: 'Lựa chọn phổ biến',
    features: [
      '15 lượt phỏng vấn AI toàn diện',
      'Phân tích & Đánh giá chuyên sâu',
      'Tự động xoá lượt sau 1 tháng',
    ],
  },
  {
    id: 2,
    name: 'Premium 1 Năm',
    amount: 599000,
    subtitle: 'Tiết kiệm nhất',
    features: [
      'Lượt phỏng vấn không giới hạn',
      'Làm mới 15 lượt ưu tiên mỗi tháng',
      'Báo cáo kỹ năng nâng cao',
    ],
  }
];

function formatVnd(amount) {
  if (amount === 0) return 'Miễn phí';
  return `${amount.toLocaleString('vi-VN')} VND`;
}

function PackagesPage() {
  const [isCreating, setIsCreating] = useState(false);
  const [loadingPackageId, setLoadingPackageId] = useState(null);
  const [error, setError] = useState('');
  const [isDarkMode, setIsDarkMode] = useState(false);

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const applyTheme = () => setIsDarkMode(media.matches);
    applyTheme();
    media.addEventListener('change', applyTheme);

    return () => {
      media.removeEventListener('change', applyTheme);
    };
  }, []);

  const handleMomoPayment = useCallback(async (packageId) => {
    setError('');
    setIsCreating(true);
    setLoadingPackageId(packageId);

    try {
      const response = await paymentService.createPayment(packageId);
      if (response && response.payUrl) {
        window.location.href = response.payUrl;
      } else {
        throw new Error('URL thanh toán MoMo không hợp lệ.');
      }
    } catch (apiError) {
      setError(apiError.message || 'Không thể tạo phiên thanh toán.');
      notify.error(apiError.message || 'Không thể tạo phiên thanh toán.', { title: 'Lỗi thanh toán' });
      setIsCreating(false);
      setLoadingPackageId(null);
    }
  }, []);

  return (
    <UserLayout>
      <div className={`payment-page payment-page-expand space-y-8 pb-12 ${isDarkMode ? 'payment-page--dark' : ''}`}>
        <section className="payment-hero relative overflow-hidden rounded-2xl border border-border p-6 md:p-10 text-center">
          <div className="payment-hero__glow payment-hero__glow--a" />
          <div className="payment-hero__glow payment-hero__glow--b" />

          <div className="relative z-10 flex flex-col items-center justify-center max-w-3xl mx-auto">
            <p className="mb-4 inline-flex rounded-full border border-white/20 bg-white/15 px-4 py-1.5 text-xs font-semibold uppercase tracking-widest text-white">
              Nâng Cấp Trải Nghiệm AI
            </p>
            <h1 className="text-3xl font-extrabold leading-tight text-white md:text-4xl lg:text-5xl mb-4">
              Chọn Gói Phù Hợp Với Bạn
            </h1>
            <p className="text-base text-white/90 md:text-lg">
              Thanh toán nhanh chóng, an toàn. Kích hoạt tính năng Premium ngay lập tức để mở khoá toàn bộ sức mạnh AI.
            </p>
          </div>
        </section>

        {error && (
          <div className="rounded-xl border border-error bg-error-light p-4 text-center max-w-2xl mx-auto">
            <div className="flex items-center justify-center gap-2">
              <AlertTriangle size={24} className="text-error" />
              <h3 className="text-base font-bold text-error">Thanh toán không thành công</h3>
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
                className={`flex flex-col rounded-2xl border bg-surface-1 p-6 shadow-sm animate-pageEntrance transition-all hover:shadow-md ${
                  !isFree ? 'border-primary/30 relative overflow-hidden' : 'border-border'
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
                  <p className={`text-xs uppercase tracking-wide ${isFree ? 'text-text-secondary' : 'text-primary-dark'}`}>Chi phí</p>
                  <p className={`mt-1 text-3xl font-extrabold ${isFree ? 'text-text-primary' : 'text-primary-dark'}`}>
                    {formatVnd(pkg.amount)}
                  </p>
                  {!isFree && <p className="text-xs mt-1 text-primary-dark/70 opacity-80">{pkg.id === 1 ? '/ tháng' : '/ năm'}</p>}
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
                    Đang sử dụng
                  </button>
                ) : (
                  <button
                    type="button"
                    className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3.5 text-sm font-bold text-white transition-all hover:bg-primary-dark hover:shadow-lg hover:-translate-y-0.5 disabled:opacity-70 disabled:cursor-not-allowed disabled:transform-none"
                    onClick={() => handleMomoPayment(pkg.id)}
                    disabled={isCreating}
                  >
                    {isLoadingThis && <RefreshCw size={18} className="animate-spin" />}
                    Nâng cấp
                  </button>
                )}
              </article>
            );
          })}
        </section>
      </div>
    </UserLayout>
  );
}

export default PackagesPage;