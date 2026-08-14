import React from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronLeft, ChevronRight } from 'lucide-react';

function SubscriptionPagination({ currentPage, totalPages, onPageChange }) {
  const { t } = useTranslation('admin-subscription');

  if (totalPages <= 1) {
    return null;
  }

  return (
    <div className="flex items-center justify-end gap-2">
      <button
        type="button"
        disabled={currentPage <= 1}
        onClick={() => onPageChange(currentPage - 1)}
        className="inline-flex h-9 items-center justify-center rounded-lg border border-border bg-white px-3 text-sm text-text-secondary disabled:opacity-50"
      >
        <ChevronLeft size={16} />
      </button>

      <span className="text-sm text-text-secondary">
        {t('pagination.page', { current: currentPage, total: totalPages })}
      </span>

      <button
        type="button"
        disabled={currentPage >= totalPages}
        onClick={() => onPageChange(currentPage + 1)}
        className="inline-flex h-9 items-center justify-center rounded-lg border border-border bg-white px-3 text-sm text-text-secondary disabled:opacity-50"
      >
        <ChevronRight size={16} />
      </button>
    </div>
  );
}

export default SubscriptionPagination;
