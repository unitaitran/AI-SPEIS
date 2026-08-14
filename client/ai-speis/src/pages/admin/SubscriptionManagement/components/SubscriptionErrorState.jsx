import React from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle, RefreshCw } from 'lucide-react';

function SubscriptionErrorState({ message, onRetry }) {
  const { t } = useTranslation('admin-subscription');

  return (
    <section className="flex flex-col items-center justify-center rounded-2xl border border-error/40 bg-error-light px-6 py-12 text-center">
      <span className="grid h-14 w-14 place-items-center rounded-full bg-white text-error">
        <AlertCircle size={24} />
      </span>
      <h3 className="mt-4 text-lg font-semibold text-text-primary">{t('errorState.title')}</h3>
      <p className="mt-2 max-w-xl text-sm text-text-secondary">{message || t('errorState.defaultMessage')}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-5 inline-flex items-center gap-2 rounded-xl border border-error/30 bg-white px-4 py-2.5 text-sm font-semibold text-error hover:bg-error-light"
      >
        <RefreshCw size={16} />
        {t('errorState.retry')}
      </button>
    </section>
  );
}

export default SubscriptionErrorState;
