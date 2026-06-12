import { useEffect, useState } from 'react';
import {
  BarChart3,
  Bell,
  Bot,
  ChevronRight,
  FileText,
  Flame,
  Globe,
  GraduationCap,
  LayoutDashboard,
  LayoutList,
  Lock,
  Mic,
  Search,
  Star,
  Ticket,
  Trophy,
  Upload,
  Users,
  Volume2,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';

import './styles/reset.css';
import './styles/variables.css';
import './styles/globals.css';
import './App.css';
import LoginPage from './pages/authen/LoginPage';
import RegisterPage from './pages/authen/RegisterPage';
import ForgotPasswordPage from './pages/authen/ForgotPasswordPage';

const navKeys = ['home', 'features', 'flow', 'personalization', 'community'];
const navHrefs = ['#hero', '#features', '#flow', '#personalization', '#community'];

const featureIcons = [Upload, Bot, FileText, BarChart3, Trophy, Users];
const communityIcons = [Bell, Star, Flame, GraduationCap];
const personalizationRowIcons = [LayoutDashboard, LayoutList, Search];
const toolIcons = [Mic, Volume2, FileText, Bell, Lock];

function App() {
  const { t, i18n } = useTranslation('landing');
  const [currentHash, setCurrentHash] = useState(window.location.hash);

  useEffect(() => {
    const onHashChange = () => setCurrentHash(window.location.hash);
    window.addEventListener('hashchange', onHashChange);
    return () => window.removeEventListener('hashchange', onHashChange);
  }, []);

  useEffect(() => {
    document.documentElement.lang = i18n.language === 'vi' ? 'vi' : 'en';
    document.title = t('meta.title');
  }, [i18n.language, t]);

  const heroCards = t('hero.cards', { returnObjects: true });
  const highlights = t('highlights', { returnObjects: true });
  const featureCards = t('sections.features.cards', { returnObjects: true });
  const flowSteps = t('sections.flow.steps', { returnObjects: true });
  const personalizationRows = t('sections.personalization.rows', { returnObjects: true });
  const communityCards = t('sections.community.cards', { returnObjects: true });
  const tools = t('sections.interviewRoom.tools', { returnObjects: true });

  const toggleLanguage = () => {
    i18n.changeLanguage(i18n.language === 'vi' ? 'en' : 'vi');
  };

  if (currentHash === '#login') return <LoginPage />;
  if (currentHash === '#register') return <RegisterPage />;
  if (currentHash === '#forgot-password') return <ForgotPasswordPage />;

  return (
    <div className="landing-shell">
      <header className="topbar">
        <div className="topbar-inner">
          <a className="brand" href="#hero" aria-label="AI-SPEIS homepage">
            <img src="/logo_AI-SPEIS-removebg.png" alt="AI-SPEIS logo" className="brand-logo" />
          </a>

          <nav className="desktop-nav" aria-label={t('aria.navigation')}>
            {navKeys.map((key, index) => (
              <a
                key={key}
                className={index === 0 ? 'nav-link active' : 'nav-link'}
                href={navHrefs[index]}
              >
                {t(`nav.${key}`)}
              </a>
            ))}
          </nav>

          <div className="topbar-actions">
            <button
              type="button"
              className="ghost-button language-button"
              onClick={toggleLanguage}
              aria-label={t('aria.languageSwitch')}
            >
              <Globe size={18} />
              <span>{i18n.language === 'vi' ? 'VI / EN' : 'EN / VI'}</span>
            </button>
            <a className="primary-button subtle" href="#login">
              {t('buttons.login')}
            </a>
          </div>
        </div>
      </header>

      <main className="page-content">
        <section className="hero-section" id="hero">
          <div className="hero-copy">
            <span className="eyebrow">{t('hero.tag')}</span>
            <h1>{t('hero.title')}</h1>
            <p className="hero-text">{t('hero.text')}</p>

            <div className="hero-actions">
              <a className="primary-button" href="#flow">
                {t('buttons.startInterview')}
                <ChevronRight size={18} />
              </a>
              <a className="secondary-button" href="#features">
                {t('buttons.howItWorks')}
              </a>
            </div>

            <div className="hero-insights">
              {heroCards.map((item) => (
                <div className="insight-card" key={item.label}>
                  <span>{item.label}</span>
                  <strong>{item.value}</strong>
                </div>
              ))}
            </div>
          </div>

          <div className="hero-visual">
            <div className="mascot-stage">
              <div className="mascot-card">
                <div className="mascot-header">
                  <span className="status-dot" />
                  <span>{t('mascot.status')}</span>
                </div>
                <img src="/mascot_AI-SPEIS-removebg.png" alt="AI-SPEIS mascot" className="mascot-image" />
                <div className="mascot-note">
                  <Bot size={18} />
                  <span>{t('mascot.note')}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="trust-strip" aria-label={t('aria.highlights')}>
          {highlights.map((item) => (
            <div key={item.title}>
              <strong>{item.title}</strong>
              <span>{item.text}</span>
            </div>
          ))}
        </section>

        <section className="section" id="features">
          <div className="section-heading">
            <span className="section-kicker">{t('sections.features.kicker')}</span>
            <h2>{t('sections.features.title')}</h2>
            <p>{t('sections.features.text')}</p>
          </div>

          <div className="feature-grid">
            {featureCards.map((card, index) => {
              const Icon = featureIcons[index] || Upload;
              return (
                <article key={card.title} className="feature-card">
                  <div className="icon-badge">
                    <Icon size={20} />
                  </div>
                  <h3>{card.title}</h3>
                  <p>{card.description}</p>
                </article>
              );
            })}
          </div>
        </section>

        <section className="section spotlight-section" id="flow">
          <div className="section-heading narrow">
            <span className="section-kicker">{t('sections.flow.kicker')}</span>
            <h2>{t('sections.flow.title')}</h2>
            <p>{t('sections.flow.text')}</p>
          </div>

          <div className="timeline">
            {flowSteps.map((step, index) => (
              <div className="timeline-item" key={step.title}>
                <div className="timeline-step">0{index + 1}</div>
                <div className="timeline-content">
                  <h3>{step.title}</h3>
                  <p>{step.text}</p>
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="section split-section" id="personalization">
          <div className="panel card-panel">
            <span className="section-kicker">{t('sections.personalization.kicker')}</span>
            <h2>{t('sections.personalization.title')}</h2>
            <p>{t('sections.personalization.text')}</p>

            <div className="stacked-list">
              {personalizationRows.map((row, index) => {
                const Icon = personalizationRowIcons[index] || LayoutDashboard;
                return (
                  <div className="stacked-row" key={row}>
                    <Icon size={18} />
                    <span>{row}</span>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="panel community-panel" id="community">
            <span className="section-kicker">{t('sections.community.kicker')}</span>
            <h2>{t('sections.community.title')}</h2>

            <div className="community-grid">
              {communityCards.map((item, index) => {
                const Icon = communityIcons[index] || Bell;
                return (
                  <article key={item.title} className="mini-card">
                    <div className="mini-icon">
                      <Icon size={18} />
                    </div>
                    <h3>{item.title}</h3>
                    <p>{item.text}</p>
                  </article>
                );
              })}
            </div>

            <div className="reward-row">
              <div className="reward-pill success">
                <Trophy size={16} />
                <span>{t('sections.community.pills.achievement')}</span>
              </div>
              <div className="reward-pill info">
                <Ticket size={16} />
                <span>{t('sections.community.pills.voucher')}</span>
              </div>
              <div className="reward-pill warning">
                <BarChart3 size={16} />
                <span>{t('sections.community.pills.progress')}</span>
              </div>
            </div>
          </div>
        </section>

        <section className="section compact-section" id="tools">
          <div className="tools-card">
            <div className="tools-copy">
              <span className="section-kicker">{t('sections.interviewRoom.kicker')}</span>
              <h2>{t('sections.interviewRoom.title')}</h2>
              <p>{t('sections.interviewRoom.text')}</p>
            </div>

            <div className="tools-tags" aria-label={t('aria.tools')}>
              {tools.map((tool, index) => {
                const Icon = toolIcons[index] || Mic;
                return (
                  <span key={tool}>
                    <Icon size={16} /> {tool}
                  </span>
                );
              })}
            </div>
          </div>
        </section>

        <section className="cta-section">
          <div>
            <span className="section-kicker">{t('sections.cta.kicker')}</span>
            <h2>{t('sections.cta.title')}</h2>
            <p>{t('sections.cta.text')}</p>
          </div>

          <div className="cta-actions">
            <a className="primary-button" href="#hero">
              {t('buttons.startInterview')}
              <ChevronRight size={18} />
            </a>
            <a className="ghost-button" href="#features">
              {t('buttons.viewFeatures')}
            </a>
          </div>
        </section>
      </main>
    </div>
  );
}

export default App;
