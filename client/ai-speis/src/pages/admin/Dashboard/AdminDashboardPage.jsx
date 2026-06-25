import React from 'react';
import { Activity, CircleDollarSign, FileQuestion, Users } from 'lucide-react';

function AdminDashboardPage() {
  const metrics = [
    { label: 'Total users', value: '12,480', change: '+8.2%', icon: Users },
    { label: 'Question bank', value: '2,846', change: '+124', icon: FileQuestion },
    { label: 'Monthly revenue', value: '$28,420', change: '+12.4%', icon: CircleDollarSign },
    { label: 'AI interviews', value: '6,392', change: '+9.6%', icon: Activity },
  ];

  return (
    <div className="w-full">
      <div className="mb-8">
        <div className="mb-4 flex items-center gap-2 text-xs text-text-secondary">
          <span>Admin</span>
          <span className="mx-1 text-text-disabled">/</span>
          <span aria-current="page">Overview</span>
        </div>

        <div className="flex flex-col items-stretch gap-8 md:flex-row md:items-start md:justify-between">
          <div className="flex-1">
            <h1 className="mb-2 text-2xl font-bold leading-[1.3] text-text-primary md:text-[32px] md:leading-[1.2]">
              Overview
            </h1>
            <p className="text-base leading-[1.6] text-text-secondary">
              Monitor platform activity, content, and business performance.
            </p>
          </div>

          <button
            className="min-h-10 w-full shrink-0 whitespace-nowrap rounded-lg bg-primary px-4 text-sm font-semibold text-white transition-colors duration-200 hover:bg-primary-dark focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 md:w-auto"
            type="button"
          >
            Export report
          </button>
        </div>
      </div>

      <div className="flex flex-col gap-8">
        <section className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 min-[1100px]:grid-cols-4" aria-label="Platform summary">
          {metrics.map(({ label, value, change, icon: Icon }) => (
            <article
              className="flex flex-col rounded-lg border border-border bg-surface-2 p-6 shadow-[0_2px_4px_rgba(31,45,61,0.05)]"
              key={label}
            >
              <div className="mb-4 flex items-center justify-between">
                <span className="grid h-10 w-10 place-items-center rounded-lg bg-primary-xlight text-primary-dark">
                  <Icon size={20} />
                </span>
                <span className="rounded-full bg-success-light px-2 py-1 text-[11px] font-semibold leading-[1.2] text-success">
                  {change}
                </span>
              </div>
              <strong className="text-2xl leading-[1.3] text-text-primary">{value}</strong>
              <span className="mt-1 text-sm text-text-secondary">{label}</span>
            </article>
          ))}
        </section>

        <section className="flex min-h-[220px] flex-col items-start gap-6 rounded-lg border border-border bg-surface-2 p-6 shadow-[0_2px_4px_rgba(31,45,61,0.05)] md:min-h-[280px] md:flex-row md:justify-between">
          <div>
            <h2 className="mb-2 text-xl leading-[1.4] text-text-primary">Platform activity</h2>
            <p className="text-sm text-text-secondary">
              Operational data and charts for the selected period appear here.
            </p>
          </div>
          <span className="shrink-0 rounded-full bg-success-light px-2.5 py-1.5 text-[11px] font-semibold leading-[1.2] text-success">
            All systems normal
          </span>
        </section>
      </div>
    </div>
  );
}

export default AdminDashboardPage;
