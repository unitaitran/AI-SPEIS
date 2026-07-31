import React from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, PackageOpen } from 'lucide-react';

function SubscriptionEmptyState({ onCreate }) {
  const { t } = useTranslation('admin-subscription');

  return (
    <section className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-border bg-surface-2 px-6 py-12 text-center">
      <span className="grid h-14 w-14 place-items-center rounded-full bg-primary-xlight text-primary-dark">
        <PackageOpen size={24} />
      </span>
      <h3 className="mt-4 text-lg font-semibold text-text-primary">{t('emptyState.title')}</h3>
      <p className="mt-2 max-w-xl text-sm text-text-secondary">{t('emptyState.description')}</p>
      <button
        type="button"
        onClick={onCreate}
        className="mt-5 inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-semibold text-white hover:bg-primary-dark"
      >
        <Plus size={16} />
        {t('emptyState.createButton')}
      </button>
    </section>
  );
}

export default SubscriptionEmptyState;
