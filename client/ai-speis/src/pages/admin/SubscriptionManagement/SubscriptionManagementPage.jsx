import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { API_BASE_URL } from '../../../config/api';
import notify from '../../../utils/notification';

const PAGE_SIZE = 3;

let featureClientKeySeed = 0;

const createFeatureClientKey = () => `feature-${featureClientKeySeed += 1}`;

const createEmptyFeatureDraft = (displayOrder = 1) => ({
  clientKey: createFeatureClientKey(),
  planFeatureId: null,
  featureCode: '',
  limitValue: '',
  displayOrder,
  isEnabled: true,
});

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
  aiTier: 'ADVANCED',
  advancedAnalyticsEnabled: false,
  currency: 'VND',
  amount: '',
  billingCycle: 1,
  billingCycleCount: 1,
  effectiveFrom: '',
  effectiveTo: '',
  features: [createEmptyFeatureDraft()],
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
    const error = new Error(body.message || body.Message || body.detail || 'Request failed.');
    error.code = body.code;
    error.field = body.field;
    error.conflictPriceId = body.conflictPriceId;
    error.conflictBillingCycle = body.conflictBillingCycle;
    error.conflictEffectiveFrom = body.conflictEffectiveFrom;
    error.conflictEffectiveTo = body.conflictEffectiveTo;
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

const normalizeMonitoring = (payload) => {
  const source = payload && typeof payload === 'object' ? payload : {};
  return {
    activePremiumUsers: Number(source.activePremiumUsers || source.premiumUsers || 0),
    totalActivePlans: Number(source.totalActivePlans || 0),
    conversionRate: Number(source.conversionRate || 0),
    quota: {
      usedQuota: Number(source?.quota?.usedQuota || 0),
      totalQuota: Number(source?.quota?.totalQuota || 0),
    },
    payments: {
      paidOrders: Number(source?.payments?.paidOrders || 0),
      revenueVnd: Number(source?.payments?.revenueVnd || 0),
      monthlyRevenueVnd: Number(source?.payments?.monthlyRevenueVnd || source?.payments?.mrr || 0),
      annualRevenueVnd: Number(source?.payments?.annualRevenueVnd || source?.payments?.revenueVnd || 0),
    },
  };
};

const normalizePlanForm = (plan) => {
  const primaryPrice = (plan?.prices || [])[0] || {};
  const features = Array.isArray(plan?.features) && plan.features.length > 0
    ? plan.features.map((feature, index) => ({
      clientKey: feature.planFeatureId ? `feature-${feature.planFeatureId}` : createFeatureClientKey(),
      planFeatureId: feature.planFeatureId ?? null,
      featureCode: feature.featureCode || '',
      limitValue: feature.limitValue ?? '',
      displayOrder: feature.displayOrder ?? index + 1,
      isEnabled: Boolean(feature.isEnabled),
    }))
    : [createEmptyFeatureDraft()];

  return {
    ...emptyPlan,
    ...plan,
    currency: primaryPrice.currency || plan?.currency || 'VND',
    amount: primaryPrice.amount ?? plan?.amount ?? '',
    billingCycle: primaryPrice.billingCycle ?? plan?.billingCycle ?? 1,
    billingCycleCount: primaryPrice.billingCycleCount ?? plan?.billingCycleCount ?? 1,
    effectiveFrom: primaryPrice.effectiveFrom || plan?.effectiveFrom || '',
    effectiveTo: primaryPrice.effectiveTo || plan?.effectiveTo || '',
    aiTier: plan?.aiTier || plan?.tier || 'ADVANCED',
    advancedAnalyticsEnabled: Boolean(plan?.advancedAnalyticsEnabled),
    isPopular: Boolean(plan?.isPopular),
    features,
  };
};

const formatNumber = (value, language) => {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return '-';
  return new Intl.NumberFormat(language === 'vi' ? 'vi-VN' : 'en-US').format(numeric);
};

const formatCurrency = (value, currency, language) => {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return '-';
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

const formatDateTime = (value, language) => {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return new Intl.DateTimeFormat(language === 'vi' ? 'vi-VN' : 'en-US', {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
  }).format(date);
};

const toInputDateTime = (value) => {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const offset = date.getTimezoneOffset();
  const normalized = new Date(date.getTime() - (offset * 60000));
  return normalized.toISOString().slice(0, 16);
};

const toIsoDateTime = (value) => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toISOString();
};

const getBillingCycleKey = (billingCycle) => {
  if (Number(billingCycle) === 2) return 'yearly';
  if (Number(billingCycle) === 3) return 'quarterly';
  return 'monthly';
};

const getPlanIdDisplay = (plan, index) => {
  const rawId = plan?.planId ?? plan?.id ?? index + 1;
  return `#PLN-${String(rawId).padStart(3, '0')}`;
};

const getPrimaryPrice = (plan) => (Array.isArray(plan?.prices) && plan.prices.length > 0 ? plan.prices[0] : null);

const isSameInstant = (left, right) => {
  const leftIso = toIsoDateTime(left);
  const rightIso = right ? new Date(right).toISOString() : null;
  return leftIso === rightIso;
};

const getSubscriberCount = (plan) => {
  const candidates = [
    plan?.subscriberCount,
    plan?.subscribers,
    plan?.activeSubscribers,
    plan?.subscriptionCount,
    plan?.subscriberTotal,
  ];
  const value = candidates.find((candidate) => Number.isFinite(Number(candidate)));
  return value == null ? null : Number(value);
};

const getPlanRevenue = (plan) => {
  const candidates = [plan?.revenueVnd, plan?.revenue, plan?.monthlyRevenueVnd, plan?.revenueMtd];
  const value = candidates.find((candidate) => Number.isFinite(Number(candidate)));
  return value == null ? null : Number(value);
};

const getHistoryRecords = (plan) => {
  const source = plan?.recentSubscriptions || plan?.purchaseHistory || plan?.latestPurchases || plan?.history;
  return Array.isArray(source) ? source : [];
};

const getHistoricalPurchaseCount = (plan) => {
  const candidates = [plan?.historicalPurchaseCount, plan?.purchaseCount, plan?.historyCount, getSubscriberCount(plan)];
  const value = candidates.find((candidate) => Number.isFinite(Number(candidate)));
  return value == null ? 0 : Number(value);
};

const getTierKey = (plan) => {
  const tier = String(plan?.aiTier || plan?.tier || '').toLowerCase();
  if (tier.includes('enterprise')) return 'enterprise';
  if (tier.includes('advanced') || tier.includes('premium') || tier.includes('gpt-4')) return 'advanced';
  if (tier.includes('standard')) return 'standard';
  return 'unknown';
};

const getInitials = (name) => {
  const parts = String(name || '').trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '--';
  return parts.slice(0, 2).map((part) => part[0].toUpperCase()).join('');
};

const formatRelativePurchase = (value, t, language) => {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  const diffMs = date.getTime() - Date.now();
  const diffHours = Math.round(diffMs / 3600000);
  const absHours = Math.abs(diffHours);
  if (absHours < 24) {
    const hourText = language === 'vi'
      ? `${absHours} giờ`
      : `${absHours} hour${absHours === 1 ? '' : 's'}`;
    return t('drawer.purchasedAgo', { time: hourText });
  }
  const diffDays = Math.round(diffMs / 86400000);
  const absDays = Math.abs(diffDays);
  const dayText = language === 'vi'
    ? `${absDays} ngày`
    : `${absDays} day${absDays === 1 ? '' : 's'}`;
  return t('drawer.purchasedAgo', { time: dayText });
};

function MaterialIcon({ children, className = '', filled = false }) {
  return (
    <span
      className={`material-symbols-outlined ${className}`.trim()}
      style={filled ? { fontVariationSettings: "'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24" } : undefined}
      aria-hidden="true"
    >
      {children}
    </span>
  );
}

function SubscriptionManagementPage() {
  const { t, i18n } = useTranslation('admin-subscription');
  const language = i18n.language === 'vi' ? 'vi' : 'en';
  const [plans, setPlans] = useState([]);
  const [monitoring, setMonitoring] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [isDeleteOpen, setIsDeleteOpen] = useState(false);
  const [selectedPlanId, setSelectedPlanId] = useState(null);
  const [modalMode, setModalMode] = useState('create');
  const [planForm, setPlanForm] = useState(emptyPlan);
  const [isSaving, setIsSaving] = useState(false);
  const [priceErrors, setPriceErrors] = useState({});
  const priceSectionRef = useRef(null);
  const effectiveFromRef = useRef(null);
  const effectiveToRef = useRef(null);

  const selectedPlan = useMemo(
    () => plans.find((plan) => String(plan.planId) === String(selectedPlanId)) || null,
    [plans, selectedPlanId],
  );

  const load = useCallback(async () => {
    setBusy(true);
    setError('');
    try {
      const [plansPayload, monitoringPayload] = await Promise.all([
        api('/api/admin/subscription-plans'),
        api('/api/admin/subscription-monitoring/summary'),
      ]);
      setPlans(normalizePlans(plansPayload));
      setMonitoring(normalizeMonitoring(monitoringPayload));
    } catch (loadError) {
      const message = loadError.message || t('errorFallback');
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
      setIsDeleteOpen(false);
      setIsDrawerOpen(false);
    };

    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  }, []);

  const metrics = useMemo(() => {
    const activePlans = plans.filter((plan) => plan?.isActive).length;
    const createdThisMonth = plans.filter((plan) => {
      if (!plan?.createdAt) return false;
      const date = new Date(plan.createdAt);
      const now = new Date();
      return date.getMonth() === now.getMonth() && date.getFullYear() === now.getFullYear();
    }).length;
    const mostPopularPlan = plans.reduce((best, plan) => {
      const currentCount = getSubscriberCount(plan) ?? -1;
      const bestCount = getSubscriberCount(best) ?? -1;
      return currentCount > bestCount ? plan : best;
    }, null);

    return [
      {
        icon: 'inventory_2',
        label: t('kpi.activePlans'),
        value: `${formatNumber(monitoring?.totalActivePlans || activePlans, language)} ${t('kpi.plansSuffix')}`,
        badge: createdThisMonth > 0 ? t('kpi.newThisMonth', { count: createdThisMonth }) : null,
      },
      {
        icon: 'group',
        label: t('kpi.premiumUsers'),
        value: formatNumber(monitoring?.activePremiumUsers, language),
      },
      {
        icon: 'account_balance_wallet',
        label: t('kpi.monthlyRevenue'),
        value: formatCurrency(monitoring?.payments?.monthlyRevenueVnd, 'VND', language),
      },
      {
        icon: 'payments',
        label: t('kpi.annualRevenue'),
        value: formatCurrency(monitoring?.payments?.annualRevenueVnd, 'VND', language),
      },
      {
        icon: 'star',
        label: t('kpi.mostPopular'),
        value: mostPopularPlan?.name || t('fields.notAvailable'),
        hint: mostPopularPlan ? `${formatNumber(getSubscriberCount(mostPopularPlan), language)} ${t('kpi.subscribersSuffix')}` : null,
      },
      {
        icon: 'trending_up',
        label: t('kpi.conversionRate'),
        value: `${formatNumber(monitoring?.conversionRate, language)}%`,
        progress: Math.max(0, Math.min(100, Number(monitoring?.conversionRate || 0))),
      },
    ];
  }, [language, monitoring, plans, t]);

  const pagedPlans = useMemo(() => {
    const startIndex = (currentPage - 1) * PAGE_SIZE;
    return plans.slice(startIndex, startIndex + PAGE_SIZE);
  }, [currentPage, plans]);

  const totalPages = Math.max(1, Math.ceil(plans.length / PAGE_SIZE));

  useEffect(() => {
    if (currentPage > totalPages) setCurrentPage(totalPages);
  }, [currentPage, totalPages]);

  const openCreateModal = () => {
    setModalMode('create');
    setSelectedPlanId(null);
    setPlanForm({
      ...emptyPlan,
      effectiveFrom: toInputDateTime(new Date().toISOString()),
    });
    setPriceErrors({});
    setIsModalOpen(true);
  };

  const openEditModal = (plan) => {
    setModalMode('edit');
    setSelectedPlanId(plan?.planId ?? null);
    setPlanForm({
      ...normalizePlanForm(plan),
      effectiveFrom: toInputDateTime(normalizePlanForm(plan).effectiveFrom),
      effectiveTo: toInputDateTime(normalizePlanForm(plan).effectiveTo),
    });
    setPriceErrors({});
    setIsModalOpen(true);
  };

  const openDuplicateModal = (plan) => {
    const duplicated = normalizePlanForm(plan);
    setModalMode('create');
    setSelectedPlanId(null);
    setPlanForm({
      ...duplicated,
      planId: null,
      code: duplicated.code ? `${duplicated.code}_COPY` : '',
      name: duplicated.name ? `${duplicated.name} Copy` : '',
      effectiveFrom: toInputDateTime(new Date().toISOString()),
      effectiveTo: '',
    });
    setPriceErrors({});
    setIsModalOpen(true);
    notify.success(t('duplicatePlanSuccess'));
  };

  const openDrawer = (planId) => {
    setSelectedPlanId(planId);
    setIsDrawerOpen(true);
  };

  const openDeleteDialog = (planId) => {
    setSelectedPlanId(planId);
    setIsDeleteOpen(true);
  };

  const closeAllOverlays = () => {
    setIsModalOpen(false);
    setIsDrawerOpen(false);
    setIsDeleteOpen(false);
    setPriceErrors({});
  };

  const updatePlanForm = (key, value) => {
    if (key === 'effectiveFrom' || key === 'effectiveTo' || key === 'billingCycle' || key === 'billingCycleCount' || key === 'currency') {
      setPriceErrors((current) => {
        if (!Object.keys(current).length) return current;
        return { ...current, message: '', [key]: '' };
      });
    }
    setPlanForm((current) => ({ ...current, [key]: value }));
  };

  const focusPriceField = useCallback((field) => {
    priceSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    if (field === 'EffectiveTo' || field === 'effectiveTo') {
      effectiveToRef.current?.focus();
      return;
    }
    effectiveFromRef.current?.focus();
  }, []);

  const buildConflictMessage = useCallback((submitError) => {
    if (!submitError?.conflictEffectiveFrom) {
      return submitError?.message || t('toast.saveError');
    }

    const cycleRaw = String(submitError.conflictBillingCycle || '').toLowerCase();
    const cycleLabel = cycleRaw === 'yearly'
      ? t('fields.billingYearly')
      : t('fields.billingMonthly');

    return t('validation.priceOverlapWithConflict', {
      cycle: cycleLabel.toLowerCase(),
      effectiveFrom: formatDateTime(submitError.conflictEffectiveFrom, language),
    });
  }, [language, t]);

  const updateFeature = (index, field, value) => {
    setPlanForm((current) => ({
      ...current,
      features: current.features.map((feature, featureIndex) => (
        featureIndex === index
          ? { ...feature, [field]: value }
          : feature
      )),
    }));
  };

  const removeFeature = (index) => {
    setPlanForm((current) => ({
      ...current,
      features: current.features.length === 1
        ? [createEmptyFeatureDraft()]
        : current.features.filter((_, featureIndex) => featureIndex !== index),
    }));
  };

  const addFeatureField = () => {
    setPlanForm((current) => ({ ...current, features: [...current.features, createEmptyFeatureDraft(current.features.length + 1)] }));
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
    displayOrder: Number(planForm.displayOrder),
    aiTier: String(planForm.aiTier || 'ADVANCED').toUpperCase(),
    advancedAnalyticsEnabled: Boolean(planForm.advancedAnalyticsEnabled),
    isPopular: Boolean(planForm.isPopular),
    isActive: Boolean(planForm.isActive),
    features: planForm.features
      .map((feature, index) => ({
        planFeatureId: feature.planFeatureId || null,
        featureCode: String(feature.featureCode || '').trim().toUpperCase(),
        limitValue: feature.limitValue === '' || feature.limitValue == null ? null : Number(feature.limitValue),
        displayOrder: feature.displayOrder === '' || feature.displayOrder == null ? index + 1 : Number(feature.displayOrder),
        isEnabled: Boolean(feature.isEnabled),
      }))
      .filter((feature) => feature.featureCode),
  });

  const buildPricePayload = () => ({
    billingCycle: Number(planForm.billingCycle),
    billingCycleCount: Number(planForm.billingCycleCount || 1),
    amount: Number(planForm.amount || 0),
    currency: String(planForm.currency || 'VND').toUpperCase(),
    effectiveFrom: toIsoDateTime(planForm.effectiveFrom) || new Date().toISOString(),
    effectiveTo: toIsoDateTime(planForm.effectiveTo),
  });

  const shouldPersistPrice = useMemo(() => {
    if (modalMode === 'create') {
      return planForm.amount !== '' || Boolean(planForm.effectiveTo) || Number(planForm.billingCycle) !== 1 || Number(planForm.billingCycleCount || 1) !== 1 || String(planForm.currency || 'VND').toUpperCase() !== 'VND';
    }

    const existingPrice = getPrimaryPrice(selectedPlan);
    if (!existingPrice) {
      return planForm.amount !== '';
    }

    return Number(planForm.billingCycle) !== Number(existingPrice.billingCycle)
      || Number(planForm.billingCycleCount || 1) !== Number(existingPrice.billingCycleCount || 1)
      || Number(planForm.amount || 0) !== Number(existingPrice.amount || 0)
      || String(planForm.currency || 'VND').toUpperCase() !== String(existingPrice.currency || 'VND').toUpperCase()
      || !isSameInstant(planForm.effectiveFrom, existingPrice.effectiveFrom)
      || !isSameInstant(planForm.effectiveTo, existingPrice.effectiveTo);
  }, [modalMode, planForm.amount, planForm.billingCycle, planForm.billingCycleCount, planForm.currency, planForm.effectiveFrom, planForm.effectiveTo, selectedPlan]);

  const handleSavePlan = async () => {
    setIsSaving(true);
    try {
      if (modalMode === 'edit' && planForm.planId) {
        await api(`/api/admin/subscription-plans/${planForm.planId}`, {
          method: 'PUT',
          body: JSON.stringify(buildPlanPayload()),
        });

        const existingPrice = getPrimaryPrice(selectedPlan);
        if (existingPrice?.priceId && shouldPersistPrice) {
          await api(`/api/admin/subscription-plans/prices/${existingPrice.priceId}`, {
            method: 'PUT',
            body: JSON.stringify(buildPricePayload()),
          });
        } else if (!existingPrice?.priceId && shouldPersistPrice) {
          await api(`/api/admin/subscription-plans/${planForm.planId}/prices`, {
            method: 'POST',
            body: JSON.stringify(buildPricePayload()),
          });
        }
        notify.success(t('savePlanSuccess'));
      } else {
        const created = await api('/api/admin/subscription-plans', {
          method: 'POST',
          body: JSON.stringify(buildPlanPayload()),
        });

        const createdPlanId = created?.planId || created?.id;
        if (createdPlanId && planForm.amount !== '') {
          await api(`/api/admin/subscription-plans/${createdPlanId}/prices`, {
            method: 'POST',
            body: JSON.stringify(buildPricePayload()),
          });
        }
        notify.success(t('createPlanSuccess'));
      }
      closeAllOverlays();
      await load();
    } catch (saveError) {
      if (saveError?.code === 'INVALID_PRICE') {
        const nextErrors = {
          message: buildConflictMessage(saveError),
          effectiveFrom: saveError.field === 'EffectiveFrom' || saveError.field === 'effectiveFrom'
            ? buildConflictMessage(saveError)
            : '',
          effectiveTo: saveError.field === 'EffectiveTo' || saveError.field === 'effectiveTo'
            ? saveError.message
            : '',
        };
        setPriceErrors(nextErrors);
        focusPriceField(saveError.field);
        notify.warning(nextErrors.message);
        return;
      }
      notify.error(saveError.message || t('toast.saveError'));
    } finally {
      setIsSaving(false);
    }
  };

  const handleToggleStatus = async (plan) => {
    try {
      await api(`/api/admin/subscription-plans/${plan.planId}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ isActive: !plan.isActive }),
      });
      notify.success(t('toast.statusSuccess'));
      await load();
    } catch (toggleError) {
      notify.error(toggleError.message || t('toast.statusError'));
    }
  };

  const handleDeletePlan = async () => {
    if (!selectedPlanId) return;
    try {
      await api(`/api/admin/subscription-plans/${selectedPlanId}`, { method: 'DELETE' });
      setIsDeleteOpen(false);
      setIsDrawerOpen(false);
      await load();
    } catch (deleteError) {
      notify.warning(deleteError.message || t('api.notSupported'));
      setIsDeleteOpen(false);
    }
  };

  const handleExport = () => {
    if (!plans.length) {
      notify.info(t('exportEmpty'));
      return;
    }

    const csvRows = [
      [
        t('table.planId'),
        t('table.planCode'),
        t('table.planName'),
        t('table.priceCycle'),
        t('table.aiTier'),
        t('table.status'),
        t('table.subscribers'),
      ],
      ...plans.map((plan, index) => {
        const price = getPrimaryPrice(plan);
        return [
          getPlanIdDisplay(plan, index),
          plan.code || '-',
          plan.name || '-',
          formatCurrency(price?.amount, price?.currency || 'VND', language),
          t(`tiers.${getTierKey(plan)}`),
          plan.isActive ? t('status.active') : t('status.inactive'),
          formatNumber(getSubscriberCount(plan), language),
        ];
      }),
    ];

    const csv = csvRows.map((row) => row.map((cell) => `"${String(cell ?? '').replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'subscription-plans.csv';
    link.click();
    URL.revokeObjectURL(url);
    notify.success(t('exportSuccess'));
  };

  const planHistory = getHistoryRecords(selectedPlan);
  const selectedPrice = getPrimaryPrice(selectedPlan);
  const showingStart = plans.length ? ((currentPage - 1) * PAGE_SIZE) + 1 : 0;
  const showingEnd = plans.length ? Math.min(currentPage * PAGE_SIZE, plans.length) : 0;

  return (
    <div className="mx-auto w-full max-w-[1200px] space-y-8">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-[32px] font-bold leading-[40px] tracking-[-0.02em] text-[var(--text-primary)]">
            {t('pageTitle')}
          </h2>
          <p className="mt-1 text-base leading-6 text-[var(--text-secondary)]">
            {t('pageSubtitle')}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="flex items-center gap-2 rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] px-4 py-2 font-medium transition-colors hover:bg-[var(--surface-1)]"
          >
            <MaterialIcon className="text-[20px] text-[var(--text-secondary)]">filter_list</MaterialIcon>
            <span>{t('filter')}</span>
          </button>
          <button
            type="button"
            onClick={handleExport}
            className="flex items-center gap-2 rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] px-4 py-2 font-medium transition-colors hover:bg-[var(--surface-1)]"
          >
            <MaterialIcon className="text-[20px] text-[var(--text-secondary)]">ios_share</MaterialIcon>
            <span>{t('export')}</span>
          </button>
          <button
            type="button"
            onClick={openCreateModal}
            className="flex items-center gap-2 rounded-2xl bg-[var(--primary-dark)] px-6 py-2.5 font-semibold text-white shadow-[0_8px_24px_rgba(31,45,61,0.10)] transition-colors hover:bg-[var(--primary)]"
          >
            <MaterialIcon className="text-[20px]">add</MaterialIcon>
            <span>{t('createNewPlan')}</span>
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3 xl:grid-cols-6">
        {metrics.map((metric) => (
          <div
            key={metric.label}
            className="group rounded-2xl border border-[var(--primary-light)] bg-[var(--surface-2)] p-4 shadow-[0_2px_12px_rgba(31,45,61,0.05)] transition-all hover:shadow-[0_8px_24px_rgba(31,45,61,0.10)]"
          >
            <div className="mb-2 flex items-center justify-between gap-2">
              <div className="flex h-10 w-10 items-center justify-center rounded-full bg-[var(--primary-xlight)] text-[var(--primary-dark)] transition-transform group-hover:scale-110">
                <MaterialIcon>{metric.icon}</MaterialIcon>
              </div>
              {metric.badge ? (
                <span className="rounded bg-[var(--primary-xlight)] px-2 py-0.5 text-xs font-bold text-[var(--primary-dark)]">
                  {metric.badge}
                </span>
              ) : null}
            </div>
            <p className="text-xs font-bold uppercase tracking-tight text-[var(--text-secondary)]">{metric.label}</p>
            <p className="mt-1 text-2xl font-bold text-[var(--text-primary)]">{metric.value}</p>
            {metric.hint ? <p className="mt-1 text-[10px] text-[var(--text-secondary)]">{metric.hint}</p> : null}
            {metric.progress != null ? (
              <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-[var(--surface-3)]">
                <div className="h-full bg-[var(--primary-dark)]" style={{ width: `${metric.progress}%` }} />
              </div>
            ) : null}
          </div>
        ))}
      </div>

      {busy && !plans.length ? (
        <div className="rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] p-8 shadow-sm">
          <div className="animate-pulse space-y-4">
            <div className="h-6 w-56 rounded bg-[var(--surface-3)]" />
            <div className="h-64 rounded bg-[var(--surface-3)]" />
          </div>
        </div>
      ) : error && !plans.length ? (
        <div className="rounded-2xl border border-[var(--error)] bg-[var(--error-light)] p-8 text-center shadow-sm">
          <h3 className="text-xl font-semibold text-[var(--text-primary)]">{t('errorState.title')}</h3>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">{error || t('errorState.defaultMessage')}</p>
          <button
            type="button"
            onClick={load}
            className="mt-4 inline-flex items-center gap-2 rounded-2xl border border-[var(--border)] bg-[var(--surface-2)] px-4 py-2 font-semibold text-[var(--text-primary)]"
          >
            <RefreshCw size={16} />
            {t('errorState.retry')}
          </button>
        </div>
      ) : plans.length === 0 ? (
        <div className="flex flex-col items-center justify-center space-y-4 py-20 text-center">
          <div className="flex h-48 w-48 items-center justify-center rounded-full bg-[var(--surface-3)]">
            <MaterialIcon className="text-[80px] text-[var(--text-secondary)]">inventory</MaterialIcon>
          </div>
          <div>
            <h3 className="text-xl font-semibold text-[var(--text-primary)]">{t('emptyState.title')}</h3>
            <p className="mx-auto mt-2 max-w-sm text-sm text-[var(--text-secondary)]">{t('emptyState.description')}</p>
          </div>
          <button
            type="button"
            onClick={openCreateModal}
            className="rounded-2xl bg-[var(--primary-dark)] px-6 py-2.5 font-bold text-white"
          >
            {t('emptyState.createButton')}
          </button>
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-[var(--primary-light)] bg-[var(--surface-2)] shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[980px] border-collapse text-left">
              <thead>
                <tr className="border-b border-[var(--primary-light)] bg-[var(--surface-3)] text-xs font-bold text-[var(--text-secondary)]">
                  <th className="px-6 py-4">{t('table.planId')}</th>
                  <th className="px-6 py-4">{t('table.planInfo')}</th>
                  <th className="px-6 py-4">{t('table.priceCycle')}</th>
                  <th className="px-6 py-4">{t('table.aiTier')}</th>
                  <th className="px-6 py-4">{t('table.status')}</th>
                  <th className="px-6 py-4">{t('table.subscribers')}</th>
                  <th className="px-6 py-4 text-right">{t('table.actions')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border)]">
                {pagedPlans.map((plan, index) => {
                  const price = getPrimaryPrice(plan);
                  const subscriberCount = getSubscriberCount(plan);
                  const isActive = Boolean(plan.isActive);
                  const rowTierKey = getTierKey(plan);
                  return (
                    <tr
                      key={plan.planId || `${plan.code}-${index}`}
                      className={`cursor-pointer transition-colors ${isActive ? 'hover:bg-[var(--surface-1)]' : 'bg-[var(--surface-1)] opacity-80 hover:opacity-100'}`}
                      onClick={() => openDrawer(plan.planId)}
                    >
                      <td className="px-6 py-4 font-medium text-[var(--primary-dark)]">{getPlanIdDisplay(plan, index)}</td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          <span className="font-bold text-[var(--text-primary)]">{plan.name || t('fields.notAvailable')}</span>
                          {plan.isPopular ? (
                            <span className="flex items-center gap-1 rounded-full bg-[var(--primary-light)] px-1.5 py-0.5 text-[10px] font-bold text-[var(--primary-dark)]">
                              <MaterialIcon className="text-[12px]" filled>star</MaterialIcon>
                              {t('table.mostPopular')}
                            </span>
                          ) : null}
                        </div>
                        <p className="mt-0.5 text-xs text-[var(--text-secondary)]">{plan.code || t('fields.notAvailable')}</p>
                      </td>
                      <td className="px-6 py-4">
                        <p className="font-bold text-[var(--text-primary)]">{formatCurrency(price?.amount, price?.currency || 'VND', language)}</p>
                        <p className="text-xs text-[var(--text-secondary)]">{t(`table.per${getBillingCycleKey(price?.billingCycle).charAt(0).toUpperCase()}${getBillingCycleKey(price?.billingCycle).slice(1)}`)}</p>
                      </td>
                      <td className="px-6 py-4">
                        <span className={`rounded px-2 py-1 text-[10px] font-bold ${rowTierKey === 'advanced' ? 'bg-[var(--primary-dark)] text-white' : rowTierKey === 'enterprise' ? 'bg-[var(--text-primary)] text-white' : 'bg-[var(--surface-3)] text-[var(--text-secondary)]'}`}>
                          {t(`tiers.${rowTierKey}`)}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <span className={`rounded-full border px-2 py-1 text-[10px] font-bold ${isActive ? 'border-[var(--primary-light)] bg-[var(--primary-xlight)] text-[var(--primary-dark)]' : 'border-[var(--border)] bg-[var(--surface-3)] text-[var(--text-secondary)]'}`}>
                          {isActive ? t('status.active') : t('status.inactive')}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          <span className="font-bold text-[var(--text-primary)]">{subscriberCount == null ? t('fields.notAvailable') : formatNumber(subscriberCount, language)}</span>
                          {subscriberCount > 0 ? <MaterialIcon className="text-[16px] text-[var(--primary-dark)]">trending_up</MaterialIcon> : null}
                        </div>
                      </td>
                      <td className="space-x-2 px-6 py-4 text-right">
                        {isActive ? (
                          <>
                            <button type="button" className="rounded p-1 text-[var(--text-secondary)] hover:bg-[var(--surface-3)] hover:text-[var(--primary-dark)]" onClick={(event) => { event.stopPropagation(); openEditModal(plan); }}>
                              <MaterialIcon>edit</MaterialIcon>
                            </button>
                            <button type="button" className="rounded p-1 text-[var(--text-secondary)] hover:bg-[var(--surface-3)] hover:text-[var(--primary-dark)]" onClick={(event) => { event.stopPropagation(); openDuplicateModal(plan); }}>
                              <MaterialIcon>content_copy</MaterialIcon>
                            </button>
                            <button type="button" className="rounded p-1 text-[var(--error)] hover:bg-[var(--error-light)]" onClick={(event) => { event.stopPropagation(); openDeleteDialog(plan.planId); }}>
                              <MaterialIcon>delete</MaterialIcon>
                            </button>
                          </>
                        ) : (
                          <>
                            <button type="button" className="rounded p-1 text-[var(--text-secondary)] hover:bg-[var(--surface-3)] hover:text-[var(--primary-dark)]" onClick={(event) => { event.stopPropagation(); openDrawer(plan.planId); }}>
                              <MaterialIcon>visibility</MaterialIcon>
                            </button>
                            <button type="button" className="rounded p-1 text-[var(--primary-dark)] hover:bg-[var(--primary-xlight)]" onClick={(event) => { event.stopPropagation(); handleToggleStatus(plan); }}>
                              <MaterialIcon>refresh</MaterialIcon>
                            </button>
                            <button type="button" className="rounded p-1 text-[var(--error)] hover:bg-[var(--error-light)]" onClick={(event) => { event.stopPropagation(); openDeleteDialog(plan.planId); }}>
                              <MaterialIcon>delete</MaterialIcon>
                            </button>
                          </>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between bg-[var(--surface-3)] px-6 py-4">
            <p className="text-xs text-[var(--text-secondary)]">{t('table.showing', { start: showingStart, end: showingEnd, total: plans.length })}</p>
            <div className="flex gap-2">
              <button
                type="button"
                disabled={currentPage === 1}
                onClick={() => setCurrentPage((value) => Math.max(1, value - 1))}
                className="rounded border border-[var(--border)] px-3 py-1 disabled:opacity-50"
              >
                <MaterialIcon className="text-[18px]">chevron_left</MaterialIcon>
              </button>
              <button type="button" className="rounded border border-[var(--primary-dark)] bg-[var(--primary-dark)] px-3 py-1 text-xs font-bold text-white">
                {currentPage}
              </button>
              <button
                type="button"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage((value) => Math.min(totalPages, value + 1))}
                className="rounded border border-[var(--border)] px-3 py-1 disabled:opacity-50"
              >
                <MaterialIcon className="text-[18px]">chevron_right</MaterialIcon>
              </button>
            </div>
          </div>
        </div>
      )}

      {isModalOpen ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-[rgba(31,45,61,0.5)] p-4 backdrop-blur-sm" onMouseDown={(event) => { if (event.target === event.currentTarget) setIsModalOpen(false); }}>
          <div className="flex max-h-[90vh] w-full max-w-[1000px] flex-col overflow-hidden rounded-[24px] bg-[var(--surface-2)] shadow-2xl" onMouseDown={(event) => event.stopPropagation()}>
            <div className="flex items-center justify-between border-b border-[var(--border)] px-8 py-4">
              <div>
                <h2 className="text-2xl font-semibold text-[var(--text-primary)]">{modalMode === 'edit' ? t('modal.editTitle') : t('modal.createTitle')}</h2>
                <p className="text-sm text-[var(--text-secondary)]">{t('modal.subtitle')}</p>
              </div>
              <button type="button" className="rounded-full p-2 hover:bg-[var(--surface-3)]" onClick={() => setIsModalOpen(false)}>
                <MaterialIcon>close</MaterialIcon>
              </button>
            </div>
            <div className="grid flex-1 grid-cols-1 gap-8 overflow-y-auto p-8 lg:grid-cols-2">
              <div className="space-y-6">
                <section className="space-y-4">
                  <h3 className="flex items-center gap-2 text-base font-bold text-[var(--primary-dark)]">
                    <MaterialIcon className="text-[20px]">info</MaterialIcon>{t('modal.basicInfo')}
                  </h3>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.planName')}</label>
                      <input value={planForm.name} onChange={(event) => updatePlanForm('name', event.target.value)} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white" placeholder={t('modal.placeholder.planName')} />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.internalCode')}</label>
                      <input value={planForm.code} onChange={(event) => updatePlanForm('code', event.target.value)} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white" placeholder={t('modal.placeholder.internalCode')} />
                    </div>
                    <div className="col-span-2">
                      <label className="mb-1 block text-xs font-bold">{t('modal.description')}</label>
                      <textarea value={planForm.description} onChange={(event) => updatePlanForm('description', event.target.value)} rows={2} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white" placeholder={t('modal.placeholder.description')} />
                    </div>
                  </div>
                </section>

                <section className="space-y-4">
                  <h3 className="flex items-center gap-2 text-base font-bold text-[var(--primary-dark)]">
                    <MaterialIcon className="text-[20px]">payments</MaterialIcon>{t('modal.pricing')}
                  </h3>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.currency')}</label>
                      <select value={planForm.currency} onChange={(event) => updatePlanForm('currency', event.target.value)} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white">
                        <option value="VND">VND</option>
                        <option value="USD">USD</option>
                      </select>
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.price')}</label>
                      <input type="number" value={planForm.amount} onChange={(event) => updatePlanForm('amount', event.target.value)} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white" placeholder="0" />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.billingCycle')}</label>
                      <select value={planForm.billingCycle} onChange={(event) => updatePlanForm('billingCycle', Number(event.target.value))} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white">
                        <option value={1}>{t('fields.billingMonthly')}</option>
                        <option value={2}>{t('fields.billingYearly')}</option>
                      </select>
                    </div>
                  </div>
                  <div ref={priceSectionRef} className={`grid grid-cols-1 gap-4 md:grid-cols-3 ${priceErrors.message ? 'rounded-2xl border border-[var(--error)] bg-[var(--error-light)]/40 p-4' : ''}`}>
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.cycleCount')}</label>
                      <input
                        type="number"
                        min="1"
                        value={planForm.billingCycleCount}
                        onChange={(event) => updatePlanForm('billingCycleCount', event.target.value)}
                        className="w-full rounded-2xl border border-[var(--primary-light)] bg-white"
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.effectiveFrom')}</label>
                      <input
                        ref={effectiveFromRef}
                        type="datetime-local"
                        value={planForm.effectiveFrom}
                        onChange={(event) => updatePlanForm('effectiveFrom', event.target.value)}
                        className={`w-full rounded-2xl border bg-white ${priceErrors.effectiveFrom ? 'border-[var(--error)]' : 'border-[var(--primary-light)]'}`}
                      />
                      {priceErrors.effectiveFrom ? <p className="mt-1 text-xs text-[var(--error)]">{priceErrors.effectiveFrom}</p> : null}
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.effectiveTo')}</label>
                      <input
                        ref={effectiveToRef}
                        type="datetime-local"
                        value={planForm.effectiveTo}
                        onChange={(event) => updatePlanForm('effectiveTo', event.target.value)}
                        className={`w-full rounded-2xl border bg-white ${priceErrors.effectiveTo ? 'border-[var(--error)]' : 'border-[var(--primary-light)]'}`}
                      />
                      {priceErrors.effectiveTo ? <p className="mt-1 text-xs text-[var(--error)]">{priceErrors.effectiveTo}</p> : null}
                    </div>
                    {priceErrors.message ? <p className="md:col-span-3 text-sm font-medium text-[var(--error)]">{priceErrors.message}</p> : null}
                  </div>
                </section>

                <section className="space-y-4">
                  <h3 className="flex items-center gap-2 text-base font-bold text-[var(--primary-dark)]">
                    <MaterialIcon className="text-[20px]">bolt</MaterialIcon>{t('modal.quotaAi')}
                  </h3>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.monthlyQuota')}</label>
                      <input type="number" value={planForm.interviewQuota} onChange={(event) => updatePlanForm('interviewQuota', event.target.value)} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white" />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-bold">{t('modal.aiTier')}</label>
                      <select value={planForm.aiTier} onChange={(event) => updatePlanForm('aiTier', event.target.value)} className="w-full rounded-2xl border border-[var(--primary-light)] bg-white">
                        <option value="STANDARD">{t('modal.aiTierStandard')}</option>
                        <option value="ADVANCED">{t('modal.aiTierAdvanced')}</option>
                        <option value="ENTERPRISE">{t('modal.aiTierEnterprise')}</option>
                      </select>
                    </div>
                    <div className="col-span-2 flex items-center gap-2">
                      <input id="adv_analytics" checked={planForm.advancedAnalyticsEnabled} onChange={(event) => updatePlanForm('advancedAnalyticsEnabled', event.target.checked)} className="rounded text-[var(--primary-dark)]" type="checkbox" />
                      <label className="text-sm font-medium" htmlFor="adv_analytics">{t('modal.advancedAnalytics')}</label>
                    </div>
                  </div>
                </section>
              </div>

              <div className="space-y-6">
                <section className="space-y-4">
                  <h3 className="text-base font-bold text-[var(--primary-dark)]">{t('modal.visibility')}</h3>
                  <div className="flex items-center justify-between rounded-2xl bg-[var(--surface-3)] p-4">
                    <div>
                      <p className="font-bold text-[var(--text-primary)]">{t('modal.activeStatus')}</p>
                      <p className="text-xs text-[var(--text-secondary)]">{t('modal.activeStatusHelp')}</p>
                    </div>
                    <label className="relative inline-flex cursor-pointer items-center">
                      <input checked={planForm.isActive} onChange={(event) => updatePlanForm('isActive', event.target.checked)} className="peer sr-only" type="checkbox" />
                      <div className="h-6 w-11 rounded-full bg-[var(--border-strong)] after:absolute after:start-[2px] after:top-[2px] after:h-5 after:w-5 after:rounded-full after:border after:border-gray-300 after:bg-white after:transition-all after:content-[''] peer-checked:bg-[var(--primary-dark)] peer-checked:after:translate-x-full" />
                    </label>
                  </div>
                  <div className="flex items-center justify-between rounded-2xl bg-[var(--surface-3)] p-4">
                    <div>
                      <p className="font-bold text-[var(--text-primary)]">{t('modal.featured')}</p>
                      <p className="text-xs text-[var(--text-secondary)]">{t('modal.featuredHelp')}</p>
                    </div>
                    <label className="relative inline-flex cursor-pointer items-center">
                      <input checked={planForm.isPopular} onChange={(event) => updatePlanForm('isPopular', event.target.checked)} className="peer sr-only" type="checkbox" />
                      <div className="h-6 w-11 rounded-full bg-[var(--border-strong)] after:absolute after:start-[2px] after:top-[2px] after:h-5 after:w-5 after:rounded-full after:border after:border-gray-300 after:bg-white after:transition-all after:content-[''] peer-checked:bg-[var(--primary-dark)] peer-checked:after:translate-x-full" />
                    </label>
                  </div>
                </section>

                <section className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-base font-bold text-[var(--primary-dark)]">{t('modal.features')}</h3>
                    <button type="button" className="text-xs font-bold text-[var(--primary-dark)] hover:underline" onClick={addFeatureField}>{t('modal.addFeature')}</button>
                  </div>
                  <div className="max-h-72 space-y-3 overflow-y-auto pr-2">
                    {planForm.features.map((feature, index) => (
                      <div key={feature.clientKey} className="rounded-2xl border border-[var(--primary-light)] bg-white p-4 shadow-sm">
                        <div className="mb-3 flex items-center justify-between gap-3">
                          <p className="text-sm font-bold text-[var(--text-primary)]">{t('modal.featureItem', { index: index + 1 })}</p>
                          <button type="button" className="text-[var(--error)]" onClick={() => removeFeature(index)}>
                            <MaterialIcon className="text-[20px]">delete</MaterialIcon>
                          </button>
                        </div>
                        <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                          <label className="text-xs font-semibold text-[var(--text-secondary)]">
                            {t('modal.featureCode')}
                            <input
                              value={feature.featureCode}
                              onChange={(event) => updateFeature(index, 'featureCode', event.target.value.toUpperCase())}
                              className="mt-1 w-full rounded-xl border border-[var(--primary-light)] text-xs"
                              type="text"
                              placeholder={t('modal.featureCodePlaceholder')}
                            />
                          </label>
                          <label className="text-xs font-semibold text-[var(--text-secondary)]">
                            {t('modal.featureLimit')}
                            <input
                              type="number"
                              min="0"
                              value={feature.limitValue}
                              onChange={(event) => updateFeature(index, 'limitValue', event.target.value)}
                              className="mt-1 w-full rounded-xl border border-[var(--primary-light)] text-xs"
                              placeholder={t('modal.featureLimitPlaceholder')}
                            />
                          </label>
                          <label className="text-xs font-semibold text-[var(--text-secondary)]">
                            {t('modal.featureOrder')}
                            <input
                              type="number"
                              min="1"
                              value={feature.displayOrder}
                              onChange={(event) => updateFeature(index, 'displayOrder', event.target.value)}
                              className="mt-1 w-full rounded-xl border border-[var(--primary-light)] text-xs"
                            />
                          </label>
                          <label className="flex items-center justify-between rounded-xl border border-dashed border-[var(--primary-light)] px-3 py-2 text-xs font-semibold text-[var(--text-secondary)]">
                            <span>{t('modal.featureEnabled')}</span>
                            <input
                              type="checkbox"
                              checked={feature.isEnabled}
                              onChange={(event) => updateFeature(index, 'isEnabled', event.target.checked)}
                              className="rounded text-[var(--primary-dark)]"
                            />
                          </label>
                        </div>
                      </div>
                    ))}
                  </div>
                </section>

                <div className="pt-4">
                  <p className="mb-3 text-xs font-bold uppercase tracking-[0.12em] text-[var(--text-secondary)]">{t('modal.livePreview')}</p>
                  <div className="relative mx-auto max-w-sm overflow-hidden rounded-[24px] border-2 border-[var(--primary-dark)] bg-white p-6 shadow-lg">
                    {planForm.isPopular ? (
                      <div className="absolute -right-1 -top-1">
                        <div className="translate-x-3 translate-y-1 rotate-45 bg-[var(--primary-dark)] px-4 py-1 text-[8px] font-bold text-white">{t('modal.popularRibbon')}</div>
                      </div>
                    ) : null}
                    <h4 className="text-xl font-semibold text-[var(--text-primary)]">{planForm.name || t('modal.planName')}</h4>
                    <div className="mt-4 flex items-baseline">
                      <span className="text-[32px] font-bold leading-[40px] text-[var(--text-primary)]">{planForm.amount || 0}</span>
                      <span className="ml-1 text-sm text-[var(--text-secondary)]">{planForm.currency || 'VND'} / {getBillingCycleKey(planForm.billingCycle) === 'yearly' ? t('modal.cycleShortYearly') : getBillingCycleKey(planForm.billingCycle) === 'quarterly' ? t('modal.cycleShortQuarterly') : t('modal.cycleShortMonthly')}</span>
                    </div>
                    <ul className="mt-6 space-y-3">
                      {planForm.features.filter((feature) => String(feature.featureCode || '').trim()).length > 0 ? planForm.features.filter((feature) => String(feature.featureCode || '').trim()).map((feature, index) => (
                        <li key={feature.clientKey} className="flex items-start gap-2 text-sm text-[var(--text-primary)]">
                          <MaterialIcon className="mt-0.5 text-[18px] text-[var(--primary-dark)]">check_circle</MaterialIcon>
                          <div>
                            <p className="font-semibold">{feature.featureCode}</p>
                            <p className="text-xs text-[var(--text-secondary)]">
                              {feature.limitValue === '' || feature.limitValue == null
                                ? t('fields.unlimited')
                                : `${t('fields.quota')}: ${feature.limitValue}`}
                              {' • '}
                              {feature.isEnabled ? t('modal.featureEnabled') : t('modal.featureDisabled')}
                            </p>
                          </div>
                        </li>
                      )) : (
                        <li className="text-sm text-[var(--text-secondary)]">{t('fields.notAvailable')}</li>
                      )}
                    </ul>
                    <button type="button" className="mt-8 w-full rounded-2xl bg-[var(--primary-dark)] py-3 font-bold text-white">{t('modal.subscribeNow')}</button>
                  </div>
                </div>
              </div>
            </div>
            <div className="flex justify-end gap-3 border-t border-[var(--border)] bg-[var(--surface-3)] p-6">
              <button type="button" onClick={() => setIsModalOpen(false)} className="rounded-2xl border border-[var(--border)] px-6 py-2 font-bold text-[var(--text-primary)] hover:bg-[var(--surface-2)]">{t('modal.discard')}</button>
              <button type="button" onClick={handleSavePlan} disabled={isSaving} className="rounded-2xl bg-[var(--primary-dark)] px-8 py-2 font-bold text-white shadow-md hover:bg-[var(--primary)] disabled:opacity-60">{isSaving ? t('modal.saving') : t('modal.savePlan')}</button>
            </div>
          </div>
        </div>
      ) : null}

      {isDrawerOpen && selectedPlan ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-[rgba(31,45,61,0.5)] p-4 backdrop-blur-sm transition-all duration-300" onMouseDown={(event) => { if (event.target === event.currentTarget) setIsDrawerOpen(false); }}>
          <div className="flex max-h-[90vh] w-full max-w-[650px] scale-100 flex-col overflow-hidden rounded-[24px] bg-[var(--surface-2)] shadow-2xl transition-all duration-300" onMouseDown={(event) => event.stopPropagation()}>
            <div className="flex items-center justify-between border-b border-[var(--border)] bg-[var(--primary-xlight)]/30 p-6">
              <div className="flex items-center gap-4">
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--primary-dark)] text-white">
                  <MaterialIcon className="text-[28px]">inventory_2</MaterialIcon>
                </div>
                <div>
                  <h3 className="text-xl font-semibold text-[var(--text-primary)]">{selectedPlan.name || t('fields.notAvailable')}</h3>
                  <p className="text-xs font-bold text-[var(--text-secondary)]">{getPlanIdDisplay(selectedPlan, 0)} • {selectedPlan.isActive ? t('status.active') : t('status.inactive')}</p>
                </div>
              </div>
              <button type="button" className="rounded-full p-2 hover:bg-[var(--surface-3)]" onClick={() => setIsDrawerOpen(false)}>
                <MaterialIcon>close</MaterialIcon>
              </button>
            </div>
            <div className="flex-1 space-y-8 overflow-y-auto p-6">
              <div className="grid grid-cols-2 gap-4">
                <div className="rounded-2xl bg-[var(--surface-3)] p-4">
                  <p className="text-xs font-bold uppercase text-[var(--text-secondary)]">{t('drawer.subscribers')}</p>
                  <p className="mt-1 text-2xl font-bold text-[var(--text-primary)]">{formatNumber(getSubscriberCount(selectedPlan), language)}</p>
                </div>
                <div className="rounded-2xl bg-[var(--surface-3)] p-4">
                  <p className="text-xs font-bold uppercase text-[var(--text-secondary)]">{t('drawer.revenueMtd')}</p>
                  <p className="mt-1 text-2xl font-bold text-[var(--text-primary)]">{formatCurrency(getPlanRevenue(selectedPlan), 'VND', language)}</p>
                </div>
              </div>

              <section>
                <h4 className="mb-4 text-xs font-bold uppercase tracking-[0.12em] text-[var(--text-secondary)]">{t('drawer.planDetails')}</h4>
                <div className="space-y-4">
                  {[
                    [t('drawer.billingCycle'), t(`fields.billing${getBillingCycleKey(selectedPrice?.billingCycle).charAt(0).toUpperCase()}${getBillingCycleKey(selectedPrice?.billingCycle).slice(1)}`)],
                    [t('drawer.aiCapability'), t(`tiers.${getTierKey(selectedPlan)}`)],
                    [t('drawer.interviewQuota'), selectedPlan.isFree ? t('fields.unlimited') : formatNumber(selectedPlan.interviewQuota, language)],
                    [t('drawer.effectiveFrom'), formatDateTime(selectedPrice?.effectiveFrom, language)],
                    [t('drawer.effectiveTo'), formatDateTime(selectedPrice?.effectiveTo, language)],
                    [t('drawer.createdDate'), formatDateTime(selectedPlan.createdAt, language)],
                  ].map(([label, value]) => (
                    <div key={label} className="flex items-center justify-between border-b border-[var(--border)] py-2">
                      <span className="text-sm text-[var(--text-secondary)]">{label}</span>
                      <span className="text-sm font-bold text-[var(--text-primary)]">{value || t('fields.notAvailable')}</span>
                    </div>
                  ))}
                </div>
              </section>

              <section>
                <h4 className="mb-4 text-xs font-bold uppercase tracking-[0.12em] text-[var(--text-secondary)]">{t('drawer.recentSubscriptions')}</h4>
                <div className="space-y-3">
                  {planHistory.length ? planHistory.slice(0, 2).map((record, index) => {
                    const customerName = record.fullName || record.customerName || record.name || t('fields.notAvailable');
                    const amount = record.amount ?? record.totalAmount ?? selectedPrice?.amount;
                    return (
                      <div key={`${customerName}-${index}`} className="flex items-center gap-3">
                        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--surface-3)] text-xs font-bold text-[var(--text-primary)]">{getInitials(customerName)}</div>
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm font-bold text-[var(--text-primary)]">{customerName}</p>
                          <p className="text-[10px] text-[var(--text-secondary)]">{formatRelativePurchase(record.purchasedAt || record.createdAt, t, language)}</p>
                        </div>
                        <span className="text-sm font-bold text-[var(--primary-dark)]">{formatCurrency(amount, record.currency || selectedPrice?.currency || 'VND', language)}</span>
                      </div>
                    );
                  }) : <p className="text-sm text-[var(--text-secondary)]">{t('drawer.noRecentSubscriptions')}</p>}
                </div>
                <button type="button" onClick={() => notify.info(t('purchaseHistoryUnavailable'))} className="mt-4 w-full py-2 text-xs font-bold text-[var(--primary-dark)] hover:underline">{t('drawer.viewAllPurchaseHistory')}</button>
              </section>
            </div>
            <div className="flex gap-2 border-t border-[var(--border)] bg-[var(--surface-3)] p-6">
              <button type="button" onClick={() => { setIsDrawerOpen(false); openEditModal(selectedPlan); }} className="flex-1 rounded-2xl bg-[var(--primary-dark)] py-2.5 font-bold text-white">{t('drawer.editPlan')}</button>
              <button type="button" className="rounded-2xl bg-[var(--surface-1)] px-3 py-2.5 font-bold text-[var(--text-primary)]"><MaterialIcon>more_horiz</MaterialIcon></button>
            </div>
          </div>
        </div>
      ) : null}

      {isDeleteOpen && selectedPlan ? (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-[rgba(31,45,61,0.6)] p-4" onMouseDown={(event) => { if (event.target === event.currentTarget) setIsDeleteOpen(false); }}>
          <div className="w-full max-w-md space-y-6 rounded-[24px] bg-[var(--surface-2)] p-6 shadow-2xl" onMouseDown={(event) => event.stopPropagation()}>
            <div className="flex items-center gap-4 text-[var(--error)]">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-[var(--error-light)]">
                <MaterialIcon className="text-[32px]">warning</MaterialIcon>
              </div>
              <h3 className="text-xl font-semibold text-[var(--text-primary)]">{t('deleteDialog.title')}</h3>
            </div>
            <p className="text-sm text-[var(--text-secondary)]">{t('deleteDialog.message', { name: selectedPlan.name || t('fields.notAvailable') })}</p>
            <div className="rounded-r-2xl border-l-4 border-[var(--error)] bg-[var(--error-light)]/40 p-4">
              <p className="flex items-center gap-2 text-xs font-bold text-[var(--error)]">
                <MaterialIcon className="text-[16px]">info</MaterialIcon>{t('deleteDialog.warning')}
              </p>
              <p className="mt-1 text-[10px] uppercase tracking-tight text-[var(--error)]">{t('deleteDialog.warningMessage', { count: getHistoricalPurchaseCount(selectedPlan) })}</p>
            </div>
            <div className="flex gap-3 pt-2">
              <button type="button" onClick={() => setIsDeleteOpen(false)} className="flex-1 rounded-2xl border border-[var(--border)] py-2.5 font-bold text-[var(--text-primary)] hover:bg-[var(--surface-3)]">{t('deleteDialog.cancel')}</button>
              <button type="button" onClick={handleDeletePlan} className="flex-1 rounded-2xl bg-[var(--error)] py-2.5 font-bold text-white shadow-lg hover:brightness-95">{t('deleteDialog.confirm')}</button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

export default SubscriptionManagementPage;
