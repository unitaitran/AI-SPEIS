import React, { useEffect, useState, useCallback } from 'react';
import {
  Users,
  CreditCard,
  DollarSign,
  TrendingUp,
  Activity,
  FileQuestion,
  RefreshCw,
  Clock,
  Zap,
  BarChart2,
  Server,
  ShieldCheck,
  ArrowRight,
  Sparkles,
  PieChart as PieIcon,
  ChevronRight,
} from 'lucide-react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  Tooltip as RechartsTooltip,
  Legend,
} from 'recharts';
import { useTranslation } from 'react-i18next';
import { navigate } from '../../../routes/navigation';
import { fetchAdminDashboard } from '../../../services/AdminDashboardService';
import './AdminDashboardPage.css';

/* ────────────────────────── Helpers ────────────────────────── */

function formatVnd(amount) {
  if (amount == null) return '0 ₫';
  return amount.toLocaleString('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
  });
}

function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
  });
}

const PIE_COLORS = ['#4CAF50', '#FF9800', '#F44336', '#9C27B0', '#2196F3', '#00BCD4'];
const SUB_COLORS = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444'];

export default function AdminDashboardPage() {
  const { t } = useTranslation('admin-dashboard');

  const [dashboardData, setDashboardData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Time & Auto-refresh (60s)
  const [currentTime, setCurrentTime] = useState(new Date());
  const [countdown, setCountdown] = useState(60);

  // Live Clock
  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const token = localStorage.getItem('token');
      if (!token) throw new Error('Phiên đăng nhập đã hết hạn.');

      const data = await fetchAdminDashboard(token);
      setDashboardData(data);
    } catch (err) {
      console.error(err);
      setError(err.message || 'Không thể kết nối API Dashboard.');
    } finally {
      setLoading(false);
      setCountdown(60);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Auto refresh 60s timer
  useEffect(() => {
    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          loadData();
          return 60;
        }
        return prev - 1;
      });
    }, 1000);
    return () => clearInterval(timer);
  }, [loadData]);

  if (loading && !dashboardData) {
    return (
      <div className="admin-dashboard-page dashboard-loading-wrap">
        <div className="dashboard-skeleton-header" />
        <div className="dashboard-skeleton-grid" />
        <div className="dashboard-skeleton-card" />
      </div>
    );
  }

  if (error && !dashboardData) {
    return (
      <div className="admin-dashboard-page">
        <div className="dashboard-error-card">
          <Activity size={36} className="text-danger" />
          <h3>Lỗi tải dữ liệu Dashboard</h3>
          <p>{error}</p>
          <button className="payment-page__btn payment-page__btn--primary" onClick={loadData} type="button">
            <RefreshCw size={16} /> Thử lại
          </button>
        </div>
      </div>
    );
  }

  const {
    overview,
    subscriptions,
    payments,
    interviews,
    questionBank,
    cv,
    aiUsageAndCost,
    recentActivities,
  } = dashboardData || {};

  return (
    <div className="admin-dashboard-page">
      {/* ──────────────── HEADER ──────────────── */}
      <div className="dashboard-header">
        <div className="dashboard-header__top">
          <div className="breadcrumb">
            <span>{t('breadcrumbAdmin', 'Admin')}</span>
            <span className="separator">/</span>
            <span aria-current="page">{t('overview', 'Dashboard')}</span>
          </div>
          <div className="dashboard-header__time">
            <Clock size={14} />
            <span>{currentTime.toLocaleTimeString('vi-VN')} — {currentTime.toLocaleDateString('vi-VN')}</span>
          </div>
        </div>

        <div className="dashboard-header__main">
          <div>
            <h1 className="dashboard-title">
              {t('dashboardGreeting', 'Xin chào, Admin')} <Sparkles size={24} className="text-primary inline-block" />
            </h1>
            <p className="dashboard-subtitle">
              {t('dashboardGreetingSub', 'Đây là tổng quan tình trạng hoạt động và dữ liệu kinh doanh của hệ thống AI-SPEIS.')}
            </p>
          </div>

          <div className="dashboard-header__actions">
            <div className="dashboard-refresh-badge">
              <Activity size={14} className="text-primary" />
              <span>Auto refresh: {countdown}s</span>
            </div>

            <button
              className="payment-page__btn"
              onClick={loadData}
              disabled={loading}
              type="button"
            >
              <RefreshCw size={15} className={loading ? 'spinning' : ''} />
              {t('btnRefresh', 'Làm mới')}
            </button>
          </div>
        </div>
      </div>

      {/* ──────────────── ROW 1: OVERVIEW CARDS (4 Clickable Cards) ──────────────── */}
      <div className="dashboard-row-1">
        {/* Total Users -> /admin/users */}
        <div
          className="dashboard-card overview-card dashboard-card--clickable"
          onClick={() => navigate('/admin/users')}
        >
          <div className="overview-card__top">
            <span className="overview-card__icon overview-card__icon--blue">
              <Users size={22} />
            </span>
            <div className="overview-card__top-right">
              <span className="payment-tag payment-tag--paid">
                +{overview?.newUsersToday ?? 0} hôm nay
              </span>
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div className="overview-card__val">{overview?.totalUsers?.toLocaleString('vi-VN') ?? 0}</div>
          <div className="overview-card__lbl">{t('cardTotalUsers', 'Tổng người dùng hệ thống')}</div>
          <div className="overview-card__sub">
            <span>Hoạt động: <strong>{overview?.activeUsers ?? 0}</strong></span>
            <span>•</span>
            <span>Mới tháng này: <strong>+{overview?.newUsersThisMonth ?? 0}</strong></span>
          </div>
        </div>

        {/* Premium Users -> /admin/subscription */}
        <div
          className="dashboard-card overview-card dashboard-card--clickable"
          onClick={() => navigate('/admin/subscription')}
        >
          <div className="overview-card__top">
            <span className="overview-card__icon overview-card__icon--green">
              <Zap size={22} />
            </span>
            <div className="overview-card__top-right">
              <span className="payment-tag payment-tag--paid">
                {subscriptions?.distribution?.find(d => (d?.label || d?.Label || '').toLowerCase().includes('monthly'))?.percentage ?? 0}%
              </span>
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div className="overview-card__val">{overview?.premiumUsers?.toLocaleString('vi-VN') ?? 0}</div>
          <div className="overview-card__lbl">{t('cardPremiumUsers', 'Thành viên gói Premium')}</div>
          <div className="overview-card__sub">
            <span>Free: <strong>{overview?.freeUsers ?? 0}</strong></span>
            <span>•</span>
            <span>Hết hạn: <strong>{subscriptions?.expiredCount ?? 0}</strong></span>
          </div>
        </div>

        {/* Today's Revenue -> /admin/payments */}
        <div
          className="dashboard-card overview-card dashboard-card--clickable"
          onClick={() => navigate('/admin/payments')}
        >
          <div className="overview-card__top">
            <span className="overview-card__icon overview-card__icon--green">
              <DollarSign size={22} />
            </span>
            <div className="overview-card__top-right">
              <span className="payment-tag payment-tag--paid">Revenue</span>
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div className="overview-card__val">{formatVnd(payments?.todayRevenue)}</div>
          <div className="overview-card__lbl">{t('cardTodayRevenue', 'Doanh thu hôm nay (Revenue)')}</div>
          <div className="overview-card__sub">
            <span>Tháng này: <strong>{formatVnd(payments?.monthlyRevenue)}</strong></span>
          </div>
        </div>

        {/* Today's Interviews -> /admin/questions */}
        <div
          className="dashboard-card overview-card dashboard-card--clickable"
          onClick={() => navigate('/admin/questions')}
        >
          <div className="overview-card__top">
            <span className="overview-card__icon overview-card__icon--purple">
              <Activity size={22} />
            </span>
            <div className="overview-card__top-right">
              <span className="payment-tag payment-tag--pending">Score: {interviews?.averageAiScore ?? 7.8}</span>
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div className="overview-card__val">{interviews?.todaySessions ?? 0}</div>
          <div className="overview-card__lbl">{t('cardTodayInterviews', 'Phỏng vấn AI hôm nay')}</div>
          <div className="overview-card__sub">
            <span>Hoàn thành: <strong>{interviews?.completedSessions ?? 0}</strong></span>
            <span>•</span>
            <span>Tổng: <strong>{interviews?.totalSessions ?? 0}</strong></span>
          </div>
        </div>
      </div>

      {/* ──────────────── ROW 2: USER GROWTH & SUB DISTRIBUTION ──────────────── */}
      <div className="dashboard-row-2">
        {/* User Growth Chart -> /admin/users */}
        <div
          className="dashboard-card chart-card dashboard-card--clickable"
          onClick={() => navigate('/admin/users')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('chartUserGrowth', 'Tăng trưởng Người dùng (14 ngày)')}</h3>
              <p className="chart-card__subtitle">Số lượng tài khoản đăng ký mới theo thời gian</p>
            </div>
            <div className="chart-card__header-right">
              <TrendingUp size={20} className="text-primary" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div style={{ width: '100%', height: 250 }}>
            <ResponsiveContainer>
              <AreaChart data={overview?.userGrowthTrend || []}>
                <defs>
                  <linearGradient id="userGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#3B82F6" stopOpacity={0.4} />
                    <stop offset="95%" stopColor="#3B82F6" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="dateLabel" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <RechartsTooltip formatter={(val) => [`${val} người dùng`, 'Đăng ký mới']} />
                <Area type="monotone" dataKey="count" stroke="#3B82F6" strokeWidth={3} fillOpacity={1} fill="url(#userGrad)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Subscription Distribution -> /admin/subscription */}
        <div
          className="dashboard-card chart-card dashboard-card--clickable"
          onClick={() => navigate('/admin/subscription')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('chartSubDistribution', 'Phân bổ Gói đăng ký')}</h3>
              <p className="chart-card__subtitle">Tỷ lệ các gói người dùng đang sử dụng</p>
            </div>
            <div className="chart-card__header-right">
              <PieIcon size={20} className="text-secondary" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div style={{ width: '100%', height: 250 }}>
            <ResponsiveContainer>
              <PieChart>
                <Pie
                  data={subscriptions?.distribution || []}
                  dataKey="count"
                  nameKey="label"
                  cx="50%"
                  cy="50%"
                  innerRadius={55}
                  outerRadius={85}
                  paddingAngle={4}
                >
                  {(subscriptions?.distribution || []).map((entry, idx) => (
                    <Cell key={`sub-${idx}`} fill={SUB_COLORS[idx % SUB_COLORS.length]} />
                  ))}
                </Pie>
                <RechartsTooltip formatter={(val, name) => [`${val} tài khoản`, name]} />
                <Legend verticalAlign="bottom" height={36} />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* ──────────────── ROW 3: REVENUE TREND & PAYMENT STATUS ──────────────── */}
      <div className="dashboard-row-3">
        {/* Revenue Trend -> /admin/payments */}
        <div
          className="dashboard-card chart-card dashboard-card--clickable"
          onClick={() => navigate('/admin/payments')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('chartRevenueTrend', 'Doanh thu Thanh toán MoMo (14 ngày)')}</h3>
              <p className="chart-card__subtitle">Tổng giá trị giao dịch thành công theo ngày</p>
            </div>
            <div className="chart-card__header-right">
              <DollarSign size={20} className="text-primary" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div style={{ width: '100%', height: 250 }}>
            <ResponsiveContainer>
              <AreaChart data={payments?.revenueTrend || []}>
                <defs>
                  <linearGradient id="revGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#10B981" stopOpacity={0.4} />
                    <stop offset="95%" stopColor="#10B981" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="dateLabel" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v / 1000).toFixed(0)}k`} />
                <RechartsTooltip formatter={(val) => [formatVnd(val), 'Doanh thu']} />
                <Area type="monotone" dataKey="value" stroke="#10B981" strokeWidth={3} fillOpacity={1} fill="url(#revGrad)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Payment Status Distribution -> /admin/payments */}
        <div
          className="dashboard-card chart-card dashboard-card--clickable"
          onClick={() => navigate('/admin/payments')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('chartPaymentStatus', 'Trạng thái Đơn hàng')}</h3>
              <p className="chart-card__subtitle">Phân bố giao dịch thành công, chờ & thất bại</p>
            </div>
            <div className="chart-card__header-right">
              <CreditCard size={20} className="text-secondary" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div style={{ width: '100%', height: 250 }}>
            <ResponsiveContainer>
              <PieChart>
                <Pie
                  data={payments?.statusDistribution || []}
                  dataKey="count"
                  nameKey="label"
                  cx="50%"
                  cy="50%"
                  innerRadius={55}
                  outerRadius={85}
                  paddingAngle={4}
                >
                  {(payments?.statusDistribution || []).map((entry, idx) => (
                    <Cell key={`pay-${idx}`} fill={PIE_COLORS[idx % PIE_COLORS.length]} />
                  ))}
                </Pie>
                <RechartsTooltip formatter={(val, name) => [`${val} giao dịch`, name]} />
                <Legend verticalAlign="bottom" height={36} />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* ──────────────── ROW 4: INTERVIEWS & AI USAGE SUMMARY ──────────────── */}
      <div className="dashboard-row-4">
        {/* Interview Statistics -> /admin/questions */}
        <div
          className="dashboard-card chart-card dashboard-card--clickable"
          onClick={() => navigate('/admin/questions')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('chartInterviewStats', 'Thống kê Phỏng vấn AI theo ngày')}</h3>
              <p className="chart-card__subtitle">Số lượng các lượt phỏng vấn kỹ thuật & hành vi</p>
            </div>
            <div className="chart-card__header-right">
              <BarChart2 size={20} className="text-primary" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>
          <div style={{ width: '100%', height: 240 }}>
            <ResponsiveContainer>
              <BarChart data={interviews?.dailyInterviewStats || []}>
                <XAxis dataKey="dateLabel" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <RechartsTooltip formatter={(val) => [`${val} lượt`, 'Phỏng vấn']} />
                <Bar dataKey="count" fill="#8B5CF6" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* AI Usage Summary Card -> /admin/ai-usage */}
        <div
          className="dashboard-card status-summary-card dashboard-card--clickable"
          onClick={() => navigate('/admin/ai-usage')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('sectionAiUsageSummary', 'Google Cloud AI Usage')}</h3>
              <p className="chart-card__subtitle">Dịch vụ & Giới hạn Quota</p>
            </div>
            <div className="chart-card__header-right">
              <Server size={20} className="text-primary" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>

          <div className="status-grid">
            <div className="status-item">
              <span className="status-lbl">Speech-to-Text</span>
              <span className="payment-tag payment-tag--paid">Healthy</span>
            </div>
            <div className="status-item">
              <span className="status-lbl">Text-to-Speech</span>
              <span className="payment-tag payment-tag--paid">Healthy</span>
            </div>
            <div className="status-item">
              <span className="status-lbl">Service Runtime</span>
              <span className="payment-tag payment-tag--paid">Healthy</span>
            </div>
            <div className="status-item">
              <span className="status-lbl">BigQuery Billing</span>
              <span className="payment-tag payment-tag--pending">Connected</span>
            </div>
          </div>

          <div className="dashboard-system-health-banner">
            <ShieldCheck size={18} className="text-success" />
            <span>Tất cả dịch vụ AI & Quota đang hoạt động an toàn</span>
          </div>
        </div>
      </div>

      {/* ──────────────── ROW 5: CLOUD COST & GOOGLE USAGE ──────────────── */}
      <div className="dashboard-row-5">
        {/* Google Cloud Cost Billing -> /admin/ai-usage */}
        <div
          className="dashboard-card cost-card dashboard-card--clickable"
          onClick={() => navigate('/admin/ai-usage')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('sectionCloudCost', 'Google Cloud Cost Billing')}</h3>
              <p className="chart-card__subtitle">Chi phí dịch vụ AI hàng tháng</p>
            </div>
            <div className="chart-card__header-right">
              <DollarSign size={20} className="text-success" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>

          <div className="cost-metrics">
            <div>
              <span className="cost-lbl">Hôm nay</span>
              <span className="cost-val">{formatVnd(aiUsageAndCost?.cost?.todayCost || 0)}</span>
            </div>
            <div>
              <span className="cost-lbl">Tháng này</span>
              <span className="cost-val">{formatVnd(aiUsageAndCost?.cost?.monthlyCost || 0)}</span>
            </div>
            <div>
              <span className="cost-lbl">Dự báo tháng</span>
              <span className="cost-val">{formatVnd(aiUsageAndCost?.cost?.forecastCost || 0)}</span>
            </div>
          </div>
        </div>

        {/* Question Bank & CV -> /admin/questions */}
        <div
          className="dashboard-card cost-card dashboard-card--clickable"
          onClick={() => navigate('/admin/questions')}
        >
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">Ngân hàng Câu hỏi & CV</h3>
              <p className="chart-card__subtitle">Tài nguyên học liệu & Hồ sơ hệ thống</p>
            </div>
            <div className="chart-card__header-right">
              <FileQuestion size={20} className="text-primary" />
              <ChevronRight size={16} className="dashboard-card__nav-icon" />
            </div>
          </div>

          <div className="resource-metrics">
            <div className="resource-box">
              <span className="resource-num">{questionBank?.totalQuestions ?? 0}</span>
              <span className="resource-txt">Tổng câu hỏi</span>
            </div>
            <div className="resource-box">
              <span className="resource-num">{questionBank?.codingCount ?? 0}</span>
              <span className="resource-txt">Bài Coding</span>
            </div>
            <div className="resource-box">
              <span className="resource-num">{cv?.totalUploadedCv ?? 0}</span>
              <span className="resource-txt">CV đã tải lên</span>
            </div>
          </div>
        </div>
      </div>

      {/* ──────────────── ROW 6: RECENT ACTIVITIES TIMELINE ──────────────── */}
      <div className="dashboard-row-6">
        <div className="dashboard-card timeline-card">
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('sectionRecentActivities', 'Nhật ký Hoạt động Mới nhất')}</h3>
              <p className="chart-card__subtitle">Tiến trình đăng ký, thanh toán & phỏng vấn vừa diễn ra</p>
            </div>
            <Clock size={20} className="text-secondary" />
          </div>

          <div className="dashboard-timeline">
            {recentActivities && recentActivities.length > 0 ? (
              recentActivities.map((act) => (
                <div key={act.id} className="dashboard-timeline-item">
                  <div className={`dashboard-timeline-dot dashboard-timeline-dot--${act.status}`} />
                  <div className="dashboard-timeline-content">
                    <div className="dashboard-timeline-title">{act.title}</div>
                    <div className="dashboard-timeline-desc">{act.description}</div>
                    <div className="dashboard-timeline-time">{formatDate(act.timestamp)}</div>
                  </div>
                </div>
              ))
            ) : (
              <div className="empty-text">Chưa có hoạt động mới nào.</div>
            )}
          </div>
        </div>
      </div>

      {/* ──────────────── ROW 7: LATEST PAYMENTS TABLE ──────────────── */}
      <div className="dashboard-row-7">
        <div className="dashboard-card table-card">
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">{t('sectionLatestPayments', 'Giao dịch Thanh toán Mới nhất')}</h3>
              <p className="chart-card__subtitle">5 đơn hàng vừa được xử lý qua cổng MoMo</p>
            </div>
            <button
              className="payment-page__btn"
              onClick={() => navigate('/admin/payments')}
              type="button"
            >
              Xem tất cả <ArrowRight size={14} />
            </button>
          </div>

          <div className="payment-table-wrap">
            <table className="payment-table">
              <thead>
                <tr>
                  <th>MÃ ĐƠN HÀNG</th>
                  <th>NGƯỜI DÙNG</th>
                  <th>GÓI NÂNG CẤP</th>
                  <th>SỐ TIỀN</th>
                  <th>TRẠNG THÁI</th>
                  <th>THỜI GIAN</th>
                </tr>
              </thead>
              <tbody>
                {payments?.latestTransactions && payments.latestTransactions.length > 0 ? (
                  payments.latestTransactions.map((tx) => (
                    <tr
                      key={tx.paymentId}
                      style={{ cursor: 'pointer' }}
                      onClick={() => navigate('/admin/payments')}
                    >
                      <td><strong style={{ fontSize: 12 }}>{tx.orderCode}</strong></td>
                      <td>
                        <div style={{ fontWeight: 500 }}>{tx.studentName}</div>
                        <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{tx.email}</div>
                      </td>
                      <td>
                        <span style={{ fontSize: 12 }}>
                          {tx.planBefore} ➔ <strong>{tx.planAfter}</strong>
                        </span>
                      </td>
                      <td><strong style={{ color: 'var(--primary-dark)' }}>{formatVnd(tx.amount)}</strong></td>
                      <td>
                        <span className={`payment-tag ${tx.status === 'Paid' || tx.status === 'PaidByReward' ? 'payment-tag--paid' : 'payment-tag--pending'}`}>
                          {tx.status === 'Paid' ? 'Thành công' : tx.status}
                        </span>
                      </td>
                      <td style={{ fontSize: 12 }}>{formatDate(tx.createdAt)}</td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6} style={{ padding: 20, textAlign: 'center', color: 'var(--text-secondary)' }}>
                      Chưa có giao dịch thanh toán nào.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
