function Footer({ t }) {
  return (
    <footer className="home-footer">
      <div className="home-section-shell home-footer__inner">
        <div>
          <h4>{t('meta.title')}</h4>
          <p>{t('sections.cta.text')}</p>
        </div>
        <div>
          <h5>{t('footer.about')}</h5>
          <a href="#features">{t('footer.features')}</a>
          <a href="#faq">{t('footer.support')}</a>
        </div>
        <div>
          <h5>{t('footer.contact')}</h5>
          <span>{t('footer.social')}</span>
        </div>
      </div>
    </footer>
  );
}

export default Footer;
