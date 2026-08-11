import React, { useEffect, useState, useCallback, useMemo } from 'react';
import { createPortal } from 'react-dom';
import {
  RefreshCw,
  Search,
  Eye,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  Clock,
  DollarSign,
  TrendingUp,
  CreditCard,
  User,
  ShieldCheck,
  X,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  FileSpreadsheet,
  Activity,
  PieChart as PieIcon,
  RotateCw
} from 'lucide-react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  Tooltip as RechartsTooltip,
  Legend,
} from 'recharts';
import { useTranslation } from 'react-i18next';
import {
  fetchAdminPayments,
  fetchAdminPaymentStatistics,
  fetchAdminPaymentDetail,
  downloadAdminPaymentsExport,
} from '../../../services/AdminPaymentService';
import './PaymentManagementPage.css';

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
    year: 'numeric',
  });
}

function getStatusBadge(status, t) {
  const statusStr = typeof status === 'string' ? status : String(status);
  switch (statusStr.toLowerCase()) {
    case 'paid':
    case 'paidbyreward':
    case '1':
    case '4':
      return { label: t ? t('filterStatusPaid', 'Thành công (Paid)') : 'Thành công', cls: 'payment-tag--paid', icon: CheckCircle2 };
    case 'pending':
    case '0':
      return { label: t ? t('filterStatusPending', 'Chờ xử lý (Pending)') : 'Chờ xử lý', cls: 'payment-tag--pending', icon: Clock };
    case 'failed':
    case '3':
      return { label: t ? t('filterStatusFailed', 'Thất bại (Failed)') : 'Thất bại', cls: 'payment-tag--failed', icon: XCircle };
    case 'expired':
    case '2':
      return { label: t ? t('filterStatusExpired', 'Hết hạn (Expired)') : 'Hết hạn', cls: 'payment-tag--expired', icon: AlertTriangle };
    case 'cancelled':
    case '5':
      return { label: t ? t('filterStatusCancelled', 'Đã hủy (Cancelled)') : 'Đã hủy', cls: 'payment-tag--cancelled', icon: XCircle };
    case 'refunded':
    case '6':
      return { label: t ? t('filterStatusRefunded', 'Hoàn tiền (Refunded)') : 'Hoàn tiền', cls: 'payment-tag--refunded', icon: RotateCw };
    default:
      return { label: statusStr, cls: 'payment-tag--expired', icon: Clock };
  }
}

const PIE_COLORS = ['#4CAF50', '#FF9800', '#F44336', '#9E9E9E', '#9C27B0', '#2196F3'];

/* ────────────────────────── Main Component ────────────────────────── */

export default function PaymentManagementPage() {
  const { t } = useTranslation('admin-dashboard');

  // Data states
  const [payments, setPayments] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [statistics, setStatistics] = useState(null);
  const [loading, setLoading] = useState(true);

  // Filters & Pagination
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [statusFilter, setStatusFilter] = useState('');
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState('newest');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  // Detail Modal state
  const [selectedPaymentId, setSelectedPaymentId] = useState(null);
  const [paymentDetail, setPaymentDetail] = useState(null);
  const [loadingDetail, setLoadingDetail] = useState(false);

  // Auto refresh 30s
  const [autoRefresh] = useState(true);
  const [countdown, setCountdown] = useState(30);

  /* ── Data Loaders ────────────────────────────────────────── */

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const token = localStorage.getItem('token');
      if (!token) throw new Error('Phiên đăng nhập đã hết hạn.');

      const [listRes, statsRes] = await Promise.all([
        fetchAdminPayments(token, {
          page,
          pageSize,
          status: statusFilter,
          search,
          sortBy,
          dateFrom: dateFrom ? new Date(dateFrom).toISOString() : undefined,
          dateTo: dateTo ? new Date(dateTo).toISOString() : undefined,
        }),
        fetchAdminPaymentStatistics(token),
      ]);

      setPayments(listRes.items || []);
      setTotalCount(listRes.totalCount || 0);
      setStatistics(statsRes);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
      setCountdown(30);
    }
  }, [page, pageSize, statusFilter, search, sortBy, dateFrom, dateTo]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Auto refresh timer
  useEffect(() => {
    if (!autoRefresh) return;
    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          loadData();
          return 30;
        }
        return prev - 1;
      });
    }, 1000);
    return () => clearInterval(timer);
  }, [autoRefresh, loadData]);

  // Lock body scroll when modal is open so background doesn't move
  useEffect(() => {
    if (selectedPaymentId) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [selectedPaymentId]);

  /* ── Open Detail Modal ─────────────────────────────────── */

  const handleOpenDetail = async (id) => {
    setSelectedPaymentId(id);
    setLoadingDetail(true);
    try {
      const token = localStorage.getItem('token');
      const detail = await fetchAdminPaymentDetail(token, id);
      setPaymentDetail(detail);
    } catch (err) {
      alert(`Lỗi: ${err.message}`);
    } finally {
      setLoadingDetail(false);
    }
  };

  const handleCloseDetail = () => {
    setSelectedPaymentId(null);
    setPaymentDetail(null);
  };

  /* ── Export CSV/Excel ────────────────────────────────────── */

  const handleExport = async () => {
    try {
      const token = localStorage.getItem('token');
      await downloadAdminPaymentsExport(token, {
        status: statusFilter,
        search,
        sortBy,
        dateFrom,
        dateTo,
      });
    } catch (err) {
      alert(`Lỗi xuất file: ${err.message}`);
    }
  };

  const setPageSize = (value) => {
    setPage(1);
    setPageSize(Number(value));
  };

  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const pageButtons = useMemo(() => {
    const buttons = [];
    if (totalPages <= 7) {
      for (let p = 1; p <= totalPages; p += 1) buttons.push(p);
      return buttons;
    }
    const leftBound = Math.max(2, page - 2);
    const rightBound = Math.min(totalPages - 1, page + 2);
    buttons.push(1);
    if (leftBound > 2) buttons.push('start-ellipsis');
    for (let p = leftBound; p <= rightBound; p += 1) buttons.push(p);
    if (rightBound < totalPages - 1) buttons.push('end-ellipsis');
    buttons.push(totalPages);
    return buttons;
  }, [page, totalPages]);

  return (
    <div className="admin-dashboard-page payment-page">
      {/* Header */}
      <div className="payment-page__header">
        <div className="breadcrumb">
          <span>{t('breadcrumbAdmin', 'Admin')}</span>
          <span className="separator">/</span>
          <span aria-current="page">Payments</span>
        </div>

        <div className="payment-page__title-row">
          <div>
            <h1 className="payment-page__title">{t('paymentTitle', 'MoMo Payment Management')}</h1>
            <p className="payment-page__subtitle">
              {t('paymentSubtitle', 'Theo dõi toàn bộ giao dịch MoMo, kiểm tra trạng thái thanh toán và lịch sử nâng cấp gói Premium.')}
            </p>
          </div>

          <div className="payment-page__actions">
            <div className="payment-page__auto-refresh-badge">
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
              {loading ? '...' : t('btnRefresh', 'Làm mới')}
            </button>

            <button
              className="payment-page__btn payment-page__btn--primary"
              onClick={handleExport}
              type="button"
            >
              <FileSpreadsheet size={15} />
              {t('btnExportExcel', 'Xuất Excel')}
            </button>
          </div>
        </div>
      </div>

      {/* 1. Summary Cards (5 Cards) */}
      <div className="payment-summary-grid">
        <div className="payment-summary-card">
          <div className="payment-summary-card__top">
            <span className="payment-summary-card__icon payment-summary-card__icon--green">
              <DollarSign size={20} />
            </span>
            <span className="payment-tag payment-tag--paid">Today</span>
          </div>
          <div className="payment-summary-card__val">
            {formatVnd(statistics?.todayRevenue)}
          </div>
          <div className="payment-summary-card__lbl">{t('cardTodayRevenue', "Hôm nay (Today's Revenue)")}</div>
        </div>

        <div className="payment-summary-card">
          <div className="payment-summary-card__top">
            <span className="payment-summary-card__icon payment-summary-card__icon--blue">
              <TrendingUp size={20} />
            </span>
            <span className="payment-tag payment-tag--paid">Monthly</span>
          </div>
          <div className="payment-summary-card__val">
            {formatVnd(statistics?.monthlyRevenue)}
          </div>
          <div className="payment-summary-card__lbl">{t('cardMonthlyRevenue', 'Tháng này (Monthly Revenue)')}</div>
        </div>

        <div className="payment-summary-card">
          <div className="payment-summary-card__top">
            <span className="payment-summary-card__icon payment-summary-card__icon--green">
              <CheckCircle2 size={20} />
            </span>
            <span className="payment-tag payment-tag--paid">OK</span>
          </div>
          <div className="payment-summary-card__val">
            {statistics?.successfulPayments ?? 0}
          </div>
          <div className="payment-summary-card__lbl">{t('cardSuccessPayments', 'Thành công (Success)')}</div>
        </div>

        <div className="payment-summary-card">
          <div className="payment-summary-card__top">
            <span className="payment-summary-card__icon payment-summary-card__icon--amber">
              <Clock size={20} />
            </span>
            <span className="payment-tag payment-tag--pending">Pending</span>
          </div>
          <div className="payment-summary-card__val">
            {statistics?.pendingPayments ?? 0}
          </div>
          <div className="payment-summary-card__lbl">{t('cardPendingPayments', 'Chờ xử lý (Pending)')}</div>
        </div>

        <div className="payment-summary-card">
          <div className="payment-summary-card__top">
            <span className="payment-summary-card__icon payment-summary-card__icon--red">
              <XCircle size={20} />
            </span>
            <span className="payment-tag payment-tag--failed">Failed</span>
          </div>
          <div className="payment-summary-card__val">
            {statistics?.failedPayments ?? 0}
          </div>
          <div className="payment-summary-card__lbl">{t('cardFailedPayments', 'Thất bại / Hủy (Failed)')}</div>
        </div>
      </div>

      {/* 2. Charts Grid */}
      {statistics && (
        <div className="payment-charts-grid">
          {/* Revenue Trend Line Chart */}
          <div className="payment-chart-card">
            <div className="payment-chart-card__header">
              <h3 className="payment-chart-card__title">{t('chartRevenueTrend', 'Revenue Trend (14 ngày gần nhất)')}</h3>
              <TrendingUp size={18} className="text-primary" />
            </div>
            <ResponsiveContainer width="100%" height={240}>
              <AreaChart data={statistics.revenueTrend}>
                <defs>
                  <linearGradient id="revenueGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#4CAF50" stopOpacity={0.4} />
                    <stop offset="95%" stopColor="#4CAF50" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v / 1000).toFixed(0)}k`} />
                <RechartsTooltip formatter={(val) => [formatVnd(val), 'Revenue']} />
                <Area type="monotone" dataKey="revenue" stroke="#4CAF50" strokeWidth={3} fillOpacity={1} fill="url(#revenueGrad)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          {/* Status Distribution Pie Chart */}
          <div className="payment-chart-card">
            <div className="payment-chart-card__header">
              <h3 className="payment-chart-card__title">{t('chartPaymentStatus', 'Payment Status')}</h3>
              <PieIcon size={18} className="text-secondary" />
            </div>
            <ResponsiveContainer width="100%" height={240}>
              <PieChart>
                <Pie
                  data={statistics.statusDistribution}
                  dataKey="count"
                  nameKey="status"
                  cx="50%"
                  cy="50%"
                  innerRadius={50}
                  outerRadius={80}
                  paddingAngle={4}
                >
                  {statistics.statusDistribution.map((entry, idx) => (
                    <Cell key={`cell-${idx}`} fill={PIE_COLORS[idx % PIE_COLORS.length]} />
                  ))}
                </Pie>
                <RechartsTooltip formatter={(val, name) => [`${val} orders`, name]} />
                <Legend verticalAlign="bottom" height={36} />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {/* 3. Filter & Search Bar */}
      <div className="payment-filter-bar">
        <div className="payment-filter-bar__inputs">
          <div className="search-wrap" style={{ position: 'relative', width: 240 }}>
            <Search size={16} style={{ position: 'absolute', left: 12, top: 10, color: 'var(--text-secondary)' }} />
            <input
              type="text"
              className="payment-input"
              style={{ paddingLeft: 36, width: '100%' }}
              placeholder={t('searchPaymentPlaceholder', 'Order ID, Email, Người dùng...')}
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <select
            className="payment-select"
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
          >
            <option value="">{t('filterAllStatus', 'Tất cả trạng thái')}</option>
            <option value="Paid">{t('filterStatusPaid', 'Thành công (Paid)')}</option>
            <option value="Pending">{t('filterStatusPending', 'Chờ xử lý (Pending)')}</option>
            <option value="Failed">{t('filterStatusFailed', 'Thất bại (Failed)')}</option>
            <option value="Expired">{t('filterStatusExpired', 'Hết hạn (Expired)')}</option>
            <option value="Cancelled">{t('filterStatusCancelled', 'Đã hủy (Cancelled)')}</option>
            <option value="Refunded">{t('filterStatusRefunded', 'Hoàn tiền (Refunded)')}</option>
          </select>

          <input
            type="date"
            className="payment-input"
            value={dateFrom}
            onChange={(e) => {
              setDateFrom(e.target.value);
              setPage(1);
            }}
          />
          <span style={{ color: 'var(--text-secondary)' }}>-</span>
          <input
            type="date"
            className="payment-input"
            value={dateTo}
            onChange={(e) => {
              setDateTo(e.target.value);
              setPage(1);
            }}
          />

          <select
            className="payment-select"
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value)}
          >
            <option value="newest">{t('sortNewest', 'Mới nhất')}</option>
            <option value="oldest">{t('sortOldest', 'Cũ nhất')}</option>
            <option value="highestamount">{t('sortHighestAmount', 'Giá cao nhất')}</option>
            <option value="lowestamount">{t('sortLowestAmount', 'Giá thấp nhất')}</option>
          </select>
        </div>

        <div style={{ fontSize: 13, color: 'var(--text-secondary)' }}>
          {t('totalTransactions', { total: totalCount })}
        </div>
      </div>

      {/* 4. Table */}
      <div className="payment-table-wrap">
        <table className="payment-table">
          <thead>
            <tr>
              <th>{t('thOrderTransId', 'ORDER ID / TRANS ID')}</th>
              <th>{t('thUser', 'NGƯỜI DÙNG')}</th>
              <th>{t('thPlanUpgrade', 'NÂNG CẤP GÓI')}</th>
              <th>{t('thAmount', 'SỐ TIỀN')}</th>
              <th>{t('thStatus', 'TRẠNG THÁI')}</th>
              <th>{t('thCreatedAt', 'THỜI GIAN TẠO')}</th>
              <th>{t('thPaidAt', 'THANH TOÁN')}</th>
              <th style={{ textAlign: 'right' }}>{t('thActions', 'THAO TÁC')}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <tr key={i}>
                  <td colSpan={8} style={{ padding: 16, textAlign: 'center' }}>
                    <div style={{ height: 20, background: 'var(--surface-3)', borderRadius: 4, animation: 'paymentFadeIn 0.8s infinite alternate' }} />
                  </td>
                </tr>
              ))
            ) : payments.length > 0 ? (
              payments.map((item) => {
                const badge = getStatusBadge(item.status, t);
                const BadgeIcon = badge.icon;
                return (
                  <tr key={item.paymentId}>
                    <td>
                      <div style={{ fontWeight: 600 }}>{item.orderCode}</div>
                      {item.providerTransactionId && (
                        <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>
                          MoMo: {item.providerTransactionId}
                        </div>
                      )}
                    </td>
                    <td>
                      <div style={{ fontWeight: 500 }}>{item.studentName}</div>
                      <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{item.email}</div>
                    </td>
                    <td>
                      <div style={{ fontSize: 12 }}>
                        <span style={{ color: 'var(--text-secondary)' }}>{item.planBefore}</span>
                        <span style={{ margin: '0 6px', color: 'var(--primary)' }}>➔</span>
                        <strong>{item.planAfter}</strong>
                      </div>
                    </td>
                    <td>
                      <strong style={{ color: 'var(--primary-dark)' }}>
                        {formatVnd(item.amount)}
                      </strong>
                    </td>
                    <td>
                      <span className={`payment-tag ${badge.cls}`}>
                        <BadgeIcon size={12} />
                        {badge.label}
                      </span>
                    </td>
                    <td style={{ fontSize: 12 }}>{formatDate(item.createdAt)}</td>
                    <td style={{ fontSize: 12 }}>{formatDate(item.paidAt)}</td>
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'inline-flex', gap: 6, justifyContent: 'flex-end' }}>
                        <button
                          className="payment-btn-icon"
                          title="Xem chi tiết"
                          onClick={() => handleOpenDetail(item.paymentId)}
                          type="button"
                        >
                          <Eye size={15} />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })
            ) : (
              <tr>
                <td colSpan={8} style={{ padding: 40, textAlign: 'center', color: 'var(--text-secondary)' }}>
                  {t('emptyPayments', 'Không tìm thấy giao dịch thanh toán nào phù hợp.')}
                </td>
              </tr>
            )}
          </tbody>
        </table>

        {/* Pagination Bar */}
        <div className="pagination">
          <div className="pagination-info">
            <span>
              Hiển thị {totalCount === 0 ? 0 : (page - 1) * pageSize + 1}-{Math.min(page * pageSize, totalCount)} trên tổng số {totalCount} giao dịch
            </span>
            <div className="page-size-selector">
              <label>Số lượng mỗi trang:</label>
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setPage(1);
                }}
                className="page-size-select"
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
          </div>

          <div className="pagination-buttons">
            <div className="pagination-desktop">
              <button
                className="pagination-btn"
                type="button"
                disabled={page === 1}
                onClick={() => setPage(1)}
                title="Trang đầu"
              >
                <ChevronsLeft size={18} />
              </button>
              <button
                className="pagination-btn"
                type="button"
                disabled={page === 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                title="Trang trước"
              >
                <ChevronLeft size={18} />
              </button>

              {pageButtons.map((button, index) =>
                button === 'start-ellipsis' || button === 'end-ellipsis' ? (
                  <span key={`ellipsis-${index}`} className="pagination-ellipsis">
                    ...
                  </span>
                ) : (
                  <button
                    key={button}
                    className={`pagination-btn ${page === button ? 'active' : ''}`}
                    type="button"
                    onClick={() => setPage(button)}
                  >
                    {button}
                  </button>
                )
              )}

              <button
                className="pagination-btn"
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                title="Trang sau"
              >
                <ChevronRight size={18} />
              </button>
              <button
                className="pagination-btn"
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage(totalPages)}
                title="Trang cuối"
              >
                <ChevronsRight size={18} />
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* 5. Detail Centered Popup Modal via React Portal */}
      {selectedPaymentId && createPortal(
        <div className="payment-modal-backdrop" onClick={handleCloseDetail}>
          <div className="payment-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="payment-modal-header">
              <h3 className="payment-modal-title">
                {t('drawerTitle', { code: paymentDetail?.orderCode || selectedPaymentId })}
              </h3>
              <button
                className="payment-btn-close"
                onClick={handleCloseDetail}
                type="button"
              >
                <X size={20} />
              </button>
            </div>

            <div className="payment-modal-body">
              {loadingDetail ? (
                <div style={{ padding: 40, textAlign: 'center' }}>...</div>
              ) : paymentDetail ? (
                <>
                  {/* User Section */}
                  <div className="payment-drawer__section">
                    <h3 className="payment-drawer__section-title">
                      <User size={16} /> {t('drawerUserInfo', 'Thông tin Người dùng')}
                    </h3>
                    <div className="payment-info-grid">
                      <div><span className="payment-info-lbl">{t('drawerFullName', 'Họ tên')}:</span> <span className="payment-info-val">{paymentDetail.user.fullName}</span></div>
                      <div><span className="payment-info-lbl">{t('drawerEmail', 'Email')}:</span> <span className="payment-info-val">{paymentDetail.user.email}</span></div>
                      <div><span className="payment-info-lbl">{t('drawerUserId', 'User ID')}:</span> <span className="payment-info-val">#{paymentDetail.user.userId}</span></div>
                      <div><span className="payment-info-lbl">{t('drawerRole', 'Vai trò')}:</span> <span className="payment-info-val">{paymentDetail.user.role}</span></div>
                    </div>
                  </div>

                  {/* Subscription Upgrade Section */}
                  <div className="payment-drawer__section">
                    <h3 className="payment-drawer__section-title">
                      <CreditCard size={16} /> {t('drawerSubInfo', 'Gói dịch vụ & Nâng cấp')}
                    </h3>
                    <div className="payment-info-grid">
                      <div><span className="payment-info-lbl">{t('drawerPlanBefore', 'Gói trước đó')}:</span> <span className="payment-info-val">{paymentDetail.subscription.planBefore}</span></div>
                      <div><span className="payment-info-lbl">{t('drawerPlanAfter', 'Gói nâng cấp')}:</span> <span className="payment-info-val"><strong>{paymentDetail.subscription.planAfter}</strong></span></div>
                      <div><span className="payment-info-lbl">{t('drawerBillingCycle', 'Chu kỳ')}:</span> <span className="payment-info-val">{paymentDetail.subscription.billingCycleName}</span></div>
                      <div><span className="payment-info-lbl">{t('drawerAmount', 'Số tiền')}:</span> <span className="payment-info-val">{formatVnd(paymentDetail.amount)}</span></div>
                      <div><span className="payment-info-lbl">{t('drawerStartsAt', 'Bắt đầu')}:</span> <span className="payment-info-val">{formatDate(paymentDetail.subscription.termStartsAt)}</span></div>
                      <div><span className="payment-info-lbl">{t('drawerEndsAt', 'Hết hạn')}:</span> <span className="payment-info-val">{formatDate(paymentDetail.subscription.termEndsAt)}</span></div>
                    </div>
                  </div>

                  {/* Timeline */}
                  <div className="payment-drawer__section">
                    <h3 className="payment-drawer__section-title">
                      <Clock size={16} /> {t('drawerTimeline', 'Tiến trình Giao dịch (Timeline)')}
                    </h3>
                    <div className="payment-timeline">
                      {paymentDetail.timeline.map((step, idx) => (
                        <div key={idx} className={`payment-timeline-item payment-timeline-item--${step.status}`}>
                          <div className="payment-timeline-dot" />
                          <div className="payment-timeline-title">{step.title}</div>
                          <div className="payment-timeline-desc">
                            {formatDate(step.timestamp)} — {step.description}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* MoMo Data */}
                  <div className="payment-drawer__section">
                    <h3 className="payment-drawer__section-title">
                      <ShieldCheck size={16} /> {t('drawerMoMoData', 'Dữ liệu MoMo Verification')}
                    </h3>
                    <div className="payment-info-grid" style={{ marginBottom: 12 }}>
                      <div><span className="payment-info-lbl">Partner Code:</span> <span className="payment-info-val">MOMO</span></div>
                      <div><span className="payment-info-lbl">Trans ID:</span> <span className="payment-info-val">{paymentDetail.moMoDetails.transId || '—'}</span></div>
                    </div>
                    <div style={{ fontSize: 12, fontWeight: 600, marginBottom: 4 }}>Raw Callback JSON:</div>
                    <div className="payment-json-view">{paymentDetail.moMoDetails.rawCallbackJson}</div>
                  </div>
                </>
              ) : null}
            </div>
          </div>
        </div>,
        document.body
      )}
    </div>
  );
}
