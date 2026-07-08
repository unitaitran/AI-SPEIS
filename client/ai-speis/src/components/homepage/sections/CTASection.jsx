import { ChevronRight } from 'lucide-react';

function CTASection({ t }) {
  return (
    <section className="home-cta">
      <div className="home-section-shell home-cta__inner">
        <div>
          <span className="home-kicker">{t('sections.cta.kicker')}</span>
          <h2>{t('sections.cta.title')}</h2>
          <p>{t('sections.cta.text')}</p>
        </div>

        <div className="home-actions home-actions--stacked">
          <a className="home-button home-button--primary" href="#hero">
            {t('buttons.startInterview')}
            <ChevronRight size={18} />
          </a>
          <a className="home-button home-button--secondary" href="#features">
            {t('buttons.viewFeatures')}
          </a>
        </div>
      </div>
    </section>
  );
}

export default CTASection;
