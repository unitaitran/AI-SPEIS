import React from 'react';
import { ArrowRight, Sparkles } from 'lucide-react';
import { USER_ROUTES } from '../../../routes/routePaths';
import { beginNewInterviewCampaign } from '../../../utils/interviewContext';

function CTASection({ t }) {
  return (
    <section className="home-cta-banner-section" id="cta">
      <div className="home-section-shell">
        <div className="cta-banner-card glow-card">
          <div className="cta-content">
            <span className="home-kicker kicker-white">
              <Sparkles size={14} className="mr-1" />
              <span>{t('cta.kicker', 'BẮT ĐẦU NGAY')}</span>
            </span>
            <h2>{t('ctaBanner.title', 'Sẵn sàng chinh phục buổi phỏng vấn mơ ước?')}</h2>
            <p>{t('ctaBanner.subtitle', 'Tải CV lên và bắt đầu phiên phỏng vấn mô phỏng đầu tiên của bạn chỉ trong 60 giây.')}</p>
          </div>
          <div className="cta-actions">
            <a
              className="home-button home-button--primary home-button--large"
              href={USER_ROUTES.INTERVIEW_MODE}
              onClick={beginNewInterviewCampaign}
            >
              <span>{t('ctaBanner.button', 'Bắt đầu luyện tập ngay')}</span>
              <ArrowRight size={20} />
            </a>
          </div>
        </div>
      </div>
    </section>
  );
}

export default CTASection;
