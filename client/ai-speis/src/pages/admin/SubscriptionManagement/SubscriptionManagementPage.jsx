import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  Plus,
  RefreshCw,
  Save,
  Search,
  CheckCircle2,
  Edit3,
  Users,
  Package,
  X,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  AlertCircle,
  Zap,
  Calendar
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { API_BASE_URL } from '../../../config/api';
import notify from '../../../utils/notification';
import '../../../styles/admin/SubscriptionManagementPage.css';

const emptyPlan = {
  planId: null,
  code: '',
  name: '',
  description: '',
  interviewQuota: 15,
  quotaResetDays: 30,
  isFree: false,
  displayOrder: 10,
  isActive: true,
  isPopular: false,
  monthlyPriceId: null,
  monthlyAmount: '',
  yearlyPriceId: null,
  yearlyAmount: '',
};

const authHeaders = () => ({
  Authorization: `Bearer ${localStorage.getItem('token')}`,
  'Content-Type': 'application/json',
});

async function api(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: { ...authHeaders(), ...options.headers },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    const error = new Error(body.message || body.Message || body.detail || 'Lỗi khi gửi yêu cầu.');
    error.code = body.code;
    error.field = body.field;
    throw error;
  }

  return response.status === 204 ? null : response.json();
}

const normalizePlans = (payload) => {
  if (Array.isArray(payload)) return payload;
  if (Array.isArray(payload?.items)) return payload.items;
  if (Array.isArray(payload?.data)) return payload.data;
  return [];
};

export const normalizeMonitoring = (payload) => {
  const source = payload && typeof payload === 'object' ? payload : {};
  const planSubscriberCounts = Array.isArray(source.planSubscriberCounts)
    ? Object.fromEntries(source.planSubscriberCounts.map((item) => [String(item.planId), Number(item.subscriberCount || 0)]))
    : {};
  return {
    activePremiumUsers: Number(source.activePremiumUsers || source.premiumUsers || 0),
    totalActivePlans: Number(source.totalActivePlans || 0),
    planSubscriberCounts,
  };
};

const normalizePlanForm = (plan) => {
  const prices = Array.isArray(plan?.prices) ? plan.prices : [];
  const monthlyPrice = prices.find((p) => Number(p.billingCycle) === 1);
  const yearlyPrice = prices.find((p) => Number(p.billingCycle) === 2);

  return {
    ...emptyPlan,
    ...plan,
    monthlyPriceId: monthlyPrice?.priceId || null,
    monthlyAmount: monthlyPrice ? monthlyPrice.amount : '',
    yearlyPriceId: yearlyPrice?.priceId || null,
    yearlyAmount: yearlyPrice ? yearlyPrice.amount : '',
  };
};

const formatNumber = (value, language = 'vi') => {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return '0';
  return new Intl.NumberFormat(language === 'vi' ? 'vi-VN' : 'en-US').format(numeric);
};

const formatCurrency = (value, currency, language = 'vi') => {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return '0 ₫';
  const safeCurrency = currency || 'VND';
  try {
    return new Intl.NumberFormat(language === 'vi' ? 'vi-VN' : 'en-US', {
      style: 'currency',
      currency: safeCurrency,
      maximumFractionDigits: 0,
    }).format(numeric);
  } catch {
    return `${formatNumber(numeric, language)} ${safeCurrency}`;
  }
};

const getPlanIdDisplay = (plan, index) => {
  const rawId = plan?.planId ?? plan?.id ?? index + 1;
  return `#PLN-${String(rawId).padStart(3, '0')}`;
};

export const getSubscriberCount = (plan, monitoring) => {
  if (!plan) return 0;
  const candidates = [
    plan?.subscriberCount,
    plan?.subscribers,
    plan?.activeSubscribers,
    plan?.subscriptionCount,
    plan?.subscriberTotal,
  ];
  const value = candidates.find((candidate) => Number.isFinite(Number(candidate)));
  if (value != null) return Number(value);

  return Number(monitoring?.planSubscriberCounts?.[String(plan.planId)] || 0);
};

export default function SubscriptionManagementPage() {
  const { t, i18n } = useTranslation('admin-subscription');
  const language = i18n.language === 'vi' ? 'vi' : 'en';

  const [plans, setPlans] = useState([]);
  const [monitoring, setMonitoring] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  // Horizontal Search & Filters via useState
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [cycleFilter, setCycleFilter] = useState('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // Edit Modal Overlay State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState('create');
  const [selectedPlanId, setSelectedPlanId] = useState(null);
  const [planForm, setPlanForm] = useState(emptyPlan);
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(async () => {
    setBusy(true);
    setError('');
    try {
      const [plansPayload, monitoringPayload] = await Promise.all([
        api('/api/admin/subscription-plans'),
        api('/api/admin/subscription-monitoring/summary').catch(() => null),
      ]);
      setPlans(normalizePlans(plansPayload));
      if (monitoringPayload) setMonitoring(normalizeMonitoring(monitoringPayload));
    } catch (loadError) {
      const message = loadError.message || t('errorFallback', 'Không thể lấy dữ liệu gói dịch vụ.');
      setError(message);
      notify.error(message);
    } finally {
      setBusy(false);
    }
  }, [t]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    const handleEscape = (event) => {
      if (event.key !== 'Escape') return;
      setIsModalOpen(false);
    };
    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  }, []);

  // Filter Reset Handler
  const handleResetFilters = () => {
    setSearchQuery('');
    setStatusFilter('all');
    setCycleFilter('all');
    setCurrentPage(1);
  };

  // Filtered plans list
  const filteredPlans = useMemo(() => {
    return plans.filter((plan) => {
      // Filter by Status
      if (statusFilter === 'active' && !plan.isActive) return false;
      if (statusFilter === 'inactive' && plan.isActive) return false;

      // Filter by Billing Cycle
      const prices = plan.prices || [];
      if (cycleFilter === 'monthly' && !prices.some((p) => Number(p.billingCycle) === 1) && !plan.isFree) return false;
      if (cycleFilter === 'yearly' && !prices.some((p) => Number(p.billingCycle) === 2)) return false;
      if (cycleFilter === 'free' && !plan.isFree && prices.some((p) => Number(p.amount) > 0)) return false;

      // Search Query
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim();
        const codeMatch = String(plan.code || '').toLowerCase().includes(query);
        const nameMatch = String(plan.name || '').toLowerCase().includes(query);
        const descMatch = String(plan.description || '').toLowerCase().includes(query);
        if (!codeMatch && !nameMatch && !descMatch) return false;
      }
      return true;
    });
  }, [plans, statusFilter, cycleFilter, searchQuery]);

  const totalPages = Math.max(1, Math.ceil(filteredPlans.length / pageSize));

  useEffect(() => {
    if (currentPage > totalPages) setCurrentPage(totalPages);
  }, [currentPage, totalPages]);

  const pagedPlans = useMemo(() => {
    const startIndex = (currentPage - 1) * pageSize;
    return filteredPlans.slice(startIndex, startIndex + pageSize);
  }, [currentPage, filteredPlans, pageSize]);

  const pageButtons = useMemo(() => {
    const buttons = [];
    if (totalPages <= 7) {
      for (let page = 1; page <= totalPages; page += 1) {
        buttons.push(page);
      }
      return buttons;
    }

    const leftBound = Math.max(2, currentPage - 2);
    const rightBound = Math.min(totalPages - 1, currentPage + 2);

    buttons.push(1);
    if (leftBound > 2) buttons.push('start-ellipsis');
    for (let page = leftBound; page <= rightBound; page += 1) {
      buttons.push(page);
    }
    if (rightBound < totalPages - 1) buttons.push('end-ellipsis');
    buttons.push(totalPages);
    return buttons;
  }, [currentPage, totalPages]);

  // Non-revenue KPI Metrics Cards
  const metrics = useMemo(() => {
    const activePlans = plans.filter((plan) => plan?.isActive).length;
    const premiumUsersCount = monitoring?.activePremiumUsers ?? 0;

    const mostPopularPlan = plans.reduce((best, plan) => {
      const currentCount = getSubscriberCount(plan, monitoring);
      const bestCount = getSubscriberCount(best, monitoring);
      return currentCount > bestCount ? plan : best;
    }, null);

    return [
      {
        icon: Package,
        color: 'text-blue-600',
        bg: 'bg-blue-50',
        label: t('kpi.activePlans', 'Gói dịch vụ'),
        value: `${formatNumber(activePlans, language)} / ${plans.length} gói`,
        subText: `${activePlans} gói đang hoạt động`,
      },
      {
        icon: CheckCircle2,
        color: 'text-emerald-600',
        bg: 'bg-emerald-50',
        label: 'Trạng thái hệ thống',
        value: `${activePlans} Active`,
        subText: `${plans.length - activePlans} gói đang tạm ẩn`,
      },
      {
        icon: Users,
        color: 'text-purple-600',
        bg: 'bg-purple-50',
        label: t('kpi.premiumUsers', 'Người dùng Premium'),
        value: formatNumber(premiumUsersCount, language),
        subText: 'Thành viên đang sử dụng',
      },
      {
        icon: Zap,
        color: 'text-amber-600',
        bg: 'bg-amber-50',
        label: t('kpi.mostPopular', 'Gói phổ biến nhất'),
        value: mostPopularPlan?.name || 'Chưa có',
        subText: mostPopularPlan ? `${formatNumber(getSubscriberCount(mostPopularPlan, monitoring), language)} thành viên` : 'Đang cập nhật',
      },
    ];
  }, [language, monitoring, plans, t]);

  const openCreateModal = () => {
    setModalMode('create');
    setSelectedPlanId(null);
    setPlanForm(emptyPlan);
    setIsModalOpen(true);
  };

  const openEditModal = (plan) => {
    setModalMode('edit');
    setSelectedPlanId(plan?.planId ?? null);
    setPlanForm(normalizePlanForm(plan));
    setIsModalOpen(true);
  };

  const updatePlanForm = (key, value) => {
    setPlanForm((current) => ({ ...current, [key]: value }));
  };

  const buildPlanPayload = () => ({
    code: planForm.code,
    name: planForm.name,
    description: planForm.description,
    interviewQuota: Number(planForm.interviewQuota),
    quotaResetDays: planForm.isFree
      ? null
      : (planForm.quotaResetDays === '' || planForm.quotaResetDays == null ? null : Number(planForm.quotaResetDays)),
    isFree: Boolean(planForm.isFree),
    displayOrder: Number(planForm.displayOrder || 10),
    isPopular: Boolean(planForm.isPopular),
    isActive: Boolean(planForm.isActive),
  });

  const handleSavePlan = async () => {
    if (!planForm.code || !planForm.name) {
      notify.error(t('validation.requiredCodeName', 'Vui lòng nhập tên gói và mã gói.'));
      return;
    }

    setIsSaving(true);
    try {
      let targetPlanId = selectedPlanId || planForm.planId;

      if (modalMode === 'edit' && targetPlanId) {
        await api(`/api/admin/subscription-plans/${targetPlanId}`, {
          method: 'PUT',
          body: JSON.stringify(buildPlanPayload()),
        });
      } else {
        const created = await api('/api/admin/subscription-plans', {
          method: 'POST',
          body: JSON.stringify(buildPlanPayload()),
        });
        targetPlanId = created?.planId || created?.PlanId;
      }

      // Save Monthly Price if provided
      if (!planForm.isFree && targetPlanId) {
        if (planForm.monthlyAmount !== '' && planForm.monthlyAmount != null) {
          const monthlyPayload = {
            billingCycle: 1, // Monthly
            billingCycleCount: 1,
            amount: Number(planForm.monthlyAmount),
            currency: 'VND',
            effectiveFrom: new Date().toISOString(),
          };

          if (planForm.monthlyPriceId) {
            await api(`/api/admin/subscription-plans/prices/${planForm.monthlyPriceId}`, {
              method: 'PUT',
              body: JSON.stringify(monthlyPayload),
            });
          } else {
            await api(`/api/admin/subscription-plans/${targetPlanId}/prices`, {
              method: 'POST',
              body: JSON.stringify(monthlyPayload),
            });
          }
        }

        // Save Yearly Price if provided
        if (planForm.yearlyAmount !== '' && planForm.yearlyAmount != null) {
          const yearlyPayload = {
            billingCycle: 2, // Yearly
            billingCycleCount: 1,
            amount: Number(planForm.yearlyAmount),
            currency: 'VND',
            effectiveFrom: new Date().toISOString(),
          };

          if (planForm.yearlyPriceId) {
            await api(`/api/admin/subscription-plans/prices/${planForm.yearlyPriceId}`, {
              method: 'PUT',
              body: JSON.stringify(yearlyPayload),
            });
          } else {
            await api(`/api/admin/subscription-plans/${targetPlanId}/prices`, {
              method: 'POST',
              body: JSON.stringify(yearlyPayload),
            });
          }
        }
      }

      notify.success(modalMode === 'edit' ? 'Đã cập nhật gói và bảng giá thành công.' : 'Đã tạo mới gói dịch vụ thành công.');
      setIsModalOpen(false);
      await load();
    } catch (saveError) {
      notify.error(saveError.message || t('toast.saveError', 'Lỗi khi lưu thông tin gói.'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleToggleStatus = async (plan) => {
    const newStatus = !plan.isActive;
    setPlans((prev) => prev.map((p) => p.planId === plan.planId ? { ...p, isActive: newStatus } : p));
    try {
      await api(`/api/admin/subscription-plans/${plan.planId}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ isActive: newStatus }),
      });
      notify.success(newStatus ? 'Đã kích hoạt gói dịch vụ.' : 'Đã tạm ẩn gói dịch vụ.');
    } catch (toggleError) {
      setPlans((prev) => prev.map((p) => p.planId === plan.planId ? { ...p, isActive: plan.isActive } : p));
      notify.error(toggleError.message || t('toast.statusError', 'Không thể đổi trạng thái gói.'));
    }
  };

  const showingStart = filteredPlans.length ? ((currentPage - 1) * pageSize) + 1 : 0;
  const showingEnd = filteredPlans.length ? Math.min(currentPage * pageSize, filteredPlans.length) : 0;

  return (
    <div className="subscription-management-page w-full animate-[fadeIn_0.3s_ease-out]">
      {/* Breadcrumb Navigation */}
      <div className="breadcrumb">
        <span>{t('breadcrumbAdmin', 'Admin')}</span>
        <span className="separator">/</span>
        <span aria-current="page">{t('pageTitle', 'Quản lý Gói Đăng ký')}</span>
      </div>

      {/* Page Header */}
      <div className="page-header">
        <div className="header-top">
          <div className="title-section">
            <h1 className="page-title">{t('pageTitle', 'Quản lý Gói Đăng ký')}</h1>
            <p className="page-description">
              {t('pageSubtitle', 'Cấu hình bảng giá và định mức phỏng vấn cho các gói dịch vụ.')}
            </p>
          </div>

          <div className="page-actions">
            <button
              type="button"
              className="btn-secondary"
              onClick={load}
              disabled={busy}
              title="Tải lại dữ liệu"
            >
              <RefreshCw size={16} className={busy ? 'animate-spin' : ''} />
              <span>Tải lại</span>
            </button>

            <button
              type="button"
              className="btn-primary"
              onClick={openCreateModal}
            >
              <Plus size={16} />
              <span>{t('createNewPlan', 'Tạo gói mới')}</span>
            </button>
          </div>
        </div>
      </div>

      {/* KPI Cards Row */}
      <div className="grid grid-cols-1 gap-4 mb-6 sm:grid-cols-2 lg:grid-cols-4">
        {metrics.map((metric, idx) => {
          const IconComp = metric.icon;
          return (
            <div
              key={idx}
              className="flex items-center gap-4 rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] p-4 shadow-sm transition-all hover:shadow-md"
            >
              <div className={`flex h-12 w-12 items-center justify-center rounded-2xl ${metric.bg} ${metric.color}`}>
                <IconComp size={24} />
              </div>
              <div>
                <p className="text-xs font-bold uppercase tracking-wider text-[var(--text-secondary)]">
                  {metric.label}
                </p>
                <p className="mt-0.5 text-xl font-extrabold text-[var(--text-primary)]">
                  {metric.value}
                </p>
                <p className="text-[11px] font-medium text-[var(--text-secondary)]">
                  {metric.subText}
                </p>
              </div>
            </div>
          );
        })}
      </div>

      {/* Horizontal Search & Filters */}
      <section className="filter-card">
        <div className="filter-row">
          {/* Search Input Group */}
          <div className="filter-group search-group">
            <Search size={18} className="text-[var(--text-secondary)]" />
            <input
              type="text"
              className="search-input"
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setCurrentPage(1);
              }}
              placeholder={t('filter.searchPlaceholder', 'Tìm kiếm theo nội dung, mã hoặc tên gói...')}
            />
          </div>

          {/* Filter 1: Status Dropdown */}
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="filter-select"
          >
            <option value="all">{t('filter.allStatus', 'Tất cả trạng thái')}</option>
            <option value="active">{t('statusLabels.active', 'Đang hoạt động')}</option>
            <option value="inactive">{t('statusLabels.inactive', 'Tạm ẩn')}</option>
          </select>

          {/* Filter 2: Billing Cycle Dropdown */}
          <select
            value={cycleFilter}
            onChange={(e) => {
              setCycleFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="filter-select"
          >
            <option value="all">Tất cả chu kỳ</option>
            <option value="monthly">Hàng tháng</option>
            <option value="yearly">Hàng năm</option>
            <option value="free">Miễn phí</option>
          </select>

          {/* Reset Filters Button */}
          <button
            type="button"
            onClick={handleResetFilters}
            className="btn-clear"
          >
            Xóa bộ lọc
          </button>
        </div>
      </section>

      {/* Main Data Table */}
      {busy && !plans.length ? (
        <div className="rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] p-12 text-center shadow-sm">
          <RefreshCw size={32} className="mx-auto animate-spin text-[var(--primary)]" />
          <p className="mt-3 text-sm font-medium text-[var(--text-secondary)]">Đang tải danh sách gói dịch vụ...</p>
        </div>
      ) : error && !plans.length ? (
        <div className="rounded-2xl border border-red-200 bg-red-50/50 p-8 text-center shadow-sm">
          <AlertCircle size={36} className="mx-auto text-red-500" />
          <h3 className="mt-2 text-base font-bold text-red-700">Không thể kết nối dữ liệu</h3>
          <p className="mt-1 text-xs text-red-600">{error}</p>
          <button
            type="button"
            onClick={load}
            className="mt-4 inline-flex items-center gap-2 rounded-xl bg-red-600 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-red-700"
          >
            <RefreshCw size={14} /> Thử lại
          </button>
        </div>
      ) : filteredPlans.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] py-16 px-4 text-center shadow-sm">
          <Package size={40} className="text-[var(--text-secondary)]" />
          <h3 className="mt-4 text-base font-bold text-[var(--text-primary)]">
            {searchQuery || statusFilter !== 'all' || cycleFilter !== 'all'
              ? 'Không tìm thấy gói thỏa mãn bộ lọc'
              : 'Chưa có gói dịch vụ nào'}
          </h3>
          <p className="mt-1 text-xs text-[var(--text-secondary)] max-w-sm">
            {searchQuery || statusFilter !== 'all' || cycleFilter !== 'all'
              ? 'Thử thay đổi từ khóa tìm kiếm hoặc bấm Xóa bộ lọc.'
              : 'Bấm nút Tạo gói mới để khởi tạo gói dịch vụ đầu tiên.'}
          </p>
          {searchQuery || statusFilter !== 'all' || cycleFilter !== 'all' ? (
            <button
              onClick={handleResetFilters}
              className="mt-4 rounded-xl border border-[var(--border)] bg-[var(--surface-1)] px-4 py-2 text-xs font-bold text-[var(--text-primary)] hover:bg-[var(--surface-3)]"
            >
              Xóa bộ lọc
            </button>
          ) : (
            <button
              type="button"
              onClick={openCreateModal}
              className="mt-4 rounded-xl bg-[var(--primary)] px-5 py-2.5 text-xs font-bold text-white shadow-md hover:bg-[var(--primary-dark)]"
            >
              {t('createNewPlan', 'Tạo gói mới')}
            </button>
          )}
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[800px] border-collapse text-left text-xs">
              <thead>
                <tr className="border-b border-[var(--border)] bg-[var(--surface-3)] text-[11px] font-bold uppercase tracking-wider text-[var(--text-secondary)]">
                  <th className="px-5 py-4">{t('table.planId', 'MÃ GÓI')}</th>
                  <th className="px-5 py-4">{t('table.planInfo', 'THÔNG TIN GÓI')}</th>
                  <th className="px-5 py-4">{t('table.priceCycle', 'GIÁ & CHU KỲ')}</th>
                  <th className="px-5 py-4">{t('table.quota', 'ĐỊNH MỨC QUOTA')}</th>
                  <th className="px-5 py-4">{t('table.subscribers', 'NGƯỜI DÙNG')}</th>
                  <th className="px-5 py-4 text-center">{t('table.status', 'TRẠNG THÁI')}</th>
                  <th className="px-5 py-4 text-center">{t('table.actions', 'THAO TÁC')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border)]">
                {pagedPlans.map((plan, index) => {
                  const subscriberCount = getSubscriberCount(plan, monitoring);
                  const isActive = Boolean(plan.isActive);
                  const prices = plan.prices || [];

                  return (
                    <tr
                      key={plan.planId || `${plan.code}-${index}`}
                      className={`group transition-colors hover:bg-[var(--surface-1)] ${!isActive ? 'bg-[var(--surface-3)]/30 opacity-75' : ''}`}
                    >
                      {/* Code Tag */}
                      <td className="px-5 py-4 font-mono font-bold text-[var(--primary)]">
                        <span className="rounded-lg bg-[var(--primary-xlight)] border border-[var(--primary-light)] px-2.5 py-1 text-[11px]">
                          {getPlanIdDisplay(plan, index)}
                        </span>
                      </td>

                      {/* Info & Badges */}
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-2">
                          <span className="font-bold text-[var(--text-primary)] text-sm">{plan.name || '-'}</span>
                        </div>
                        <div className="mt-1 flex items-center gap-2">
                          <span className="font-mono text-[10px] uppercase font-bold text-[var(--text-secondary)] bg-[var(--surface-3)] px-1.5 py-0.5 rounded">
                            {plan.code}
                          </span>
                          {plan.description && (
                            <span className="truncate max-w-[240px] text-[11px] text-[var(--text-secondary)]">
                              {plan.description}
                            </span>
                          )}
                        </div>
                      </td>

                      {/* Display ALL Pricing Options from SubscriptionPrices table */}
                      <td className="px-5 py-4">
                        {plan.isFree || !prices.length ? (
                          <div>
                            <p className="font-extrabold text-[var(--text-primary)] text-sm">Miễn phí</p>
                            <p className="text-[11px] font-medium text-[var(--text-secondary)] mt-0.5">Bản dùng thử</p>
                          </div>
                        ) : (
                          <div className="space-y-1">
                            {prices.map((price, pIdx) => (
                              <div key={price.priceId || pIdx} className="flex items-center gap-1.5">
                                <span className="font-extrabold text-[var(--text-primary)] text-sm">
                                  {formatCurrency(price.amount, price.currency, language)}
                                </span>
                                <span className="text-[11px] font-semibold text-blue-600 bg-blue-50 px-1.5 py-0.5 rounded border border-blue-100">
                                  {Number(price.billingCycle) === 2
                                    ? '/ năm'
                                    : Number(price.billingCycle) === 3
                                    ? '/ 3 tháng'
                                    : '/ tháng'}
                                </span>
                              </div>
                            ))}
                          </div>
                        )}
                      </td>

                      {/* Quota */}
                      <td className="px-5 py-4">
                        <div className="font-bold text-[var(--text-primary)]">
                          <span className={plan.isFree ? 'text-gray-600' : 'text-emerald-600'}>
                            {formatNumber(plan.interviewQuota, language)} lượt
                          </span>
                        </div>
                        <p className="text-[10px] text-[var(--text-secondary)] mt-0.5">
                          {plan.quotaResetDays ? `Reset mỗi ${plan.quotaResetDays} ngày` : 'Quota cố định'}
                        </p>
                      </td>

                      {/* Subscribers Count */}
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-1.5 font-bold text-[var(--text-primary)]">
                          <Users size={14} className="text-[var(--primary)]" />
                          <span>{formatNumber(subscriberCount, language)}</span>
                          <span className="text-[10px] font-normal text-[var(--text-secondary)]">thành viên</span>
                        </div>
                      </td>

                      {/* Status Toggle Badge */}
                      <td className="px-5 py-4 text-center">
                        <button
                          type="button"
                          onClick={() => handleToggleStatus(plan)}
                          className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[11px] font-extrabold transition-all ${
                            isActive
                              ? 'bg-emerald-100 text-emerald-800 hover:bg-emerald-200'
                              : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
                          }`}
                        >
                          <span className={`h-2 w-2 rounded-full ${isActive ? 'bg-emerald-500' : 'bg-gray-400'}`} />
                          {isActive ? t('statusLabels.active', 'Đang hoạt động') : t('statusLabels.inactive', 'Tạm ẩn')}
                        </button>
                      </td>

                      {/* ONLY 1 EDIT ACTION BUTTON */}
                      <td className="px-5 py-4 text-center">
                        <button
                          type="button"
                          onClick={() => openEditModal(plan)}
                          className="inline-flex items-center gap-1.5 rounded-xl border border-[var(--border)] bg-[var(--surface-1)] px-3 py-1.5 font-bold text-xs text-[var(--primary)] transition-all hover:bg-[var(--primary-xlight)] hover:border-[var(--primary-light)]"
                        >
                          <Edit3 size={14} />
                          <span>Chỉnh sửa</span>
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Table Pagination Bar */}
          <div className="pagination">
            <div className="pagination-info">
              <span>
                Hiển thị {showingStart}-{showingEnd} trên tổng số {filteredPlans.length} gói
              </span>
              <div className="page-size-selector">
                <label>Số lượng mỗi trang:</label>
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setCurrentPage(1);
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
                  type="button"
                  disabled={currentPage === 1}
                  onClick={() => setCurrentPage(1)}
                  className="pagination-btn"
                  title="Trang đầu"
                >
                  <ChevronsLeft size={18} />
                </button>

                <button
                  type="button"
                  disabled={currentPage === 1}
                  onClick={() => setCurrentPage((prev) => Math.max(1, prev - 1))}
                  className="pagination-btn"
                  title="Trang trước"
                >
                  <ChevronLeft size={18} />
                </button>

                {pageButtons.map((button, pIdx) => (
                  button === 'start-ellipsis' || button === 'end-ellipsis' ? (
                    <span key={`ellipsis-${pIdx}`} className="pagination-ellipsis">...</span>
                  ) : (
                    <button
                      key={button}
                      type="button"
                      onClick={() => setCurrentPage(button)}
                      className={`pagination-btn ${currentPage === button ? 'active' : ''}`}
                    >
                      {button}
                    </button>
                  )
                ))}

                <button
                  type="button"
                  disabled={currentPage >= totalPages}
                  onClick={() => setCurrentPage((prev) => Math.min(totalPages, prev + 1))}
                  className="pagination-btn"
                  title="Trang sau"
                >
                  <ChevronRight size={18} />
                </button>

                <button
                  type="button"
                  disabled={currentPage >= totalPages}
                  onClick={() => setCurrentPage(totalPages)}
                  className="pagination-btn"
                  title="Trang cuối"
                >
                  <ChevronsRight size={18} />
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Edit Modal Overlay with Dimmed Backdrop rendered via React Portal */}
      {isModalOpen && createPortal(
        <div
          className="admin-modal-overlay"
          onClick={() => setIsModalOpen(false)}
        >
          <div
            className="admin-modal-container"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div className="flex items-center justify-between border-b border-[var(--border)] px-6 py-4 bg-[var(--surface-3)]">
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--primary)] text-white font-bold">
                  {modalMode === 'edit' ? <Edit3 size={20} /> : <Plus size={20} />}
                </div>
                <div>
                  <h2 className="text-lg font-extrabold text-[var(--text-primary)]">
                    {modalMode === 'edit' ? t('editPlan', 'Chỉnh sửa gói dịch vụ') : t('createNewPlan', 'Tạo gói dịch vụ mới')}
                  </h2>
                  <p className="text-xs text-[var(--text-secondary)]">Cấu hình thông tin và bảng giá dịch vụ</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsModalOpen(false)}
                className="rounded-full p-2 text-gray-400 hover:bg-[var(--surface-1)] hover:text-gray-600 transition-colors"
              >
                <X size={20} />
              </button>
            </div>

            {/* Modal Form Content */}
            <div className="flex-1 overflow-y-auto p-6 space-y-6 text-xs">
              {/* Basic Details Section */}
              <section className="space-y-4">
                <h3 className="font-bold text-sm text-[var(--primary)] flex items-center gap-1.5 border-b border-[var(--border)] pb-2">
                  <Package size={16} /> Thông tin gói
                </h3>

                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div>
                    <label className="mb-1 block font-bold text-[var(--text-primary)]">
                      Tên gói <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={planForm.name}
                      onChange={(e) => updatePlanForm('name', e.target.value)}
                      placeholder="Ví dụ: Premium"
                      className="w-full h-10 rounded-xl border border-[var(--border)] bg-[var(--surface-1)] px-3 text-xs font-semibold text-[var(--text-primary)] focus:border-[var(--primary)] focus:outline-none"
                    />
                  </div>

                  <div>
                    <label className="mb-1 block font-bold text-[var(--text-primary)]">
                      Mã gói (Code) <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={planForm.code}
                      onChange={(e) => updatePlanForm('code', e.target.value)}
                      placeholder="Ví dụ: PREMIUM"
                      className="w-full h-10 rounded-xl border border-[var(--border)] bg-[var(--surface-1)] px-3 font-mono text-xs font-bold text-[var(--primary)] focus:border-[var(--primary)] focus:outline-none uppercase"
                    />
                  </div>

                  <div className="sm:col-span-2">
                    <label className="mb-1 block font-bold text-[var(--text-primary)]">Mô tả chi tiết</label>
                    <textarea
                      value={planForm.description}
                      onChange={(e) => updatePlanForm('description', e.target.value)}
                      rows={2}
                      placeholder="Mô tả ngắn gọn về quyền lợi gói..."
                      className="w-full rounded-xl border border-[var(--border)] bg-[var(--surface-1)] p-3 text-xs font-medium text-[var(--text-primary)] focus:border-[var(--primary)] focus:outline-none"
                    />
                  </div>
                </div>
              </section>

              {/* Quota Section */}
              <section className="space-y-4">
                <h3 className="font-bold text-sm text-[var(--primary)] flex items-center gap-1.5 border-b border-[var(--border)] pb-2">
                  <Zap size={16} /> Định mức Phỏng vấn (Quota)
                </h3>

                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div>
                    <label className="mb-1 block font-bold text-[var(--text-primary)]">Định mức Phỏng vấn (Lượt)</label>
                    <input
                      type="number"
                      value={planForm.interviewQuota}
                      onChange={(e) => updatePlanForm('interviewQuota', e.target.value)}
                      className="w-full h-10 rounded-xl border border-[var(--border)] bg-[var(--surface-1)] px-3 font-bold text-[var(--text-primary)] focus:border-[var(--primary)] focus:outline-none"
                    />
                  </div>

                  <div>
                    <label className="mb-1 block font-bold text-[var(--text-primary)]">Chu kỳ Reset Quota (Ngày)</label>
                    <input
                      type="number"
                      value={planForm.quotaResetDays ?? ''}
                      onChange={(e) => updatePlanForm('quotaResetDays', e.target.value)}
                      placeholder="30"
                      className="w-full h-10 rounded-xl border border-[var(--border)] bg-[var(--surface-1)] px-3 font-bold text-[var(--text-primary)] focus:border-[var(--primary)] focus:outline-none"
                    />
                  </div>
                </div>
              </section>

              {/* Individual Pricing Table Section */}
              <section className="space-y-4">
                <h3 className="font-bold text-sm text-[var(--primary)] flex items-center gap-1.5 border-b border-[var(--border)] pb-2">
                  <Calendar size={16} /> Giá dịch vụ từng chu kỳ (SubscriptionPrices)
                </h3>

                {planForm.isFree ? (
                  <div className="rounded-xl bg-gray-50 border border-gray-200 p-4 text-xs font-semibold text-gray-600 text-center">
                    Gói cơ bản Miễn phí (0 VNĐ)
                  </div>
                ) : (
                  <div className="space-y-4">
                    {/* Price 1: Monthly */}
                    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface-1)] p-4 shadow-sm">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-2">
                          <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-100 text-blue-700 font-bold text-xs">
                            1M
                          </span>
                          <span className="font-bold text-sm text-[var(--text-primary)]">
                            Gói Hàng tháng (Monthly)
                          </span>
                        </div>
                        <span className="text-[11px] font-semibold text-blue-600 bg-blue-50 px-2 py-0.5 rounded border border-blue-100">
                          Chu kỳ 30 ngày
                        </span>
                      </div>
                      <div>
                        <label className="mb-1 block font-bold text-[var(--text-primary)] text-xs">
                          Số tiền (VNĐ)
                        </label>
                        <input
                          type="number"
                          value={planForm.monthlyAmount}
                          onChange={(e) => updatePlanForm('monthlyAmount', e.target.value)}
                          placeholder="Ví dụ: 60000"
                          className="w-full h-10 rounded-xl border border-[var(--border)] bg-[var(--surface-2)] px-3 font-extrabold text-emerald-600 focus:border-[var(--primary)] focus:outline-none text-sm"
                        />
                      </div>
                    </div>

                    {/* Price 2: Yearly */}
                    <div className="rounded-xl border border-[var(--border)] bg-[var(--surface-1)] p-4 shadow-sm">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-2">
                          <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-purple-100 text-purple-700 font-bold text-xs">
                            1Y
                          </span>
                          <span className="font-bold text-sm text-[var(--text-primary)]">
                            Gói Hàng năm (Yearly)
                          </span>
                        </div>
                        <span className="text-[11px] font-semibold text-purple-600 bg-purple-50 px-2 py-0.5 rounded border border-purple-100">
                          Chu kỳ 365 ngày
                        </span>
                      </div>
                      <div>
                        <label className="mb-1 block font-bold text-[var(--text-primary)] text-xs">
                          Số tiền (VNĐ)
                        </label>
                        <input
                          type="number"
                          value={planForm.yearlyAmount}
                          onChange={(e) => updatePlanForm('yearlyAmount', e.target.value)}
                          placeholder="Ví dụ: 590000"
                          className="w-full h-10 rounded-xl border border-[var(--border)] bg-[var(--surface-2)] px-3 font-extrabold text-purple-600 focus:border-[var(--primary)] focus:outline-none text-sm"
                        />
                      </div>
                    </div>
                  </div>
                )}
              </section>
            </div>

            {/* Modal Footer Controls */}
            <div className="flex items-center justify-end gap-3 border-t border-[var(--border)] bg-[var(--surface-3)] px-6 py-4">
              <button
                type="button"
                onClick={() => setIsModalOpen(false)}
                className="btn-secondary"
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={isSaving}
                onClick={handleSavePlan}
                className="btn-primary"
              >
                {isSaving ? <RefreshCw size={16} className="animate-spin" /> : <Save size={16} />}
                <span>{modalMode === 'edit' ? 'Lưu thay đổi' : 'Tạo gói mới'}</span>
              </button>
            </div>
          </div>
        </div>,
        document.body
      )}
    </div>
  );
}
