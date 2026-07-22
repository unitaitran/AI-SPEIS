import React from 'react';
import { Activity, CircleDollarSign, FileQuestion, Users } from 'lucide-react';

import { useTranslation } from 'react-i18next';

function AdminDashboardPage() {
  const { t } = useTranslation('admin-dashboard');

  const metrics = [
    { label: t('totalUsers', 'Total users'), value: '12,480', change: '+8.2%', icon: Users },
    { label: t('questionBank', 'Question bank'), value: '2,846', change: '+124', icon: FileQuestion },
    { label: t('monthlyRevenue', 'Monthly revenue'), value: '$28,420', change: '+12.4%', icon: CircleDollarSign },
    { label: t('aiInterviews', 'AI interviews'), value: '6,392', change: '+9.6%', icon: Activity },
  ];

  return (
    <div className="w-full animate-[fadeIn_0.5s_ease]">
      <style>{`
        @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
        @keyframes cardEntrance {
          from { opacity: 0; transform: translateY(16px); }
          to { opacity: 1; transform: translateY(0); }
        }
        .metric-card {
          animation: cardEntrance 0.5s cubic-bezier(0.16, 1, 0.3, 1) backwards;
          animation-delay: var(--delay, 0ms);
        }
      `}</style>
      <div className="mb-8">
        <div className="mb-4 flex items-center gap-2 text-xs text-text-secondary/70">
          <span>{t('breadcrumbAdmin', 'Admin')}</span>
          <span className="mx-1 text-text-disabled">/</span>
          <span aria-current="page">{t('overview', 'Overview')}</span>
        </div>

        <div className="flex flex-col items-stretch gap-8 md:flex-row md:items-start md:justify-between">
          <div className="flex-1">
            <h1 className="mb-2 text-2xl font-bold leading-[1.3] text-text-primary md:text-[32px] md:leading-[1.2]">
              {t('overview', 'Overview')}
            </h1>
            <p className="text-base leading-[1.6] text-text-secondary">
              {t('overviewDesc', 'Monitor platform activity, content, and business performance.')}
            </p>
          </div>

          <button
            className="min-h-10 w-full shrink-0 whitespace-nowrap rounded-xl bg-gradient-to-r from-primary to-primary-dark px-5 text-sm font-semibold text-white shadow-sm transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-0.5 hover:shadow-md active:scale-[0.97] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 md:w-auto"
            type="button"
          >
            {t('exportReport', 'Export report')}
          </button>
        </div>
      </div>

      <div className="flex flex-col gap-8">
        <section className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 min-[1100px]:grid-cols-4" aria-label={t('platformSummaryAria', 'Platform summary')}>
          {metrics.map(({ label, value, change, icon: Icon }, index) => (
            <article
              className="metric-card flex flex-col rounded-xl border border-border/60 bg-surface-2 p-6 shadow-[0_2px_4px_rgba(31,45,61,0.05)] transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:-translate-y-1 hover:border-primary/20 hover:shadow-[0_8px_24px_rgba(31,45,61,0.10)]"
              key={label}
              style={{ '--delay': `${index * 80}ms` }}
            >
              <div className="mb-4 flex items-center justify-between">
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-gradient-to-br from-primary-xlight to-primary-light/40 text-primary-dark transition-transform duration-300 group-hover:scale-110">
                  <Icon size={20} />
                </span>
                <span className="rounded-full bg-success-light px-2.5 py-1 text-[11px] font-semibold leading-[1.2] text-success">
                  {change}
                </span>
              </div>
              <strong className="text-2xl leading-[1.3] text-text-primary">{value}</strong>
              <span className="mt-1.5 text-sm text-text-secondary">{label}</span>
            </article>
          ))}
        </section>

        <section className="flex min-h-[220px] flex-col items-start gap-6 rounded-xl border border-border/60 bg-surface-2 p-6 shadow-[0_2px_4px_rgba(31,45,61,0.05)] transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:shadow-[0_8px_24px_rgba(31,45,61,0.10)] md:min-h-[280px] md:flex-row md:justify-between">
          <div>
            <h2 className="mb-2 text-xl leading-[1.4] text-text-primary">{t('platformActivity', 'Platform activity')}</h2>
            <p className="text-sm text-text-secondary">
              {t('platformActivityDesc', 'Operational data and charts for the selected period appear here.')}
            </p>
          </div>
          <span className="shrink-0 rounded-full bg-success-light px-3 py-1.5 text-[11px] font-semibold leading-[1.2] text-success shadow-sm">
            {t('allSystemsNormal', 'All systems normal')}
          </span>
        </section>
      </div>
    </div>
  );
}

export default AdminDashboardPage;
