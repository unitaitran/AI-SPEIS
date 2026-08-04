import React from 'react';
import { useTranslation } from 'react-i18next';
import { X } from 'lucide-react';

function formatDate(value, locale) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString(locale);
}

function formatAmount(value, locale) {
  const amount = Number(value);
  if (!Number.isFinite(amount)) return '—';
  return amount.toLocaleString(locale);
}

function SubscriptionPlanDrawer({ open, plan, onClose }) {
  const { t, i18n } = useTranslation('admin-subscription');
  const locale = i18n.language === 'vi' ? 'vi-VN' : 'en-US';

  const billingCycleLabel = {
    1: t('billingCycle.monthly'),
    2: t('billingCycle.yearly'),
  };

  if (!open || !plan) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-[140] flex" role="dialog" aria-modal="true">
      <button className="flex-1 bg-text-primary/20 backdrop-blur-[2px]" type="button" aria-label={t('drawer.close')} onClick={onClose} />
      <aside className="h-full w-full max-w-xl overflow-y-auto border-l border-border/60 bg-white p-6 shadow-[-10px_0_30px_rgba(31,45,61,0.16)]">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-xs uppercase tracking-[0.08em] text-text-secondary">{t('drawer.planDetail')}</p>
            <h2 className="mt-1 text-xl font-semibold text-text-primary">{plan.name}</h2>
            <p className="mt-1 text-sm text-text-secondary">{plan.code}</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg border border-border p-2 text-text-secondary">
            <X size={16} />
          </button>
        </div>

        <div className="mt-6 grid grid-cols-2 gap-3">
          <div className="rounded-xl border border-border bg-surface-1 p-3">
            <p className="text-xs text-text-secondary">{t('drawer.interviewQuota')}</p>
            <p className="mt-1 text-sm font-semibold text-text-primary">{plan.interviewQuota}</p>
          </div>
          <div className="rounded-xl border border-border bg-surface-1 p-3">
            <p className="text-xs text-text-secondary">{t('drawer.displayOrder')}</p>
            <p className="mt-1 text-sm font-semibold text-text-primary">{plan.displayOrder}</p>
          </div>
          <div className="rounded-xl border border-border bg-surface-1 p-3">
            <p className="text-xs text-text-secondary">{t('drawer.status')}</p>
            <p className="mt-1 text-sm font-semibold text-text-primary">{plan.isActive ? t('status.active') : t('status.inactive')}</p>
          </div>
          <div className="rounded-xl border border-border bg-surface-1 p-3">
            <p className="text-xs text-text-secondary">{t('drawer.quotaResetDays')}</p>
            <p className="mt-1 text-sm font-semibold text-text-primary">{plan.quotaResetDays ?? '—'}</p>
          </div>
        </div>

        <section className="mt-6">
          <h3 className="text-sm font-semibold text-text-primary">{t('drawer.description')}</h3>
          <p className="mt-2 rounded-xl border border-border bg-surface-1 p-3 text-sm text-text-secondary">{plan.description || '—'}</p>
        </section>

        <section className="mt-6">
          <h3 className="text-sm font-semibold text-text-primary">{t('drawer.pricing')}</h3>
          {Array.isArray(plan.prices) && plan.prices.length > 0 ? (
            <div className="mt-2 overflow-hidden rounded-xl border border-border">
              <table className="min-w-full divide-y divide-border text-sm">
                <thead className="bg-surface-1 text-left text-text-secondary">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t('drawer.billing')}</th>
                    <th className="px-3 py-2 font-medium">{t('drawer.amount')}</th>
                    <th className="px-3 py-2 font-medium">{t('drawer.currency')}</th>
                    <th className="px-3 py-2 font-medium">{t('drawer.status')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border bg-white">
                  {plan.prices.map((price) => (
                    <tr key={price.priceId}>
                      <td className="px-3 py-2 text-text-primary">{billingCycleLabel[price.billingCycle] || '—'} x {price.billingCycleCount}</td>
                      <td className="px-3 py-2 text-text-primary">{formatAmount(price.amount, locale)}</td>
                      <td className="px-3 py-2 text-text-primary">{price.currency || '—'}</td>
                      <td className="px-3 py-2 text-text-primary">{price.isActive ? t('status.active') : t('status.inactive')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="mt-2 rounded-xl border border-border bg-surface-1 p-3 text-sm text-text-secondary">{t('drawer.noPricing')}</p>
          )}
        </section>

        <section className="mt-6 grid grid-cols-2 gap-3">
          <div className="rounded-xl border border-border bg-surface-1 p-3">
            <p className="text-xs text-text-secondary">{t('drawer.createdAt')}</p>
            <p className="mt-1 text-sm font-medium text-text-primary">{formatDate(plan.createdAt, locale)}</p>
          </div>
          <div className="rounded-xl border border-border bg-surface-1 p-3">
            <p className="text-xs text-text-secondary">{t('drawer.updatedAt')}</p>
            <p className="mt-1 text-sm font-medium text-text-primary">{formatDate(plan.updatedAt, locale)}</p>
          </div>
        </section>
      </aside>
    </div>
  );
}

export default SubscriptionPlanDrawer;
