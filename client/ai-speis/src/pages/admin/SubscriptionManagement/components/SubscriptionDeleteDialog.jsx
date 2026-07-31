import React from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle } from 'lucide-react';

function SubscriptionDeleteDialog({ open, plan, isDeleting, onCancel, onConfirm }) {
  const { t } = useTranslation('admin-subscription');

  if (!open || !plan) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-[150] flex items-center justify-center bg-text-primary/30 p-4 backdrop-blur-sm" role="dialog" aria-modal="true">
      <div className="w-full max-w-md rounded-2xl border border-border bg-white p-6 shadow-[0_18px_36px_rgba(31,45,61,0.18)]">
        <div className="flex items-start gap-3">
          <span className="mt-0.5 grid h-10 w-10 place-items-center rounded-xl bg-warning-light text-warning">
            <AlertTriangle size={20} />
          </span>
          <div>
            <h3 className="text-lg font-semibold text-text-primary">{t('deleteDialog.title')}</h3>
            <p className="mt-1 text-sm text-text-secondary">
              {t('deleteDialog.message')}
            </p>
          </div>
        </div>

        <div className="mt-5 rounded-xl border border-border bg-surface-1 p-3">
          <p className="text-xs text-text-secondary">{t('deleteDialog.planLabel')}</p>
          <p className="text-sm font-semibold text-text-primary">{plan.code} - {plan.name}</p>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-xl border border-border bg-white px-4 py-2 text-sm font-medium text-text-secondary"
          >
            {t('deleteDialog.close')}
          </button>
          <button
            type="button"
            disabled={isDeleting}
            onClick={onConfirm}
            className="rounded-xl bg-error px-4 py-2 text-sm font-semibold text-white disabled:opacity-60"
          >
            {t('deleteDialog.confirm')}
          </button>
        </div>
      </div>
    </div>
  );
}

export default SubscriptionDeleteDialog;
