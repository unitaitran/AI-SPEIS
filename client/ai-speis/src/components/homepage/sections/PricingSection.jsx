function PricingSection({ t }) {
  return (
    <section className="home-section home-section--surface" id="pricing">
      <div className="home-section-shell">
        <div className="home-section-heading">
          <span className="home-kicker">{t('sections.pricing.kicker', 'Pricing')}</span>
          <h2>{t('sections.pricing.title', 'Plans for learners')}</h2>
          <p>{t('sections.pricing.text', 'Choose a plan to unlock more practice features.')}</p>
        </div>

        <div className="home-pricing-grid">
          <article className="home-pricing-card">
            <h3>{t('sections.pricing.freeTitle', 'Free')}</h3>
            <p>{t('sections.pricing.free')}</p>
            <a className="home-button home-button--secondary home-button--full" href="#register">{t('buttons.createAccount')}</a>
          </article>
          <article className="home-pricing-card home-pricing-card--featured">
            <h3>{t('sections.pricing.proTitle', 'Pro')}</h3>
            <p>{t('sections.pricing.pro')}</p>
            <a className="home-button home-button--primary home-button--full" href="#register">{t('buttons.createAccount')}</a>
          </article>
          <article className="home-pricing-card">
            <h3>{t('sections.pricing.enterpriseTitle', 'Enterprise')}</h3>
            <p>{t('sections.pricing.enterprise')}</p>
            <button className="home-button home-button--secondary home-button--full home-button--disabled" type="button" aria-disabled="true">{t('buttons.contactSales')}</button>
          </article>
        </div>
      </div>
    </section>
  );
}

export default PricingSection;
