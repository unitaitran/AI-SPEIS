import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  AlertTriangle,
  BadgeCheck,
  Building2,
  CircleDashed,
  CheckCircle2,
  Clock3,
  Copy,
  Crown,
  Download,
  QrCode,
  RefreshCw,
  ScanLine,
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { USER_ROUTES } from '../../routes/routePaths';
import { navigate } from '../../routes/navigation';
import paymentService from '../../services/PaymentService';
import notify from '../../utils/notification';
import '../../styles/user/PackagesPage.css';

const PREMIUM_PACKAGE = {
  id: 1,
  name: 'Premium Package',
  amount: 29000,
  subtitle: 'Unlimited interview sessions and priority processing',
  features: [
    'Unlimited interview campaigns',
    'Priority AI processing',
    'Advanced learning analytics',
  ],
};

function formatVnd(amount) {
  return `${amount.toLocaleString('vi-VN')} VND`;
}

function parseApiDateToMs(input) {
  if (!input) return null;
  if (input instanceof Date) return input.getTime();

  const raw = String(input).trim();
  if (!raw) return null;

  // Treat backend timestamps without timezone suffix as UTC.
  const normalized = /(Z|[+-]\d{2}:\d{2})$/i.test(raw) ? raw : `${raw}Z`;
  const parsedMs = new Date(normalized).getTime();
  return Number.isFinite(parsedMs) ? parsedMs : null;
}

function getSecondsRemaining(expiresAt) {
  const expiresAtMs = parseApiDateToMs(expiresAt);
  if (!expiresAtMs) return 0;

  const remainingMs = expiresAtMs - Date.now();
  if (remainingMs <= 0) return 0;

  return Math.ceil(remainingMs / 1000);
}

function formatCountdown(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

function PackagesPage() {
  const [payment, setPayment] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState('');
  const [timeLeft, setTimeLeft] = useState(0);
  const [isSuccess, setIsSuccess] = useState(false);
  const [isDarkMode, setIsDarkMode] = useState(false);
  const expiryToastShownRef = useRef(false);

  const isPaid = payment?.status === 'Paid';
  const isExpired = payment?.status === 'Expired' || timeLeft <= 0;
  const statusMeta = useMemo(() => {
    if (isPaid) {
      return { label: 'Paid', className: 'payment-status-badge payment-status-badge--paid', icon: BadgeCheck };
    }

    if (isExpired) {
      return { label: 'Expired', className: 'payment-status-badge payment-status-badge--expired', icon: AlertTriangle };
    }

    return { label: 'Waiting', className: 'payment-status-badge payment-status-badge--waiting', icon: CircleDashed };
  }, [isExpired, isPaid]);

  const createPayment = useCallback(async () => {
    setError('');
    setIsCreating(true);

    try {
      const response = await paymentService.createPayment(PREMIUM_PACKAGE.id);
      setPayment(response);
      setTimeLeft(getSecondsRemaining(response.expiresAt));
      setIsSuccess(false);
      expiryToastShownRef.current = false;
    } catch (apiError) {
      setPayment(null);
      setError(apiError.message || 'Unable to create payment QR.');
      notify.error(apiError.message || 'Unable to create payment QR.', { title: 'Payment Error' });
    } finally {
      setIsLoading(false);
      setIsCreating(false);
    }
  }, []);

  useEffect(() => {
    createPayment();
  }, [createPayment]);

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const applyTheme = () => setIsDarkMode(media.matches);
    applyTheme();
    media.addEventListener('change', applyTheme);

    return () => {
      media.removeEventListener('change', applyTheme);
    };
  }, []);

  useEffect(() => {
    if (!payment?.expiresAt || isPaid) return undefined;

    const countdown = setInterval(() => {
      const next = getSecondsRemaining(payment.expiresAt);
      setTimeLeft(next);

      if (next <= 0) {
        setPayment((current) => (current ? { ...current, status: 'Expired' } : current));
      }
    }, 1000);

    return () => clearInterval(countdown);
  }, [payment?.expiresAt, isPaid]);

  useEffect(() => {
    if (!payment?.orderCode || isExpired || isPaid) return undefined;

    const poller = setInterval(async () => {
      try {
        const status = await paymentService.checkPayment(payment.orderCode);
        setPayment((current) => (current ? { ...current, ...status } : current));

        if (status.status === 'Paid') {
          setIsSuccess(true);
          notify.success('Payment confirmed. Redirecting to dashboard...', { title: 'Payment Success' });
          window.setTimeout(() => {
            navigate(USER_ROUTES.DASHBOARD, { replace: true });
          }, 3000);
        }
      } catch (apiError) {
        // Ignore transient polling errors to avoid noisy UX while QR is active.
      }
    }, 3000);

    return () => clearInterval(poller);
  }, [payment?.orderCode, isExpired, isPaid]);

  useEffect(() => {
    if (!isExpired || expiryToastShownRef.current || isPaid) return;

    expiryToastShownRef.current = true;
    notify.warning('Payment QR has expired. Please generate a new QR code.', { title: 'Payment Expired' });
  }, [isExpired, isPaid]);

  const paymentContent = useMemo(() => payment?.orderCode || '', [payment?.orderCode]);

  const copyToClipboard = async (value, successMessage) => {
    if (!value) return;

    try {
      await navigator.clipboard.writeText(value);
      notify.success(successMessage, { title: 'Copied' });
    } catch {
      notify.error('Unable to copy to clipboard.', { title: 'Copy Failed' });
    }
  };

  const handleDownloadQr = async () => {
    if (!payment?.qrUrl || isExpired) return;

    try {
      const response = await fetch(payment.qrUrl);
      const blob = await response.blob();
      const objectUrl = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = objectUrl;
      anchor.download = `AI-SPEIS-${payment.orderCode}.png`;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(objectUrl);
      notify.success('QR image downloaded.', { title: 'Download Complete' });
    } catch {
      notify.error('Unable to download QR image.', { title: 'Download Failed' });
    }
  };

  return (
    <UserLayout>
      <div className={`payment-page payment-page-expand space-y-4 pb-8 ${isDarkMode ? 'payment-page--dark' : ''}`}>
        <section className="payment-hero relative overflow-hidden rounded-2xl border border-border p-4 md:p-6">
          <div className="payment-hero__glow payment-hero__glow--a" />
          <div className="payment-hero__glow payment-hero__glow--b" />

          <div className="relative z-10 grid gap-4 md:grid-cols-12 md:items-center">
            <div className="md:col-span-8">
              <p className="mb-2 inline-flex rounded-full border border-white/20 bg-white/15 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-white">
                Secure VietQR Payment
              </p>
              <h1 className="text-xl font-bold leading-tight text-white md:text-2xl lg:text-3xl">
                Upgrade To Premium In One Scan
              </h1>
              <p className="mt-2 max-w-2xl text-sm text-white/90 md:text-base">
                Scan the QR with your banking app, keep the payment content unchanged, and we will activate your Premium benefits instantly.
              </p>
            </div>

            <div className="md:col-span-4">
              <div className="mx-auto flex h-24 w-24 items-center justify-center rounded-3xl border border-white/20 bg-white/10 backdrop-blur-md md:ml-auto md:h-28 md:w-28">
                <Crown size={40} className="text-yellow-200" />
              </div>
            </div>
          </div>
        </section>

        <section className="grid grid-cols-1 gap-4 xl:grid-cols-12">
          <article className="rounded-2xl border border-border bg-surface-2 p-5 shadow-sm xl:col-span-4 animate-pageEntrance">
            <div className="mb-3 flex items-center gap-3">
              <div className="rounded-xl bg-primary-xlight p-2 text-primary-dark">
                <Crown size={20} />
              </div>
              <div>
                <h2 className="text-lg font-bold text-text-primary">{PREMIUM_PACKAGE.name}</h2>
                <p className="text-sm text-text-secondary">{PREMIUM_PACKAGE.subtitle}</p>
              </div>
            </div>

            <div className="rounded-xl border border-primary-light bg-primary-xlight/55 p-4">
              <p className="text-xs uppercase tracking-wide text-primary-dark">Amount</p>
              <p className="mt-1 text-2xl font-bold text-primary-dark">
                {formatVnd(payment?.amount ?? PREMIUM_PACKAGE.amount)}
              </p>
            </div>

            <ul className="mt-4 space-y-2.5">
              {PREMIUM_PACKAGE.features.map((feature) => (
                <li key={feature} className="flex items-center gap-2 text-sm text-text-secondary">
                  <CheckCircle2 size={16} className="text-success" />
                  <span>{feature}</span>
                </li>
              ))}
            </ul>

            <div className="mt-4 space-y-3 rounded-xl border border-border bg-surface-1 p-4 text-sm shadow-sm">
              <div className="flex items-center justify-between gap-3">
                <span className="text-text-secondary">Bank</span>
                <span className="inline-flex items-center gap-1 font-semibold text-text-primary">
                  <Building2 size={16} className="text-primary-dark" />
                  BIDV
                </span>
              </div>

              <div className="flex items-center justify-between gap-3">
                <span className="text-text-secondary">Account</span>
                <span className="font-mono font-semibold tracking-wide text-text-primary">4270767262</span>
              </div>

              <div className="flex items-center justify-between gap-3">
                <span className="text-text-secondary">Payment Content</span>
                <span className="font-mono text-xs font-semibold text-text-primary">{payment?.orderCode || '---'}</span>
              </div>
            </div>

            <div className="mt-4 rounded-xl border border-border bg-surface-1 p-4">
              <h4 className="text-sm font-bold text-text-primary">3-Step Payment Guide</h4>
              <ol className="mt-2 space-y-2 text-sm text-text-secondary">
                <li className="flex items-start gap-2">
                  <span className="payment-step-dot">1</span>
                  <span>Scan the QR</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="payment-step-dot">2</span>
                  <span>Transfer the exact amount</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="payment-step-dot">3</span>
                  <span>Wait for automatic confirmation</span>
                </li>
              </ol>
            </div>
          </article>

          <article className="rounded-2xl border border-border bg-surface-2 p-5 shadow-sm xl:col-span-5 animate-pageEntrance">
            {isLoading ? (
              <div className="payment-skeleton">
                <div className="payment-skeleton__qr" />
                <div className="payment-skeleton__line" />
                <div className="payment-skeleton__line payment-skeleton__line--short" />
              </div>
            ) : null}

            {!isLoading && error ? (
              <div className="rounded-xl border border-error bg-error-light p-6 text-center">
                <AlertTriangle size={34} className="mx-auto text-error" />
                <h3 className="mt-3 text-lg font-bold text-error">Unable to Create Payment</h3>
                <p className="mt-2 text-sm text-text-secondary">{error}</p>
                <button
                  type="button"
                  className="mt-4 inline-flex items-center gap-2 rounded-xl border border-error px-4 py-2 text-sm font-semibold text-error transition hover:bg-error hover:text-white"
                  onClick={createPayment}
                  disabled={isCreating}
                >
                  <RefreshCw size={16} className={isCreating ? 'animate-spin' : ''} />
                  Retry
                </button>
              </div>
            ) : null}

            {!isLoading && !error && !payment ? (
              <div className="rounded-xl border border-border bg-surface-1 p-6 text-center">
                <QrCode size={40} className="mx-auto text-primary-dark" />
                <h3 className="mt-3 text-lg font-bold text-text-primary">No Active Payment</h3>
                <p className="mt-2 text-sm text-text-secondary">Generate a new QR code to continue your Premium purchase.</p>
                <button
                  type="button"
                  className="mt-4 inline-flex items-center gap-2 rounded-xl bg-primary-dark px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary"
                  onClick={createPayment}
                  disabled={isCreating}
                >
                  <RefreshCw size={16} className={isCreating ? 'animate-spin' : ''} />
                  Generate QR
                </button>
              </div>
            ) : null}

            {!isLoading && !error && payment ? (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <h3 className="text-lg font-bold text-text-primary">Scan To Pay</h3>
                  <span className={statusMeta.className}>
                    <statusMeta.icon size={14} />
                    {statusMeta.label}
                  </span>
                </div>

                <div className="payment-countdown-card">
                  <p className="payment-countdown-label">Payment Countdown</p>
                  <div className="payment-countdown-value">
                    <Clock3 size={22} />
                    <span>{isExpired ? '00:00' : formatCountdown(timeLeft)}</span>
                  </div>
                  <p className="payment-countdown-note">QR is valid for 10 minutes</p>
                </div>

                <div className={`payment-qr-wrapper ${isExpired ? 'payment-qr-wrapper--expired' : ''}`}>
                  <img src={payment.qrUrl} alt="VietQR payment code" className="payment-qr-image" />
                  {isExpired ? (
                    <div className="payment-qr-overlay">
                      <AlertTriangle size={28} className="text-warning" />
                      <p className="mt-2 font-semibold text-warning">Payment Expired</p>
                      <p className="text-xs text-text-secondary">Please generate a new QR code.</p>
                    </div>
                  ) : null}
                </div>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <button
                    type="button"
                    className="payment-action-btn"
                    onClick={() => copyToClipboard('4270767262', 'Account number copied.')}
                  >
                    <Copy size={16} />
                    Copy Account
                  </button>

                  <button
                    type="button"
                    className="payment-action-btn"
                    onClick={() => copyToClipboard(paymentContent, 'Payment content copied.')}
                    disabled={!paymentContent}
                  >
                    <Copy size={16} />
                    Copy Payment Content
                  </button>

                  <button
                    type="button"
                    className="payment-action-btn"
                    onClick={handleDownloadQr}
                    disabled={isExpired}
                  >
                    <Download size={16} />
                    Download QR
                  </button>

                  <button
                    type="button"
                    className={`payment-action-btn payment-action-btn--primary ${isExpired ? 'payment-action-btn--urgent' : ''}`}
                    onClick={createPayment}
                    disabled={isCreating}
                  >
                    <RefreshCw size={16} className={isCreating ? 'animate-spin' : ''} />
                    Generate New QR
                  </button>
                </div>

                <div className="rounded-xl border border-border bg-surface-1 p-4 text-sm">
                  <div className="flex items-center justify-between gap-3">
                    <span className="text-text-secondary">Amount</span>
                    <span className="font-semibold text-text-primary">{formatVnd(payment.amount)}</span>
                  </div>
                  <div className="mt-2 flex items-center justify-between gap-3">
                    <span className="text-text-secondary">OrderCode</span>
                    <span className="font-mono text-xs font-semibold text-text-primary">{payment.orderCode}</span>
                  </div>
                </div>
              </div>
            ) : null}
          </article>

          <aside className="rounded-2xl border border-border bg-surface-2 p-5 shadow-sm xl:col-span-3 animate-pageEntrance">
            <div className="payment-mascot-wrap">
              <div className="payment-mascot-glow" />
              <img
                src="/mascot_AI-SPEIS-removebg.png"
                alt="AI-SPEIS mascot"
                className="payment-mascot-image"
              />
            </div>
            <div className="mt-3 rounded-xl border border-primary-light bg-primary-xlight/65 p-3 text-center">
              <p className="text-sm font-bold text-primary-dark">Your Premium AI Assistant is waiting for you!</p>
              <p className="mt-1 text-xs text-text-secondary">Scan the QR to unlock all AI features.</p>
            </div>
            <div className="mt-4 rounded-xl border border-border bg-surface-1 p-3 text-xs text-text-secondary">
              <div className="inline-flex items-center gap-2 font-semibold text-text-primary">
                <ScanLine size={14} className="text-primary-dark" />
                Auto-detection enabled
              </div>
              <p className="mt-1">The system checks payment status every 3 seconds and confirms automatically.</p>
            </div>
          </aside>
        </section>

        {isSuccess ? (
          <section className="payment-success-card">
            <div className="payment-success-badge">
              <CheckCircle2 size={44} className="text-success" />
            </div>
            <h3 className="mt-4 text-xl font-bold text-text-primary">Payment Successful</h3>
            <p className="mt-2 text-sm text-text-secondary">
              Your Premium package is now active. Redirecting to dashboard in 3 seconds.
            </p>
          </section>
        ) : null}
      </div>
    </UserLayout>
  );
}

export default PackagesPage;