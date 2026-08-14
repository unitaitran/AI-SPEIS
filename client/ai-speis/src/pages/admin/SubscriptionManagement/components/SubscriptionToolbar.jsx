import React from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, RefreshCw, Search } from 'lucide-react';

function SubscriptionToolbar({
  filters,
  sortBy,
  onFilterChange,
  onSortChange,
  onCreate,
  onRefresh,
  isRefreshing,
}) {
  const { t } = useTranslation('admin-subscription');

  return (
    <section className="rounded-2xl border border-border/60 bg-surface-2 p-4 md:p-5">
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-[1fr_auto_auto_auto_auto_auto]">
        <label className="relative flex items-center">
          <Search size={18} className="pointer-events-none absolute left-3 text-text-secondary" />
          <input
            type="text"
            value={filters.search}
            onChange={(event) => onFilterChange('search', event.target.value)}
            placeholder={t('toolbar.searchPlaceholder')}
            className="w-full rounded-xl border border-border bg-white pl-10 pr-3 py-2.5 text-sm text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
          />
        </label>

        <select
          value={filters.status}
          onChange={(event) => onFilterChange('status', event.target.value)}
          className="rounded-xl border border-border bg-white px-3 py-2.5 text-sm text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
        >
          <option value="all">{t('toolbar.allStatus')}</option>
          <option value="active">{t('toolbar.statusActive')}</option>
          <option value="inactive">{t('toolbar.statusInactive')}</option>
        </select>

        <select
          value={filters.billingCycle}
          onChange={(event) => onFilterChange('billingCycle', event.target.value)}
          className="rounded-xl border border-border bg-white px-3 py-2.5 text-sm text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
        >
          <option value="all">{t('toolbar.allBillingCycles')}</option>
          <option value="1">{t('toolbar.cycleMonthly')}</option>
          <option value="2">{t('toolbar.cycleYearly')}</option>
        </select>

        <select
          value={filters.currency}
          onChange={(event) => onFilterChange('currency', event.target.value)}
          className="rounded-xl border border-border bg-white px-3 py-2.5 text-sm text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
        >
          <option value="all">{t('toolbar.allCurrencies')}</option>
          <option value="VND">VND</option>
          <option value="USD">USD</option>
        </select>

        <select
          value={sortBy}
          onChange={(event) => onSortChange(event.target.value)}
          className="rounded-xl border border-border bg-white px-3 py-2.5 text-sm text-text-primary outline-none focus:border-primary focus:ring-4 focus:ring-primary-xlight"
        >
          <option value="name_asc">{t('toolbar.sortNameAsc')}</option>
          <option value="name_desc">{t('toolbar.sortNameDesc')}</option>
          <option value="price_asc">{t('toolbar.sortPriceAsc')}</option>
          <option value="price_desc">{t('toolbar.sortPriceDesc')}</option>
          <option value="updated_desc">{t('toolbar.sortUpdatedDesc')}</option>
          <option value="updated_asc">{t('toolbar.sortUpdatedAsc')}</option>
        </select>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={onRefresh}
            className="grid h-10 w-10 place-items-center rounded-xl border border-border bg-white text-text-secondary hover:text-primary"
            aria-label={t('toolbar.refresh')}
          >
            <RefreshCw size={18} className={isRefreshing ? 'animate-spin' : ''} />
          </button>
          <button
            type="button"
            onClick={onCreate}
            className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl bg-primary px-4 text-sm font-semibold text-white hover:bg-primary-dark"
          >
            <Plus size={16} />
            {t('toolbar.createPlan')}
          </button>
        </div>
      </div>
    </section>
  );
}

export default SubscriptionToolbar;
