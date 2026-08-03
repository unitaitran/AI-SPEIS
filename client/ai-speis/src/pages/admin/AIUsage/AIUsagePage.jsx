import React, { useEffect, useState, useCallback } from 'react';
import {
  RefreshCw,
  Activity,
  AlertTriangle,
  CheckCircle2,
  Gauge,
  Cloud,
  AlertCircle,
  ServerCrash,
  DollarSign,
  TrendingUp,
  PieChart as PieIcon,
  LineChart as LineIcon,
  Calendar,
  DatabaseZap,
} from 'lucide-react';
import {
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip as RechartsTooltip,
  Legend,
} from 'recharts';
import { fetchGoogleDashboard } from '../../../services/GoogleQuotaService';
import { useTranslation } from 'react-i18next';
import './AIUsagePage.css';

/* ─────────────────────────────── helpers ──────────────────────── */

function formatNumber(num, t) {
  if (num == null) return '—';
  if (num >= 9_000_000_000_000_000) return t ? t('unlimited', 'Không giới hạn') : 'Không giới hạn';
  if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(1)}M`;
  if (num >= 1_000) return `${(num / 1_000).toFixed(1)}K`;
  return num.toLocaleString();
}

function formatCurrency(amount) {
  if (amount == null) return '$0.00';
  return amount.toLocaleString('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

function getStatusInfo(pct, t) {
  if (pct == null) return { label: t ? t('statusNoData', 'Chưa có data') : 'Chưa có data', cls: 'healthy', color: 'green' };
  if (pct >= 85)   return { label: t ? t('statusCritical', 'Cảnh báo') : 'Cảnh báo', cls: 'critical', color: 'red' };
  if (pct >= 60)   return { label: t ? t('statusWarning', 'Gần giới hạn') : 'Gần giới hạn', cls: 'warning', color: 'amber' };
  return { label: t ? t('statusHealthy', 'Hoạt động tốt') : 'Hoạt động tốt', cls: 'healthy', color: 'green' };
}

function formatTimestamp(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  return d.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

const PIE_COLORS = ['#6FB6E8', '#4CAF8F', '#F4B64A', '#7C3AED', '#E76F6F'];

/* ──────────────────────────── Skeletons ───────────────────────── */

function SkeletonCards() {
  return (
    <div className="ai-usage-page__summary-grid">
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="ai-usage-skeleton ai-usage-skeleton--card" />
      ))}
    </div>
  );
}

function SkeletonTable() {
  return (
    <div className="ai-usage-page__table-section">
      <div className="ai-usage-page__table-header">
        <div className="ai-usage-skeleton" style={{ width: 180, height: 20 }} />
      </div>
      {[0, 1, 2, 3, 4].map((i) => (
        <div key={i} className="ai-usage-skeleton ai-usage-skeleton--row" />
      ))}
    </div>
  );
}

/* ────────────────────────── Summary Card ──────────────────────── */

function SummaryCard({ icon: Icon, iconCls, value, label, badge, badgeCls }) {
  return (
    <article className="ai-usage-summary-card">
      <div className="ai-usage-summary-card__icon-row">
        <span className={`ai-usage-summary-card__icon ${iconCls}`}>
          <Icon size={22} />
        </span>
        {badge != null && (
          <span className={`ai-usage-summary-card__badge ${badgeCls}`}>
            {badge}
          </span>
        )}
      </div>
      <strong className="ai-usage-summary-card__value">{value}</strong>
      <span className="ai-usage-summary-card__label">{label}</span>
    </article>
  );
}

/* ────────────────────────── Progress Bar ──────────────────────── */

function ProgressBar({ percent, t }) {
  const pct = percent ?? 0;
  const { color } = getStatusInfo(pct, t);

  return (
    <div className="ai-usage-progress">
      <div className="ai-usage-progress__bar-wrap">
        <div
          className={`ai-usage-progress__bar-fill ai-usage-progress__bar-fill--${color}`}
          style={{ width: `${Math.min(pct, 100)}%` }}
        />
      </div>
      <span className="ai-usage-progress__label">{pct.toFixed(1)}%</span>
    </div>
  );
}

/* ──────────────────────────── Main Page ───────────────────────── */

export default function AIUsagePage() {
  const { t } = useTranslation('admin-dashboard');
  const [dashboard, setDashboard] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const token = localStorage.getItem('token');
      if (!token) throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
      const result = await fetchGoogleDashboard(token);
      setDashboard(result);
    } catch (err) {
      setError(err.message || 'Không thể tải dữ liệu Google Resource Dashboard');
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial load + 60s auto refresh
  useEffect(() => {
    loadData();

    const interval = setInterval(() => {
      loadData();
    }, 60_000); // 60s auto refresh

    return () => clearInterval(interval);
  }, [loadData]);

  /* ── Compute Quota Summary ────────────────────────── */
  const usageData = dashboard?.usage ?? dashboard;
  const services = usageData?.services ?? [];
  const totalServices = services.length;
  const nearLimitCount = services.filter(
    (s) => s.percentUsed != null && s.percentUsed >= 60
  ).length;
  const criticalCount = services.filter(
    (s) => s.percentUsed != null && s.percentUsed >= 85
  ).length;
  const healthyCount = totalServices - nearLimitCount;
  const avgUsage =
    totalServices > 0
      ? (
          services.reduce((sum, s) => sum + (s.percentUsed ?? 0), 0) /
          totalServices
        ).toFixed(1)
      : '0.0';

  /* ── Cost Data ────────────────────────────────────── */
  const cost = dashboard?.cost;
  const hasCostData = cost?.hasData ?? false;

  return (
    <div className="ai-usage-page">
      {/* ── Header ───────────────────────────────────────── */}
      <div className="ai-usage-page__header">
        <div className="ai-usage-page__breadcrumb">
          <span>{t('breadcrumbAdmin', 'Admin')}</span>
          <span className="separator">/</span>
          <span aria-current="page">AI Usage</span>
        </div>

        <div className="ai-usage-page__header-row">
          <div>
            <h1 className="ai-usage-page__title">{t('aiUsageTitle', 'AI Usage & Cloud Resource Monitor')}</h1>
            <p className="ai-usage-page__subtitle">
              {t('aiUsageSubtitle', 'Theo dõi toàn bộ Quota, Usage và Chi phí Billing Google Cloud trong hệ thống AI-SPEIS.')}
            </p>
          </div>

          <div className="ai-usage-page__actions">
            {dashboard?.projectId && (
              <span className="ai-usage-project-badge">
                <span className="ai-usage-project-badge__dot" />
                {dashboard.projectId}
              </span>
            )}
            {dashboard?.queriedAt && (
              <span className="ai-usage-page__timestamp">
                {t('lastUpdated', 'Cập nhật:')} {formatTimestamp(dashboard.queriedAt)}
              </span>
            )}
            <button
              className="ai-usage-page__refresh-btn"
              onClick={loadData}
              disabled={loading}
              type="button"
            >
              <RefreshCw size={16} className={loading ? 'spinning' : ''} />
              {loading ? t('refreshing', 'Đang tải...') : t('refreshBtn', 'Làm mới')}
            </button>
          </div>
        </div>
      </div>

      {/* ── Error banner with Retry ──────────────────────── */}
      {error && (
        <div className="ai-usage-error">
          <AlertCircle size={20} className="ai-usage-error__icon" />
          <div className="ai-usage-error__text">
            <p className="ai-usage-error__title">{t('fetchErrorTitle', 'Không thể tải dữ liệu')}</p>
            <p className="ai-usage-error__desc">{error}</p>
          </div>
          <button
            className="ai-usage-page__refresh-btn"
            style={{ minHeight: 32, padding: '0 14px', fontSize: 12 }}
            onClick={loadData}
            type="button"
          >
            <RefreshCw size={14} /> {t('retryBtn', 'Thử lại')}
          </button>
        </div>
      )}

      {/* ── Loading skeleton ─────────────────────────────── */}
      {loading && !dashboard && (
        <>
          <SkeletonCards />
          <SkeletonCards />
          <SkeletonTable />
        </>
      )}

      {/* ── Content ──────────────────────────────────────── */}
      {dashboard && (
        <>
          {/* 1. AI Usage Quota Summary Cards */}
          <div className="ai-usage-page__summary-grid">
            <SummaryCard
              icon={Cloud}
              iconCls="ai-usage-summary-card__icon--blue"
              value={totalServices}
              label={t('totalServices', 'Tổng dịch vụ')}
              badge={`${totalServices} API`}
              badgeCls="ai-usage-summary-card__badge--info"
            />
            <SummaryCard
              icon={AlertTriangle}
              iconCls="ai-usage-summary-card__icon--amber"
              value={nearLimitCount}
              label={t('nearLimit', 'Gần giới hạn')}
              badge={criticalCount > 0 ? `${criticalCount} critical` : 'OK'}
              badgeCls={
                criticalCount > 0
                  ? 'ai-usage-summary-card__badge--warning'
                  : 'ai-usage-summary-card__badge--success'
              }
            />
            <SummaryCard
              icon={CheckCircle2}
              iconCls="ai-usage-summary-card__icon--green"
              value={healthyCount}
              label={t('healthy', 'Hoạt động tốt')}
              badge="Healthy"
              badgeCls="ai-usage-summary-card__badge--success"
            />
            <SummaryCard
              icon={Gauge}
              iconCls="ai-usage-summary-card__icon--purple"
              value={`${avgUsage}%`}
              label={t('avgUsage', 'TB sử dụng')}
              badge="avg"
              badgeCls="ai-usage-summary-card__badge--info"
            />
          </div>

          {/* 2. Cloud Cost Section */}
          <section className="ai-usage-cost-section">
            <div className="ai-usage-cost-section__header">
              <h2 className="ai-usage-cost-section__title">
                <DollarSign size={20} className="text-success" />
                {t('cloudBillingCost', 'Cloud Billing Cost')}
              </h2>
              <span className="ai-usage-cost-section__badge">
                BigQuery Export
              </span>
            </div>

            {/* 4 Cost Summary Cards */}
            <div className="ai-usage-page__summary-grid">
              <SummaryCard
                icon={DollarSign}
                iconCls="ai-usage-summary-card__icon--green"
                value={formatCurrency(cost?.todayCost)}
                label={t('todayCost', "Hôm nay (Today's Cost)")}
                badge="Today"
                badgeCls="ai-usage-summary-card__badge--success"
              />
              <SummaryCard
                icon={Calendar}
                iconCls="ai-usage-summary-card__icon--blue"
                value={formatCurrency(cost?.yesterdayCost)}
                label={t('yesterdayCost', 'Hôm qua (Yesterday Cost)')}
                badge="Yesterday"
                badgeCls="ai-usage-summary-card__badge--info"
              />
              <SummaryCard
                icon={TrendingUp}
                iconCls="ai-usage-summary-card__icon--purple"
                value={formatCurrency(cost?.monthlyCost)}
                label={t('monthlyCost', 'Tháng này (Monthly Cost)')}
                badge="MTD"
                badgeCls="ai-usage-summary-card__badge--info"
              />
              <SummaryCard
                icon={Activity}
                iconCls="ai-usage-summary-card__icon--amber"
                value={formatCurrency(cost?.forecast)}
                label={t('forecast', 'Dự báo cả tháng (Forecast)')}
                badge="Est."
                badgeCls="ai-usage-summary-card__badge--warning"
              />
            </div>

            {/* Charts or Empty State */}
            {hasCostData ? (
              <div className="ai-usage-cost-charts">
                {/* Pie Chart: Cost By Service */}
                <div className="ai-usage-chart-card">
                  <h3 className="ai-usage-chart-card__title">
                    <span>{t('costByService', 'Cost By Service')}</span>
                    <PieIcon size={18} className="text-text-secondary" />
                  </h3>
                  <div className="ai-usage-chart-card__body">
                    {cost?.topServices?.length > 0 ? (
                      <ResponsiveContainer width="100%" height={260}>
                        <PieChart>
                          <Pie
                            data={cost.topServices}
                            dataKey="cost"
                            nameKey="serviceName"
                            cx="50%"
                            cy="50%"
                            innerRadius={55}
                            outerRadius={85}
                            paddingAngle={4}
                          >
                            {cost.topServices.map((entry, index) => (
                              <Cell
                                key={`cell-${index}`}
                                fill={PIE_COLORS[index % PIE_COLORS.length]}
                              />
                            ))}
                          </Pie>
                          <RechartsTooltip
                            formatter={(value) => [formatCurrency(value), 'Cost']}
                          />
                          <Legend verticalAlign="bottom" height={36} />
                        </PieChart>
                      </ResponsiveContainer>
                    ) : (
                      <div className="text-xs text-text-secondary">Chưa có chi phí theo service</div>
                    )}
                  </div>
                </div>

                {/* Line Chart: Daily Cost Trend */}
                <div className="ai-usage-chart-card">
                  <h3 className="ai-usage-chart-card__title">
                    <span>{t('dailyCostTrend', 'Daily Cost Trend (14 ngày)')}</span>
                    <LineIcon size={18} className="text-text-secondary" />
                  </h3>
                  <div className="ai-usage-chart-card__body">
                    {cost?.dailyTrend?.length > 0 ? (
                      <ResponsiveContainer width="100%" height={260}>
                        <LineChart data={cost.dailyTrend}>
                          <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                          <YAxis
                            tick={{ fontSize: 11 }}
                            tickFormatter={(v) => `$${v}`}
                          />
                          <RechartsTooltip
                            formatter={(value) => [formatCurrency(value), 'Cost']}
                          />
                          <Line
                            type="monotone"
                            dataKey="cost"
                            stroke="#6FB6E8"
                            strokeWidth={3}
                            dot={{ r: 4, fill: '#3F7FAE' }}
                            activeDot={{ r: 6 }}
                          />
                        </LineChart>
                      </ResponsiveContainer>
                    ) : (
                      <div className="text-xs text-text-secondary">Chưa có xu hướng chi phí theo ngày</div>
                    )}
                  </div>
                </div>
              </div>
            ) : (
              <div className="ai-usage-cost-empty">
                <div className="ai-usage-cost-empty__icon">
                  <DatabaseZap size={26} />
                </div>
                <h4 className="ai-usage-cost-empty__title">
                  {t('billingEmptyTitle', 'Billing Export chưa có dữ liệu')}
                </h4>
                <p className="ai-usage-cost-empty__desc">
                  {t('billingEmptyDesc', 'Tính năng Cloud Cost tự động lấy thông tin chi phí từ BigQuery Billing Export. Vui lòng bật tính năng Standard / Detailed Usage Cost Export trên Google Cloud Console tới dataset BigQuery tương ứng.')}
                </p>
              </div>
            )}
          </section>

          {/* 3. Chi tiết từng dịch vụ (Table) */}
          {services.length > 0 ? (
            <div className="ai-usage-page__table-section">
              <div className="ai-usage-page__table-header">
                <h2 className="ai-usage-page__table-title">{t('serviceDetailsTitle', 'Chi tiết từng dịch vụ')}</h2>
                <span className="ai-usage-page__table-count">
                  {services.length} metric{services.length !== 1 ? 's' : ''}
                </span>
              </div>

              <div className="ai-usage-page__table-wrap">
                <table className="ai-usage-table">
                  <thead>
                    <tr>
                      <th>{t('colService', 'Dịch vụ')}</th>
                      <th>{t('colStatus', 'Trạng thái')}</th>
                      <th>{t('colLimit', 'Giới hạn')}</th>
                      <th>{t('colUsage', 'Đã dùng')}</th>
                      <th>{t('colRemaining', 'Còn lại')}</th>
                      <th>{t('colPercentUsed', '% Sử dụng')}</th>
                      <th>{t('colUnit', 'Đơn vị')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {services.map((svc, idx) => {
                      const status = getStatusInfo(svc.percentUsed, t);
                      return (
                        <tr key={`${svc.serviceName}-${svc.quotaMetric}-${idx}`}>
                          <td>
                            <div className="ai-usage-svc-name">
                              <span className="ai-usage-svc-name__title">
                                {svc.serviceName}
                              </span>
                              {svc.quotaMetric && (
                                <span className="ai-usage-svc-name__metric">
                                  {svc.quotaMetric}
                                </span>
                              )}
                            </div>
                          </td>
                          <td>
                            <span
                              className={`ai-usage-status ai-usage-status--${status.cls}`}
                            >
                              <span className="ai-usage-status__dot" />
                              {status.label}
                            </span>
                          </td>
                          <td>
                            <span className="ai-usage-num ai-usage-num--limit">
                              {formatNumber(svc.limit, t)}
                            </span>
                          </td>
                          <td>
                            <span className="ai-usage-num ai-usage-num--usage">
                              {formatNumber(svc.currentUsage, t)}
                            </span>
                          </td>
                          <td>
                            <span className="ai-usage-num ai-usage-num--remaining">
                              {formatNumber(svc.remaining, t)}
                            </span>
                          </td>
                          <td>
                            <ProgressBar percent={svc.percentUsed} t={t} />
                          </td>
                          <td>
                            <span className="ai-usage-unit">
                              {svc.unit || '—'}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          ) : (
            <div className="ai-usage-page__table-section">
              <div className="ai-usage-empty">
                <div className="ai-usage-empty__icon">
                  <ServerCrash size={28} />
                </div>
                <h3 className="ai-usage-empty__title">{t('noQuotaDataTitle', 'Không có dữ liệu quota')}</h3>
                <p className="ai-usage-empty__desc">
                  {t('noQuotaDataDesc', 'Không tìm thấy metric quota nào cho các dịch vụ đang bật. Điều này có thể do Service Account chưa được cấp quyền Monitoring Viewer hoặc các dịch vụ chưa tạo traffic.')}
                </p>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
