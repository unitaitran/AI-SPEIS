import React from 'react';
import { useTranslation } from 'react-i18next';
import { Activity, BadgeDollarSign, Crown, Layers, Percent, WalletCards } from 'lucide-react';

function formatCurrency(value, locale) {
  if (value === null || value === undefined) return '—';
  const amount = Number(value);
  if (!Number.isFinite(amount)) return '—';
  return `${amount.toLocaleString(locale)} VND`;
}

function formatNumber(value, locale) {
  if (value === null || value === undefined) return '—';
  const amount = Number(value);
  if (!Number.isFinite(amount)) return '—';
  return amount.toLocaleString(locale);
}

function SubscriptionKPICards({ plans, monitoring }) {
  const { t, i18n } = useTranslation('admin-subscription');
  const locale = i18n.language === 'vi' ? 'vi-VN' : 'en-US';

  const activePlans = plans.filter((plan) => plan.isActive).length;
  const paidUsers = monitoring?.activePaidUsers ?? null;
  const totalQuota = monitoring?.quota?.totalQuota ?? null;
  const monthlyRevenue = monitoring?.monthlyRevenueVnd ?? monitoring?.payments?.monthlyRevenueVnd ?? null;
  const annualRevenue = monitoring?.annualRevenueVnd ?? monitoring?.payments?.annualRevenueVnd ?? null;
  const conversionRate = monitoring?.conversionRate ?? null;

  const cards = [
    {
      key: 'activePlans',
      label: t('kpi.activePlans'),
      value: formatNumber(activePlans, locale),
      icon: Layers,
      tone: 'from-primary-light/50 to-primary-xlight',
    },
    {
      key: 'paidUsers',
      label: t('kpi.paidUsers', 'Người dùng gói trả phí'),
      value: formatNumber(paidUsers, locale),
      icon: Crown,
      tone: 'from-warning-light to-warning-light/40',
    },
    {
      key: 'monthlyRevenue',
      label: t('kpi.monthlyRevenue'),
      value: formatCurrency(monthlyRevenue, locale),
      icon: WalletCards,
      tone: 'from-success-light to-success-light/40',
    },
    {
      key: 'annualRevenue',
      label: t('kpi.annualRevenue'),
      value: formatCurrency(annualRevenue, locale),
      icon: BadgeDollarSign,
      tone: 'from-info-light to-info-light/40',
    },
    {
      key: 'totalQuota',
      label: t('kpi.totalQuota'),
      value: formatNumber(totalQuota, locale),
      icon: Activity,
      tone: 'from-surface-3 to-surface-2',
    },
    {
      key: 'conversionRate',
      label: t('kpi.conversionRate'),
      value: conversionRate === null || conversionRate === undefined ? '—' : `${conversionRate}%`,
      icon: Percent,
      tone: 'from-surface-3 to-surface-2',
    },
  ];

  return (
    <section className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3" aria-label="Subscription KPIs">
      {cards.map((card, index) => {
        const Icon = card.icon;

        return (
          <article
            key={card.key}
            className="rounded-2xl border border-border/60 bg-surface-2 p-5 shadow-[0_2px_8px_rgba(31,45,61,0.05)]"
            style={{ animation: `cardEntrance 0.35s ease ${index * 70}ms both` }}
          >
            <div className="flex items-center justify-between">
              <p className="text-xs font-semibold uppercase tracking-[0.08em] text-text-secondary">{card.label}</p>
              <span className={`grid h-10 w-10 place-items-center rounded-xl bg-gradient-to-br ${card.tone} text-primary-dark`}>
                <Icon size={20} />
              </span>
            </div>
            <p className="mt-3 text-2xl font-bold text-text-primary">{card.value}</p>
          </article>
        );
      })}
    </section>
  );
}

export default SubscriptionKPICards;
