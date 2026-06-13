import React from 'react';
import { Activity, CircleDollarSign, FileQuestion, Users } from 'lucide-react';
import './AdminDashboardPage.css';

function AdminDashboardPage() {
  const metrics = [
    { label: 'Total users', value: '12,480', change: '+8.2%', icon: Users },
    { label: 'Question bank', value: '2,846', change: '+124', icon: FileQuestion },
    { label: 'Monthly revenue', value: '$28,420', change: '+12.4%', icon: CircleDollarSign },
    { label: 'AI interviews', value: '6,392', change: '+9.6%', icon: Activity },
  ];

  return (
    <div className="admin-dashboard-page">
      <div className="page-header">
        <div className="breadcrumb">
          <span>Admin</span>
          <span className="separator">/</span>
          <span aria-current="page">Overview</span>
        </div>

        <div className="header-top">
          <div className="title-section">
            <h1 className="page-title">Overview</h1>
            <p className="page-description">
              Monitor platform activity, content, and business performance.
            </p>
          </div>

          <button className="btn-primary" type="button">Export report</button>
        </div>
      </div>

      <div className="page-content">
        <section className="metric-grid" aria-label="Platform summary">
          {metrics.map(({ label, value, change, icon: Icon }) => (
            <article className="metric-card" key={label}>
              <div className="metric-card-top">
                <span className="metric-icon"><Icon size={20} /></span>
                <span className="metric-change">{change}</span>
              </div>
              <strong className="metric-value">{value}</strong>
              <span className="metric-label">{label}</span>
            </article>
          ))}
        </section>

        <section className="dashboard-panel">
          <div>
            <h2>Platform activity</h2>
            <p>Operational data and charts for the selected period appear here.</p>
          </div>
          <span className="status-pill">All systems normal</span>
        </section>
      </div>
    </div>
  );
}

export default AdminDashboardPage;
