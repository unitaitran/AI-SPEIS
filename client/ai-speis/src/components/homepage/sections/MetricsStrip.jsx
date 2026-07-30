import React from 'react';
import { Award, CheckCircle, ShieldCheck, Users, Zap } from 'lucide-react';

function MetricsStrip({ t }) {
  const metrics = [
    {
      icon: Award,
      number: t('hero.metrics.0.number', '98.4%'),
      label: t('hero.metrics.0.label', 'Ứng viên tự tin hơn sau 3 buổi phỏng vấn')
    },
    {
      icon: Users,
      number: t('hero.metrics.1.number', '15,000+'),
      label: t('hero.metrics.1.label', 'Phiên phỏng vấn mô phỏng đã hoàn thành')
    },
    {
      icon: Zap,
      number: t('hero.metrics.2.number', '3 Phút'),
      label: t('hero.metrics.2.label', 'Nhận báo cáo phân tích Rubric Doanh nghiệp')
    },
    {
      icon: ShieldCheck,
      number: t('hero.metrics.3.number', '100%'),
      label: t('hero.metrics.3.label', 'Bảo mật CV & Bản ghi âm cá nhân')
    }
  ];

  return (
    <section className="home-metrics-strip">
      <div className="home-section-shell">
        <div className="metrics-grid">
          {metrics.map((item, index) => {
            const Icon = item.icon;
            return (
              <div className="metric-item" key={index}>
                <div className="metric-icon-box">
                  <Icon size={24} />
                </div>
                <div className="metric-info">
                  <span className="metric-number">{item.number}</span>
                  <span className="metric-label">{item.label}</span>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}

export default MetricsStrip;
