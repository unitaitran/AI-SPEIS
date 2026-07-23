import React, { useState, useEffect, useCallback } from 'react';
import {
  Activity,
  CircleDollarSign,
  FileQuestion,
  Users,
  AlertCircle,
  Crown,
  RefreshCw,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { userService } from '../../../services/UserService';

// ── Simple SVG sparkline ────────────────────────────────────────────────────
const Sparkline = ({ data = [], color = '#6FB6E8', height = 56 }) => {
  const values = (data || []).map(d => Number(d.value));
  const allZero = values.every(v => v === 0);

  if (!values.length || allZero) {
    return (
      <svg viewBox="0 0 200 56" className="w-full opacity-25">
        <line x1="0" y1="28" x2="200" y2="28" stroke={color} strokeWidth="1.5" strokeDasharray="4 4" />
      </svg>
    );
  }

  const max = Math.max(...values) || 1;
  const min = Math.min(...values);
  const range = max - min || 1;
  const W = 200;
  const pad = 8;
  const pts = values.map((v, i) => {
    const x = (i / (values.length - 1)) * W;
    const y = height - pad - ((v - min) / range) * (height - 2 * pad);
    return `${x},${y}`;
  });

  const firstX = 0;
  const lastX = W;
  const bottomY = height - 1;
  const area = `${firstX},${bottomY} ${pts.join(' ')} ${lastX},${bottomY}`;
  const gradId = `sg-${color.replace(/[^a-z0-9]/gi, '')}`;

  return (
    <svg viewBox={`0 0 ${W} ${height}`} className="w-full" preserveAspectRatio="none">
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.22" />
          <stop offset="100%" stopColor={color} stopOpacity="0.02" />
        </linearGradient>
      </defs>
      <polygon points={area} fill={`url(#${gradId})`} />
      <polyline
        points={pts.join(' ')}
        fill="none"
        stroke={color}
        strokeWidth="2"
        strokeLinejoin="round"
        strokeLinecap="round"
      />
    </svg>
  );
};

// ── Horizontal stat bar ──────────────────────────────────────────────────────
const StatBar = ({ label, value, total, color }) => {
  const pct = total > 0 ? Math.min(100, Math.round((value / total) * 100)) : 0;
  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between text-sm">
        <span className="text-text-secondary">{label}</span>
        <span className="font-semibold text-text-primary">
          {(value ?? 0).toLocaleString()}
          <span className="ml-1 text-xs font-normal text-text-secondary">({pct}%)</span>
        </span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-border/40">
        <div
          className="h-full rounded-full transition-[width] duration-700 ease-out"
          style={{ width: `${pct}%`, backgroundColor: color }}
        />
      </div>
    </div>
  );
};

// ── Skeleton card ────────────────────────────────────────────────────────────
const SkeletonCard = () => (
  <div className="flex animate-pulse flex-col rounded-xl border border-border/60 bg-surface-2 p-6">
    <div className="mb-4 flex items-center justify-between">
      <div className="h-10 w-10 rounded-xl bg-border/40" />
      <div className="h-5 w-20 rounded-full bg-border/30" />
    </div>
    <div className="mb-1.5 h-7 w-28 rounded bg-border/40" />
    <div className="h-4 w-36 rounded bg-border/30" />
  </div>
);

// ── Helpers ──────────────────────────────────────────────────────────────────
const fmt = (n) => (n == null ? '\u2014' : n.toLocaleString());

const fmtVnd = (amount) => {
  if (amount == null) return '\u2014';
  if (amount >= 1_000_000_000) return `${(amount / 1_000_000_000).toFixed(1)}B \u20ab`;
  if (amount >= 1_000_000) return `${(amount / 1_000_000).toFixed(1)}M \u20ab`;
  if (amount >= 1_000) return `${(amount / 1_000).toFixed(0)}K \u20ab`;
  return `${amount.toLocaleString()} \u20ab`;
};

// ── Main component ────────────────────────────────────────────────────────────
function AdminDashboardPage() {
  const { t } = useTranslation('admin-dashboard');
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchDashboard = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await userService.getDashboard();
      setData(result);
    } catch (err) {
      setError(err?.message || t('loadError', 'Unable to load dashboard data'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  // Export dashboard data as CSV
  const handleExport = () => {
    if (!data) return;
    const rows = [
      ['Metric', 'Value'],
      ['Total Users', data.totalUsers],
      ['Premium Users', data.premiumUsers],
      ['Free Users', data.freeUsers],
      ['Active Users', data.activeUsers],
      ['Locked Users', data.lockedUsers],
      ['Total Questions', data.totalQuestions],
      ['Total Interviews', data.totalInterviews],
      ['Monthly Interviews', data.monthlyInterviews],
      ['Total Revenue (VND)', data.totalRevenue],
      ['Monthly Revenue (VND)', data.monthlyRevenue],
      ['Generated At', data.generatedAt],
      ['---', '---'],
      ['Month', 'New Users', 'Interviews', 'Revenue (VND)'],
      ...(data.userGrowth || []).map((d, i) => [
        d.label,
        d.value,
        data.interviewActivity?.[i]?.value ?? 0,
        data.revenueActivity?.[i]?.value ?? 0,
      ]),
    ];
    const csv = rows.map(r => r.map(v => `"${v}"`).join(',')).join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `dashboard-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const thisMonthGrowth = data?.userGrowth?.slice(-1)[0]?.value ?? 0;

  const metrics = [
    {
      label: t('totalUsers'),
      value: fmt(data?.totalUsers),
      badge: thisMonthGrowth > 0 ? `+${thisMonthGrowth} ${t('thisMonth', 'this month')}` : null,
      badgeStyle: 'positive',
      icon: Users,
    },
    {
      label: t('questionBank'),
      value: fmt(data?.totalQuestions),
      badge: null,
      badgeStyle: 'neutral',
      icon: FileQuestion,
    },
    {
      label: t('monthlyRevenue'),
      value: fmtVnd(data?.monthlyRevenue),
      badge: data?.totalRevenue > 0 ? `${fmtVnd(data.totalRevenue)} ${t('total', 'total')}` : null,
      badgeStyle: 'positive',
      icon: CircleDollarSign,
    },
    {
      label: t('aiInterviews'),
      value: fmt(data?.totalInterviews),
      badge: (data?.monthlyInterviews ?? 0) > 0
        ? `+${data.monthlyInterviews} ${t('thisMonth', 'this month')}`
        : null,
      badgeStyle: 'positive',
      icon: Activity,
    },
  ];

  return (
    <div className="w-full animate-[fadeIn_0.5s_ease]">
      <style>{`
        @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
        @keyframes cardEntrance {
          from { opacity: 0; transform: translateY(16px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        .metric-card {
          animation: cardEntrance 0.5s cubic-bezier(0.16, 1, 0.3, 1) backwards;
          animation-delay: var(--delay, 0ms);
        }
      `}</style>

      {/* ── Page header ────────────────────────────────────────────────── */}
      <div className="mb-8">
        <div className="mb-4 flex items-center gap-2 text-xs text-text-secondary/70">
          <span>{t('breadcrumbAdmin', 'Admin')}</span>
          <span className="mx-1 text-text-disabled">/</span>
          <span aria-current="page">{t('overview', 'Overview')}</span>
        </div>

        <div className="flex flex-col items-stretch gap-6 md:flex-row md:items-start md:justify-between">
          <div className="flex-1">
            <h1 className="mb-2 text-2xl font-bold leading-[1.3] text-text-primary md:text-[32px] md:leading-[1.2]">
              {t('overview', 'Overview')}
            </h1>
            <p className="text-base leading-[1.6] text-text-secondary">
              {t('overviewDesc', 'Monitor platform activity, content, and business performance.')}
            </p>
          </div>

          <div className="flex shrink-0 items-center gap-3">
            <button
              type="button"
              className="grid h-10 w-10 place-items-center rounded-xl border border-border/60 bg-surface-2 text-text-secondary transition hover:border-primary/40 hover:text-primary disabled:opacity-40"
              onClick={fetchDashboard}
              disabled={loading}
              title="Refresh"
              aria-label="Refresh dashboard"
            >
              <RefreshCw size={16} className={loading ? 'animate-spin' : ''} />
            </button>
            <button
              className="min-h-10 whitespace-nowrap rounded-xl bg-gradient-to-r from-primary to-primary-dark px-5 text-sm font-semibold text-white shadow-sm transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-0.5 hover:shadow-md active:scale-[0.97] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 disabled:opacity-50"
              type="button"
              onClick={handleExport}
              disabled={!data || loading}
            >
              {t('exportReport', 'Export report')}
            </button>
          </div>
        </div>
      </div>

      {/* ── Error banner ────────────────────────────────────────────────── */}
      {error && !loading && (
        <div className="mb-6 flex items-center gap-3 rounded-xl border border-danger/30 bg-danger/5 p-4 text-sm text-danger">
          <AlertCircle size={18} className="shrink-0" />
          <span>{error}</span>
          <button
            type="button"
            className="ml-auto shrink-0 rounded-lg border border-danger/30 px-3 py-1 text-xs font-medium hover:bg-danger/10"
            onClick={fetchDashboard}
          >
            {t('retry', 'Retry')}
          </button>
        </div>
      )}

      <div className="flex flex-col gap-8">

        {/* ── Metric cards ────────────────────────────────────────────────── */}
        <section
          className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 min-[1100px]:grid-cols-4"
          aria-label={t('platformSummaryAria', 'Platform summary')}
        >
          {loading
            ? [...Array(4)].map((_, i) => <SkeletonCard key={i} />)
            : metrics.map(({ label, value, badge, badgeStyle, icon: Icon }, index) => (
              <article
                className="metric-card flex flex-col rounded-xl border border-border/60 bg-surface-2 p-6 shadow-[0_2px_4px_rgba(31,45,61,0.05)] transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1 hover:border-primary/20 hover:shadow-[0_8px_24px_rgba(31,45,61,0.10)]"
                key={label}
                style={{ '--delay': `${index * 80}ms` }}
              >
                <div className="mb-4 flex items-center justify-between">
                  <span className="grid h-10 w-10 place-items-center rounded-xl bg-gradient-to-br from-primary-xlight to-primary-light/40 text-primary-dark">
                    <Icon size={20} />
                  </span>
                  {badge && (
                    <span className={`rounded-full px-2.5 py-1 text-[11px] font-semibold leading-[1.2] ${
                      badgeStyle === 'positive'
                        ? 'bg-success-light text-success'
                        : 'bg-border/40 text-text-secondary'
                    }`}>
                      {badge}
                    </span>
                  )}
                </div>
                <strong className="text-2xl leading-[1.3] text-text-primary">{value}</strong>
                <span className="mt-1.5 text-sm text-text-secondary">{label}</span>
              </article>
            ))
          }
        </section>

        {/* ── Bottom section: Subscription stats + Activity charts ─────── */}
        <section className="grid grid-cols-1 gap-6 lg:grid-cols-2">

          {/* Subscription statistics */}
          <div className="flex flex-col gap-5 rounded-xl border border-border/60 bg-surface-2 p-6 shadow-[0_2px_4px_rgba(31,45,61,0.05)] transition-all duration-300 hover:shadow-[0_8px_24px_rgba(31,45,61,0.08)]">
            <div className="flex items-center gap-2">
              <span className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-to-br from-primary-xlight to-primary-light/40 text-primary-dark">
                <Crown size={18} />
              </span>
              <h2 className="text-base font-semibold text-text-primary">
                {t('subscriptionStats', 'Subscription Statistics')}
              </h2>
            </div>

            {loading ? (
              <div className="flex animate-pulse flex-col gap-5">
                {[...Array(4)].map((_, i) => (
                  <div key={i} className="flex flex-col gap-1.5">
                    <div className="flex justify-between">
                      <div className="h-4 w-28 rounded bg-border/40" />
                      <div className="h-4 w-16 rounded bg-border/40" />
                    </div>
                    <div className="h-2 rounded-full bg-border/30" />
                  </div>
                ))}
              </div>
            ) : data ? (
              <div className="flex flex-col gap-4">
                <StatBar label={t('premiumUsers', 'Premium users')} value={data.premiumUsers ?? 0} total={data.totalUsers ?? 0} color="#f59e0b" />
                <StatBar label={t('freeUsers', 'Free users')} value={data.freeUsers ?? 0} total={data.totalUsers ?? 0} color="#6FB6E8" />
                <StatBar label={t('activeUsers', 'Active users')} value={data.activeUsers ?? 0} total={data.totalUsers ?? 0} color="#22c55e" />
                <StatBar label={t('lockedUsers', 'Locked users')} value={data.lockedUsers ?? 0} total={data.totalUsers ?? 0} color="#ef4444" />
              </div>
            ) : (
              <p className="text-sm text-text-secondary">{t('noData', 'No data available')}</p>
            )}
          </div>

          {/* Activity sparkline charts */}
          <div className="flex flex-col gap-5 rounded-xl border border-border/60 bg-surface-2 p-6 shadow-[0_2px_4px_rgba(31,45,61,0.05)] transition-all duration-300 hover:shadow-[0_8px_24px_rgba(31,45,61,0.08)]">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-to-br from-primary-xlight to-primary-light/40 text-primary-dark">
                  <Activity size={18} />
                </span>
                <h2 className="text-base font-semibold text-text-primary">
                  {t('platformActivity', 'Platform activity')}
                </h2>
              </div>
              {!loading && !error && (
                <span className="rounded-full bg-success-light px-3 py-1 text-[11px] font-semibold leading-[1.2] text-success shadow-sm">
                  {t('allSystemsNormal', 'All systems normal')}
                </span>
              )}
            </div>

            {loading ? (
              <div className="flex animate-pulse flex-col gap-6">
                <div className="h-4 w-48 rounded bg-border/40" />
                <div className="h-14 rounded bg-border/30" />
                <div className="h-4 w-48 rounded bg-border/40" />
                <div className="h-14 rounded bg-border/30" />
              </div>
            ) : data ? (
              <div className="flex flex-col gap-6">

                {/* User growth chart */}
                <div>
                  <div className="mb-2 flex items-center justify-between">
                    <span className="text-xs text-text-secondary">
                      {t('newUsersChart', 'New users \u2014 last 6 months')}
                    </span>
                    <span className="text-xs font-semibold text-text-primary">
                      {(data.userGrowth || []).reduce((s, d) => s + d.value, 0).toLocaleString()}
                    </span>
                  </div>
                  <Sparkline data={data.userGrowth} color="#6FB6E8" height={56} />
                  <div className="mt-1.5 flex justify-between text-[10px] text-text-secondary/60">
                    {(data.userGrowth || []).map(d => (
                      <span key={`u-${d.year}-${d.month}`}>{d.label?.split(' ')[0]}</span>
                    ))}
                  </div>
                </div>

                {/* Interview activity chart */}
                <div>
                  <div className="mb-2 flex items-center justify-between">
                    <span className="text-xs text-text-secondary">
                      {t('interviewActivityChart', 'AI interviews \u2014 last 6 months')}
                    </span>
                    <span className="text-xs font-semibold text-text-primary">
                      {(data.interviewActivity || []).reduce((s, d) => s + d.value, 0).toLocaleString()}
                    </span>
                  </div>
                  <Sparkline data={data.interviewActivity} color="#a78bfa" height={56} />
                  <div className="mt-1.5 flex justify-between text-[10px] text-text-secondary/60">
                    {(data.interviewActivity || []).map(d => (
                      <span key={`i-${d.year}-${d.month}`}>{d.label?.split(' ')[0]}</span>
                    ))}
                  </div>
                </div>

              </div>
            ) : (
              <p className="text-sm text-text-secondary">{t('noData', 'No data available')}</p>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}

export default AdminDashboardPage;