import { Globe } from 'lucide-react';

const navItems = [
  { key: 'home', href: '#hero' },
  { key: 'features', href: '#features' },
  { key: 'flow', href: '#flow' },
  { key: 'pricing', href: '#pricing' },
  { key: 'faq', href: '#faq' },
];

function Navbar({ currentHash = '', onToggleLanguage, t }) {
  const hashPath = (currentHash || '').split('?')[0];

  return (
    <header className="home-navbar">
      <div className="home-navbar__inner">
        <a className="home-brand" href="#hero" aria-label={t('aria.navigation')}>
          <img src="/logo_AI-SPEIS-removebg.png" alt={t('meta.title')} className="home-brand__logo" />
        </a>

        <nav className="home-nav" aria-label={t('aria.navigation')}>
          {navItems.map(({ key, href }) => {
            const isActive = href === '#hero'
              ? hashPath === '' || hashPath === '#hero' || hashPath === '#'
              : hashPath === href;

            return (
              <a key={key} className={`home-nav__link${isActive ? ' is-active' : ''}`} href={href}>
                {t(`nav.${key}`)}
              </a>
            );
          })}
        </nav>

        <div className="home-navbar__actions">
          <button type="button" className="home-button home-button--ghost home-button--compact" onClick={onToggleLanguage} aria-label={t('aria.languageSwitch')}>
            <Globe size={18} />
            <span>{t('buttons.language')}</span>
          </button>
          <a className="home-button home-button--ghost home-button--compact" href="#login">
            {t('buttons.login')}
          </a>
          <a className="home-button home-button--primary home-button--compact" href="#register">
            {t('buttons.register')}
          </a>
        </div>
      </div>
    </header>
  );
}

export default Navbar;
