import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, Trash2, X } from 'lucide-react';

const defaultPriceDraft = {
  priceId: null,
  billingCycle: 1,
  billingCycleCount: 1,
  amount: 0,
  currency: 'VND',
  effectiveFrom: '',
  effectiveTo: '',
  isActive: true,
};

const defaultForm = {
  code: '',
  name: '',
  description: '',
  interviewQuota: 15,
  quotaResetDays: 30,
  isFree: false,
  displayOrder: 1,
};

const toDateInput = (value) => {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return date.toISOString().slice(0, 10);
};

function SubscriptionPlanModal({ open, mode, submitting, initialPlan, existingPlans, onClose, onSubmit }) {
  const { t } = useTranslation('admin-subscription');

  const billingOptions = [
    { value: 1, label: t('billingCycle.monthly') },
    { value: 2, label: t('billingCycle.yearly') },
  ];

  const [form, setForm] = useState(defaultForm);
  const [prices, setPrices] = useState([]);

  useEffect(() => {
    if (!open) {
      return;
    }

    if (mode === 'edit' && initialPlan) {
      setForm({
        code: initialPlan.code || '',
        name: initialPlan.name || '',
        description: initialPlan.description || '',
        interviewQuota: Number(initialPlan.interviewQuota ?? 0),
        quotaResetDays: initialPlan.quotaResetDays ?? '',
        isFree: Boolean(initialPlan.isFree),
        displayOrder: Number(initialPlan.displayOrder ?? 0),
      });

      setPrices((initialPlan.prices || []).map((price) => ({
        priceId: price.priceId,
        billingCycle: Number(price.billingCycle || 1),
        billingCycleCount: Number(price.billingCycleCount || 1),
        amount: Number(price.amount || 0),
        currency: price.currency || 'VND',
        effectiveFrom: toDateInput(price.effectiveFrom),
        effectiveTo: toDateInput(price.effectiveTo),
        isActive: Boolean(price.isActive),
      })));
      return;
    }

    setForm(defaultForm);
    setPrices([]);
  }, [open, mode, initialPlan]);

  const errors = useMemo(() => {
    const result = {};

    if (!form.code || !String(form.code).trim()) {
      result.code = t('validation.codeRequired');
    } else {
      const normalizedCode = String(form.code).trim().toUpperCase();
      const duplicateCode = existingPlans.some((plan) => {
        if (mode === 'edit' && initialPlan?.planId === plan.planId) return false;
        return String(plan.code).trim().toUpperCase() === normalizedCode;
      });

      if (duplicateCode) {
        result.code = t('validation.codeDuplicate');
      }
    }

    if (!form.name || !String(form.name).trim()) {
      result.name = t('validation.nameRequired');
    }

    const quota = Number(form.interviewQuota);
    if (!Number.isInteger(quota) || quota < 0 || quota > 1000000) {
      result.interviewQuota = t('validation.interviewQuotaRange');
    }

    const order = Number(form.displayOrder);
    if (!Number.isInteger(order) || order < 0) {
      result.displayOrder = t('validation.displayOrderInvalid');
    }

    if (!form.isFree) {
      const resetDays = Number(form.quotaResetDays);
      if (!Number.isInteger(resetDays) || resetDays < 1 || resetDays > 3650) {
        result.quotaResetDays = t('validation.quotaResetDaysRange');
      }
    }

    prices.forEach((price, index) => {
      const prefix = `price_${index}`;
      const amount = Number(price.amount);
      const cycleCount = Number(price.billingCycleCount);
      const billing = Number(price.billingCycle);
      const currency = String(price.currency || '').trim().toUpperCase();

      if (![1, 2].includes(billing)) {
        result[`${prefix}_billingCycle`] = t('validation.billingCycleInvalid');
      }

      if (!Number.isInteger(cycleCount) || cycleCount < 1 || cycleCount > 120) {
        result[`${prefix}_billingCycleCount`] = t('validation.cycleCountRange');
      }

      if (!Number.isFinite(amount) || amount < 0) {
        result[`${prefix}_amount`] = t('validation.amountInvalid');
      }

      if (currency.length !== 3) {
        result[`${prefix}_currency`] = t('validation.currencyInvalid');
      }

      if (!price.effectiveFrom) {
        result[`${prefix}_effectiveFrom`] = t('validation.effectiveFromRequired');
      }

      if (price.effectiveTo && price.effectiveFrom) {
        const fromDate = new Date(price.effectiveFrom);
        const toDate = new Date(price.effectiveTo);

        if (toDate <= fromDate) {
          result[`${prefix}_effectiveTo`] = t('validation.effectiveToInvalid');
        }
      }
    });

    return result;
  }, [existingPlans, form, initialPlan?.planId, mode, prices, t]);

  const hasError = Object.keys(errors).length > 0;

  const handleSubmit = (event) => {
    event.preventDefault();
    if (hasError || submitting) return;

    const normalizedForm = {
      ...form,
      code: String(form.code).trim().toUpperCase(),
      name: String(form.name).trim(),
      description: form.description ? String(form.description).trim() : null,
      interviewQuota: Number(form.interviewQuota),
      quotaResetDays: form.isFree ? null : Number(form.quotaResetDays),
      displayOrder: Number(form.displayOrder),
    };

    const normalizedPrices = prices.map((price) => ({
      ...price,
      billingCycle: Number(price.billingCycle),
      billingCycleCount: Number(price.billingCycleCount),
      amount: Number(price.amount),
      currency: String(price.currency).trim().toUpperCase(),
      effectiveFrom: new Date(price.effectiveFrom).toISOString(),
      effectiveTo: price.effectiveTo ? new Date(price.effectiveTo).toISOString() : null,
    }));

    onSubmit({ form: normalizedForm, prices: normalizedPrices });
  };

  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-[160] flex items-center justify-center bg-text-primary/30 p-4 backdrop-blur-sm" role="dialog" aria-modal="true">
      <form onSubmit={handleSubmit} className="max-h-[92vh] w-full max-w-4xl overflow-y-auto rounded-2xl border border-border bg-white p-6 shadow-[0_20px_40px_rgba(31,45,61,0.2)]">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-xs uppercase tracking-[0.08em] text-text-secondary">{t('modal.subscriptionPlan')}</p>
            <h3 className="mt-1 text-xl font-semibold text-text-primary">{mode === 'create' ? t('modal.createTitle') : t('modal.editTitle')}</h3>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg border border-border p-2 text-text-secondary">
            <X size={16} />
          </button>
        </div>

        <div className="mt-5 grid grid-cols-1 gap-4 md:grid-cols-3">
          <label className="text-sm text-text-secondary">
            {t('modal.planCode')}
            <input
              value={form.code}
              onChange={(event) => setForm((prev) => ({ ...prev, code: event.target.value }))}
              className="mt-1 w-full rounded-xl border border-border px-3 py-2.5 text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
            />
            {errors.code && <span className="mt-1 block text-xs text-error">{errors.code}</span>}
          </label>

          <label className="text-sm text-text-secondary">
            {t('modal.planName')}
            <input
              value={form.name}
              onChange={(event) => setForm((prev) => ({ ...prev, name: event.target.value }))}
              className="mt-1 w-full rounded-xl border border-border px-3 py-2.5 text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
            />
            {errors.name && <span className="mt-1 block text-xs text-error">{errors.name}</span>}
          </label>

          <label className="text-sm text-text-secondary">
            {t('modal.displayOrder')}
            <input
              type="number"
              min="0"
              value={form.displayOrder}
              onChange={(event) => setForm((prev) => ({ ...prev, displayOrder: event.target.value }))}
              className="mt-1 w-full rounded-xl border border-border px-3 py-2.5 text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
            />
            {errors.displayOrder && <span className="mt-1 block text-xs text-error">{errors.displayOrder}</span>}
          </label>

          <label className="text-sm text-text-secondary md:col-span-3">
            {t('modal.description')}
            <textarea
              rows={3}
              value={form.description}
              onChange={(event) => setForm((prev) => ({ ...prev, description: event.target.value }))}
              className="mt-1 w-full rounded-xl border border-border px-3 py-2.5 text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
            />
          </label>

          <label className="text-sm text-text-secondary">
            {t('modal.interviewQuota')}
            <input
              type="number"
              min="0"
              max="1000000"
              value={form.interviewQuota}
              onChange={(event) => setForm((prev) => ({ ...prev, interviewQuota: event.target.value }))}
              className="mt-1 w-full rounded-xl border border-border px-3 py-2.5 text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
            />
            {errors.interviewQuota && <span className="mt-1 block text-xs text-error">{errors.interviewQuota}</span>}
          </label>

          <label className="text-sm text-text-secondary">
            {t('modal.quotaResetDays')}
            <input
              type="number"
              min="1"
              max="3650"
              disabled={form.isFree}
              value={form.quotaResetDays}
              onChange={(event) => setForm((prev) => ({ ...prev, quotaResetDays: event.target.value }))}
              className="mt-1 w-full rounded-xl border border-border px-3 py-2.5 text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight disabled:cursor-not-allowed disabled:bg-surface-1"
            />
            {errors.quotaResetDays && <span className="mt-1 block text-xs text-error">{errors.quotaResetDays}</span>}
          </label>

          <label className="flex items-center gap-2 self-end text-sm text-text-secondary">
            <input
              type="checkbox"
              checked={form.isFree}
              onChange={(event) => setForm((prev) => ({ ...prev, isFree: event.target.checked }))}
              className="h-4 w-4 rounded border-border"
            />
            {t('modal.freePlan')}
          </label>
        </div>

        {!form.isFree && (
          <section className="mt-6 rounded-2xl border border-border bg-surface-1 p-4">
            <div className="flex items-center justify-between">
              <h4 className="text-sm font-semibold text-text-primary">{t('modal.pricing')}</h4>
              <button
                type="button"
                onClick={() => setPrices((prev) => [...prev, defaultPriceDraft])}
                className="inline-flex items-center gap-2 rounded-lg border border-border bg-white px-3 py-1.5 text-sm font-medium text-text-secondary"
              >
                <Plus size={14} />
                {t('modal.addPrice')}
              </button>
            </div>

            {prices.length === 0 && (
              <p className="mt-3 text-sm text-text-secondary">{t('modal.noPrices')}</p>
            )}

            <div className="mt-3 space-y-3">
              {prices.map((price, index) => (
                <div key={`${price.priceId || 'new'}-${index}`} className="rounded-xl border border-border bg-white p-3">
                  <div className="grid grid-cols-1 gap-3 md:grid-cols-6">
                    <label className="text-xs text-text-secondary">
                      {t('modal.billingCycle')}
                      <select
                        value={price.billingCycle}
                        onChange={(event) => setPrices((prev) => prev.map((item, itemIndex) => itemIndex === index
                          ? { ...item, billingCycle: Number(event.target.value) }
                          : item))}
                        className="mt-1 w-full rounded-lg border border-border px-2.5 py-2 text-sm text-text-primary"
                      >
                        {billingOptions.map((option) => (
                          <option key={option.value} value={option.value}>{option.label}</option>
                        ))}
                      </select>
                      {errors[`price_${index}_billingCycle`] && <span className="mt-1 block text-[11px] text-error">{errors[`price_${index}_billingCycle`]}</span>}
                    </label>

                    <label className="text-xs text-text-secondary">
                      {t('modal.cycleCount')}
                      <input
                        type="number"
                        min="1"
                        max="120"
                        value={price.billingCycleCount}
                        onChange={(event) => setPrices((prev) => prev.map((item, itemIndex) => itemIndex === index
                          ? { ...item, billingCycleCount: event.target.value }
                          : item))}
                        className="mt-1 w-full rounded-lg border border-border px-2.5 py-2 text-sm text-text-primary"
                      />
                      {errors[`price_${index}_billingCycleCount`] && <span className="mt-1 block text-[11px] text-error">{errors[`price_${index}_billingCycleCount`]}</span>}
                    </label>

                    <label className="text-xs text-text-secondary">
                      {t('modal.amount')}
                      <input
                        type="number"
                        min="0"
                        value={price.amount}
                        onChange={(event) => setPrices((prev) => prev.map((item, itemIndex) => itemIndex === index
                          ? { ...item, amount: event.target.value }
                          : item))}
                        className="mt-1 w-full rounded-lg border border-border px-2.5 py-2 text-sm text-text-primary"
                      />
                      {errors[`price_${index}_amount`] && <span className="mt-1 block text-[11px] text-error">{errors[`price_${index}_amount`]}</span>}
                    </label>

                    <label className="text-xs text-text-secondary">
                      {t('modal.currency')}
                      <input
                        maxLength={3}
                        value={price.currency}
                        onChange={(event) => setPrices((prev) => prev.map((item, itemIndex) => itemIndex === index
                          ? { ...item, currency: event.target.value }
                          : item))}
                        className="mt-1 w-full rounded-lg border border-border px-2.5 py-2 text-sm text-text-primary"
                      />
                      {errors[`price_${index}_currency`] && <span className="mt-1 block text-[11px] text-error">{errors[`price_${index}_currency`]}</span>}
                    </label>

                    <label className="text-xs text-text-secondary">
                      {t('modal.effectiveFrom')}
                      <input
                        type="date"
                        value={price.effectiveFrom}
                        onChange={(event) => setPrices((prev) => prev.map((item, itemIndex) => itemIndex === index
                          ? { ...item, effectiveFrom: event.target.value }
                          : item))}
                        className="mt-1 w-full rounded-lg border border-border px-2.5 py-2 text-sm text-text-primary"
                      />
                      {errors[`price_${index}_effectiveFrom`] && <span className="mt-1 block text-[11px] text-error">{errors[`price_${index}_effectiveFrom`]}</span>}
                    </label>

                    <label className="text-xs text-text-secondary">
                      {t('modal.effectiveTo')}
                      <div className="flex items-center gap-2">
                        <input
                          type="date"
                          value={price.effectiveTo}
                          onChange={(event) => setPrices((prev) => prev.map((item, itemIndex) => itemIndex === index
                            ? { ...item, effectiveTo: event.target.value }
                            : item))}
                          className="mt-1 w-full rounded-lg border border-border px-2.5 py-2 text-sm text-text-primary"
                        />
                        <button
                          type="button"
                          onClick={() => setPrices((prev) => prev.filter((_, itemIndex) => itemIndex !== index))}
                          className="mt-1 rounded-lg border border-border p-2 text-text-secondary hover:text-error"
                          aria-label={t('modal.removePrice')}
                        >
                          <Trash2 size={14} />
                        </button>
                      </div>
                      {errors[`price_${index}_effectiveTo`] && <span className="mt-1 block text-[11px] text-error">{errors[`price_${index}_effectiveTo`]}</span>}
                    </label>
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

        <div className="mt-6 flex justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-xl border border-border bg-white px-4 py-2 text-sm font-medium text-text-secondary">
            {t('modal.cancel')}
          </button>
          <button type="submit" disabled={hasError || submitting} className="rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-60">
            {submitting ? t('modal.saving') : mode === 'create' ? t('modal.createButton') : t('modal.saveChanges')}
          </button>
        </div>
      </form>
    </div>
  );
}

export default SubscriptionPlanModal;
