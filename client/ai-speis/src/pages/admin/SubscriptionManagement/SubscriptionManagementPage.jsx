import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import notify from '../../../utils/notification';
import subscriptionPlanService, { API_NOT_SUPPORTED_MESSAGE } from '../../../services/SubscriptionPlanService';
import SubscriptionKPICards from './components/SubscriptionKPICards';
import SubscriptionToolbar from './components/SubscriptionToolbar';
import SubscriptionTable from './components/SubscriptionTable';
import SubscriptionPlanModal from './components/SubscriptionPlanModal';
import SubscriptionPlanDrawer from './components/SubscriptionPlanDrawer';
import SubscriptionDeleteDialog from './components/SubscriptionDeleteDialog';
import SubscriptionLoading from './components/SubscriptionLoading';
import SubscriptionEmptyState from './components/SubscriptionEmptyState';
import SubscriptionErrorState from './components/SubscriptionErrorState';
import SubscriptionPagination from './components/SubscriptionPagination';

const PAGE_SIZE = 8;

const defaultFilters = {
  search: '',
  status: 'all',
  billingCycle: 'all',
  currency: 'all',
};

const getPrimaryPrice = (plan) => {
  if (!Array.isArray(plan.prices) || plan.prices.length === 0) {
    return null;
  }

  const activePrices = plan.prices.filter((item) => item.isActive);
  const source = activePrices.length > 0 ? activePrices : plan.prices;

  return [...source].sort((left, right) => Number(left.amount || 0) - Number(right.amount || 0))[0] || null;
};

const parseTime = (value) => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.getTime();
};

export default function SubscriptionManagementPage() {
  const { t } = useTranslation('admin-subscription');

  const [plans, setPlans] = useState([]);
  const [monitoring, setMonitoring] = useState(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');

  const [filters, setFilters] = useState(defaultFilters);
  const [sortBy, setSortBy] = useState('name_asc');
  const [currentPage, setCurrentPage] = useState(1);

  const [modalState, setModalState] = useState({ open: false, mode: 'create', planId: null });
  const [submitting, setSubmitting] = useState(false);

  const [drawerPlanId, setDrawerPlanId] = useState(null);
  const [deletePlanId, setDeletePlanId] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const loadData = async ({ silent = false } = {}) => {
    if (silent) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }

    setError('');
    try {
      const [planData, monitoringData] = await Promise.all([
        subscriptionPlanService.getPlans(),
        subscriptionPlanService.getMonitoring(),
      ]);

      setPlans(Array.isArray(planData) ? planData : []);
      setMonitoring(monitoringData || null);
    } catch (loadError) {
      setError(loadError.message || t('toast.loadError'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  useEffect(() => {
    setCurrentPage(1);
  }, [filters.search, filters.status, filters.billingCycle, filters.currency, sortBy]);

  const normalizedRows = useMemo(() => plans.map((plan) => {
    const primaryPrice = getPrimaryPrice(plan);

    return {
      planId: plan.planId,
      code: plan.code,
      name: plan.name,
      description: plan.description,
      interviewQuota: plan.interviewQuota,
      isActive: Boolean(plan.isActive),
      updatedAt: plan.updatedAt ?? null,
      createdAt: plan.createdAt ?? null,
      priceAmount: primaryPrice?.amount ?? null,
      currency: primaryPrice?.currency ?? null,
      billingCycle: primaryPrice?.billingCycle ?? null,
      prices: plan.prices || [],
      subscribersCount: plan.subscribersCount ?? null,
      revenue: plan.revenue ?? null,
    };
  }), [plans]);

  const filteredRows = useMemo(() => {
    let rows = [...normalizedRows];

    const searchValue = filters.search.trim().toLowerCase();
    if (searchValue) {
      rows = rows.filter((row) => {
        const code = String(row.code || '').toLowerCase();
        const name = String(row.name || '').toLowerCase();
        return code.includes(searchValue) || name.includes(searchValue);
      });
    }

    if (filters.status === 'active') {
      rows = rows.filter((row) => row.isActive);
    }

    if (filters.status === 'inactive') {
      rows = rows.filter((row) => !row.isActive);
    }

    if (filters.billingCycle !== 'all') {
      const selected = Number(filters.billingCycle);
      rows = rows.filter((row) => (row.prices || []).some((price) => Number(price.billingCycle) === selected));
    }

    if (filters.currency !== 'all') {
      rows = rows.filter((row) => (row.prices || []).some((price) => String(price.currency || '').toUpperCase() === filters.currency));
    }

    switch (sortBy) {
      case 'name_desc':
        rows.sort((left, right) => String(right.name || '').localeCompare(String(left.name || '')));
        break;
      case 'price_asc':
        rows.sort((left, right) => Number(left.priceAmount ?? Number.MAX_SAFE_INTEGER) - Number(right.priceAmount ?? Number.MAX_SAFE_INTEGER));
        break;
      case 'price_desc':
        rows.sort((left, right) => Number(right.priceAmount ?? -1) - Number(left.priceAmount ?? -1));
        break;
      case 'updated_asc':
        rows.sort((left, right) => Number(parseTime(left.updatedAt) ?? Number.MAX_SAFE_INTEGER) - Number(parseTime(right.updatedAt) ?? Number.MAX_SAFE_INTEGER));
        break;
      case 'updated_desc':
        rows.sort((left, right) => Number(parseTime(right.updatedAt) ?? -1) - Number(parseTime(left.updatedAt) ?? -1));
        break;
      default:
        rows.sort((left, right) => String(left.name || '').localeCompare(String(right.name || '')));
        break;
    }

    return rows;
  }, [normalizedRows, filters, sortBy]);

  const totalPages = Math.max(1, Math.ceil(filteredRows.length / PAGE_SIZE));

  const paginatedRows = useMemo(() => {
    const safeCurrentPage = Math.min(currentPage, totalPages);
    const start = (safeCurrentPage - 1) * PAGE_SIZE;
    return filteredRows.slice(start, start + PAGE_SIZE);
  }, [currentPage, filteredRows, totalPages]);

  const showColumns = useMemo(() => ({
    description: normalizedRows.some((row) => Boolean(row.description)),
    price: normalizedRows.some((row) => row.priceAmount !== null && row.priceAmount !== undefined),
    currency: normalizedRows.some((row) => Boolean(row.currency)),
    billingCycle: normalizedRows.some((row) => row.billingCycle !== null && row.billingCycle !== undefined),
    subscribers: normalizedRows.some((row) => row.subscribersCount !== null && row.subscribersCount !== undefined),
    revenue: normalizedRows.some((row) => row.revenue !== null && row.revenue !== undefined),
    updatedAt: normalizedRows.some((row) => Boolean(row.updatedAt)),
  }), [normalizedRows]);

  const selectedPlan = useMemo(() => plans.find((plan) => plan.planId === drawerPlanId) || null, [drawerPlanId, plans]);
  const deletePlan = useMemo(() => plans.find((plan) => plan.planId === deletePlanId) || null, [deletePlanId, plans]);
  const modalPlan = useMemo(() => plans.find((plan) => plan.planId === modalState.planId) || null, [modalState.planId, plans]);

  const handleCreate = () => {
    setModalState({ open: true, mode: 'create', planId: null });
  };

  const handleEdit = (planId) => {
    setModalState({ open: true, mode: 'edit', planId });
  };

  const handleSubmitModal = async ({ form, prices }) => {
    setSubmitting(true);

    try {
      if (modalState.mode === 'create') {
        const created = await subscriptionPlanService.createPlan(form);
        const createdPlanId = created?.planId;

        if (createdPlanId && Array.isArray(prices) && prices.length > 0) {
          for (const price of prices) {
            await subscriptionPlanService.createPrice(createdPlanId, price);
          }
        }
      } else if (modalPlan) {
        await subscriptionPlanService.updatePlan(modalPlan.planId, form);

        for (const price of prices) {
          if (price.priceId) {
            await subscriptionPlanService.updatePrice(price.priceId, price);
          } else {
            await subscriptionPlanService.createPrice(modalPlan.planId, price);
          }
        }
      }

      notify.success(t('toast.saveSuccess'));
      setModalState({ open: false, mode: 'create', planId: null });
      await loadData({ silent: true });
    } catch (submitError) {
      notify.error(submitError.message === API_NOT_SUPPORTED_MESSAGE ? t('api.notSupported') : (submitError.message || t('toast.saveError')));
    } finally {
      setSubmitting(false);
    }
  };

  const handleToggleStatus = async (planId, isActive) => {
    try {
      await subscriptionPlanService.updatePlanStatus(planId, isActive);
      notify.success(t('toast.statusSuccess'));
      await loadData({ silent: true });
    } catch (statusError) {
      notify.error(statusError.message === API_NOT_SUPPORTED_MESSAGE ? t('api.notSupported') : (statusError.message || t('toast.statusError')));
    }
  };

  const handleDuplicate = async () => {
    try {
      await subscriptionPlanService.duplicatePlan();
    } catch (duplicateError) {
      notify.error(duplicateError.message === API_NOT_SUPPORTED_MESSAGE ? t('api.notSupported') : (duplicateError.message || t('api.notSupported')));
    }
  };

  const handleDelete = async () => {
    if (!deletePlan) {
      return;
    }

    setDeleting(true);
    try {
      await subscriptionPlanService.deletePlan(deletePlan.planId);
    } catch (deleteError) {
      notify.error(deleteError.message === API_NOT_SUPPORTED_MESSAGE ? t('api.notSupported') : (deleteError.message || t('api.notSupported')));
    } finally {
      setDeleting(false);
      setDeletePlanId(null);
    }
  };

  if (loading) {
    return <SubscriptionLoading />;
  }

  if (error && plans.length === 0) {
    return <SubscriptionErrorState message={error} onRetry={() => loadData()} />;
  }

  return (
    <div className="space-y-6 animate-[fadeIn_0.35s_ease]">
      <style>{`
        @keyframes fadeIn {
          from { opacity: 0; transform: translateY(8px); }
          to { opacity: 1; transform: translateY(0); }
        }
        @keyframes cardEntrance {
          from { opacity: 0; transform: translateY(10px); }
          to { opacity: 1; transform: translateY(0); }
        }
      `}</style>

      <header className="space-y-2">
        <div className="text-xs text-text-secondary">
          <span>{t('breadcrumbAdmin')}</span>
          <span className="mx-1">/</span>
          <span aria-current="page">{t('breadcrumb')}</span>
        </div>
        <h1 className="text-2xl font-bold text-text-primary md:text-3xl">{t('title')}</h1>
        <p className="text-sm text-text-secondary">{t('subtitle')}</p>
      </header>

      <SubscriptionKPICards plans={plans} monitoring={monitoring} />

      <SubscriptionToolbar
        filters={filters}
        sortBy={sortBy}
        onFilterChange={(key, value) => setFilters((prev) => ({ ...prev, [key]: value }))}
        onSortChange={setSortBy}
        onCreate={handleCreate}
        onRefresh={() => loadData({ silent: true })}
        isRefreshing={refreshing}
      />

      {error && plans.length > 0 && (
        <div className="rounded-xl border border-warning/50 bg-warning-light px-4 py-3 text-sm text-text-primary">
          {error}
        </div>
      )}

      {filteredRows.length === 0 ? (
        <SubscriptionEmptyState onCreate={handleCreate} />
      ) : (
        <>
          <SubscriptionTable
            rows={paginatedRows}
            showColumns={showColumns}
            onView={setDrawerPlanId}
            onEdit={handleEdit}
            onDuplicate={handleDuplicate}
            onToggleStatus={handleToggleStatus}
            onDelete={setDeletePlanId}
          />

          <SubscriptionPagination
            currentPage={Math.min(currentPage, totalPages)}
            totalPages={totalPages}
            onPageChange={setCurrentPage}
          />
        </>
      )}

      <SubscriptionPlanModal
        open={modalState.open}
        mode={modalState.mode}
        submitting={submitting}
        initialPlan={modalPlan}
        existingPlans={plans}
        onClose={() => setModalState({ open: false, mode: 'create', planId: null })}
        onSubmit={handleSubmitModal}
      />

      <SubscriptionPlanDrawer
        open={Boolean(drawerPlanId)}
        plan={selectedPlan}
        onClose={() => setDrawerPlanId(null)}
      />

      <SubscriptionDeleteDialog
        open={Boolean(deletePlanId)}
        plan={deletePlan}
        isDeleting={deleting}
        onCancel={() => setDeletePlanId(null)}
        onConfirm={handleDelete}
      />
    </div>
  );
}
