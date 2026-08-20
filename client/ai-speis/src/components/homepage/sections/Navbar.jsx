import React from 'react';
import { USER_ROUTES } from '../../../routes/routePaths';
import { beginNewInterviewCampaign } from '../../../utils/interviewContext';

const navItems = [
  { key: 'home', href: '#hero' },
  { key: 'demo', href: '#demo' },
  { key: 'features', href: '#features' },
  { key: 'flow', href: '#flow' },
  { key: 'comparison', href: '#comparison' },
  { key: 'pricing', href: '#pricing' },
  { key: 'faq', href: '#faq' },
];

function Navbar({ currentHash = '', onToggleLanguage, t, i18n }) {
  const hashPath = (currentHash || '').split('?')[0];
  const currentLang = i18n?.language?.startsWith('vi') ? 'VI' : 'EN';
  const flagIcon = currentLang === 'VI' ? '🇻🇳' : '🇬🇧';

  return (
    <header className="home-navbar">
      <div className="home-navbar__inner">
        <a className="home-brand" href="#hero" aria-label={t('aria.navigation')}>
          <img src="/logo_AI-SPEIS-removebg.png" alt="AI-SPEIS Logo" className="home-brand__logo" />
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
          <button
            type="button"
            className="home-button home-button--ghost home-button--compact"
            onClick={onToggleLanguage}
            aria-label={t('aria.languageSwitch')}
            title="Chuyển ngôn ngữ / Switch Language"
          >
            <span className="text-base mr-1">{flagIcon}</span>
            <span>{currentLang}</span>
          </button>
          <a className="home-button home-button--ghost home-button--compact" href="#login">
            {t('buttons.login', 'Đăng nhập')}
          </a>
          <a
            className="home-button home-button--primary home-button--compact"
            href={USER_ROUTES.INTERVIEW_MODE}
            onClick={beginNewInterviewCampaign}
          >
            <span>{t('buttons.startInterview', 'Luyện tập ngay')}</span>
          </a>
        </div>
      </div>
    </header>
  );
}

export default Navbar;
