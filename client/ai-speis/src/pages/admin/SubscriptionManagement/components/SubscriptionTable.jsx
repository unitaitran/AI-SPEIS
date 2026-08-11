import React from 'react';
import { useTranslation } from 'react-i18next';
import { Copy, Edit3, Eye, Power, Trash2 } from 'lucide-react';

function formatPrice(value, locale) {
  const amount = Number(value);
  if (!Number.isFinite(amount)) return '—';
  return amount.toLocaleString(locale);
}

function formatDate(value, locale) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleDateString(locale);
}

function SubscriptionTable({ rows, showColumns, onView, onEdit, onDuplicate, onToggleStatus, onDelete }) {
  const { t, i18n } = useTranslation('admin-subscription');
  const locale = i18n.language === 'vi' ? 'vi-VN' : 'en-US';

  const billingCycleLabel = {
    1: t('billingCycle.monthly'),
    2: t('billingCycle.yearly'),
  };

  return (
    <section className="overflow-hidden rounded-2xl border border-border/60 bg-white">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-border">
          <thead className="bg-surface-1 text-left text-xs uppercase tracking-[0.06em] text-text-secondary">
            <tr>
              <th className="px-4 py-3 font-semibold">{t('table.planId')}</th>
              <th className="px-4 py-3 font-semibold">{t('table.planCode')}</th>
              <th className="px-4 py-3 font-semibold">{t('table.planName')}</th>
              {showColumns.description && <th className="px-4 py-3 font-semibold">{t('table.description')}</th>}
              {showColumns.price && <th className="px-4 py-3 font-semibold">{t('table.price')}</th>}
              {showColumns.currency && <th className="px-4 py-3 font-semibold">{t('table.currency')}</th>}
              {showColumns.billingCycle && <th className="px-4 py-3 font-semibold">{t('table.billingCycle')}</th>}
              <th className="px-4 py-3 font-semibold">{t('table.quota')}</th>
              <th className="px-4 py-3 font-semibold">{t('table.status')}</th>
              {showColumns.subscribers && <th className="px-4 py-3 font-semibold">{t('table.subscribers')}</th>}
              {showColumns.revenue && <th className="px-4 py-3 font-semibold">{t('table.revenue')}</th>}
              {showColumns.updatedAt && <th className="px-4 py-3 font-semibold">{t('table.updatedAt')}</th>}
              <th className="px-4 py-3 font-semibold">{t('table.actions')}</th>
            </tr>
          </thead>

          <tbody className="divide-y divide-border bg-white text-sm">
            {rows.map((row) => (
              <tr key={row.planId} className="hover:bg-surface-1/60">
                <td className="px-4 py-3 text-text-primary">{row.planId}</td>
                <td className="px-4 py-3 text-text-primary">{row.code}</td>
                <td className="px-4 py-3 text-text-primary">{row.name}</td>
                {showColumns.description && <td className="px-4 py-3 text-text-secondary">{row.description || '—'}</td>}
                {showColumns.price && <td className="px-4 py-3 text-text-primary">{formatPrice(row.priceAmount, locale)}</td>}
                {showColumns.currency && <td className="px-4 py-3 text-text-primary">{row.currency || '—'}</td>}
                {showColumns.billingCycle && (
                  <td className="px-4 py-3 text-text-primary">{billingCycleLabel[row.billingCycle] || '—'}</td>
                )}
                <td className="px-4 py-3 text-text-primary">{row.interviewQuota}</td>
                <td className="px-4 py-3">
                  <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${row.isActive ? 'bg-success-light text-success' : 'bg-surface-3 text-text-secondary'}`}>
                    {row.isActive ? t('status.active') : t('status.inactive')}
                  </span>
                </td>
                {showColumns.subscribers && <td className="px-4 py-3 text-text-primary">{row.subscribersCount ?? '—'}</td>}
                {showColumns.revenue && <td className="px-4 py-3 text-text-primary">{formatPrice(row.revenue, locale)}</td>}
                {showColumns.updatedAt && <td className="px-4 py-3 text-text-primary">{formatDate(row.updatedAt, locale)}</td>}
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1.5">
                    <button type="button" onClick={() => onView(row.planId)} className="rounded-lg border border-border p-2 text-text-secondary hover:text-primary" aria-label={t('actions.view')}>
                      <Eye size={14} />
                    </button>
                    <button type="button" onClick={() => onEdit(row.planId)} className="rounded-lg border border-border p-2 text-text-secondary hover:text-primary" aria-label={t('actions.edit')}>
                      <Edit3 size={14} />
                    </button>
                    <button type="button" onClick={() => onDuplicate(row.planId)} className="rounded-lg border border-border p-2 text-text-secondary hover:text-primary" aria-label={t('actions.duplicate')}>
                      <Copy size={14} />
                    </button>
                    <button
                      type="button"
                      onClick={() => onToggleStatus(row.planId, !row.isActive)}
                      className="rounded-lg border border-border p-2 text-text-secondary hover:text-primary"
                      aria-label={t('actions.toggleStatus')}
                    >
                      <Power size={14} />
                    </button>
                    <button type="button" onClick={() => onDelete(row.planId)} className="rounded-lg border border-border p-2 text-text-secondary hover:text-error" aria-label={t('actions.delete')}>
                      <Trash2 size={14} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

export default SubscriptionTable;
