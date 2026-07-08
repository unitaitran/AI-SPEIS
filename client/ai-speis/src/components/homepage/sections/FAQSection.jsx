function FAQSection({ faqItems = [], t }) {
  return (
    <section className="home-section" id="faq">
      <div className="home-section-shell">
        <div className="home-section-heading">
          <span className="home-kicker">{t('sections.faq.kicker')}</span>
          <h2>{t('sections.faq.title')}</h2>
        </div>

        <div className="home-faq-list">
          {faqItems.map((item, index) => (
            <details className="home-faq-item" key={item.q || index}>
              <summary>{item.q}</summary>
              <div>{item.a}</div>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

export default FAQSection;
