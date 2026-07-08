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
  X,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';

import './styles/reset.css';
import './styles/variables.css';
import './styles/globals.css';
import './App.css';
import Button from './components/UI/Button';
import LoginPage from './pages/authen/LoginPage';
import RegisterPage from './pages/authen/RegisterPage';
import ForgotPasswordPage from './pages/authen/ForgotPasswordPage';
import { getStoredSession, getDefaultRouteForRole } from './routes/auth';
import { navigate } from './routes/navigation';

const navKeys = ['home', 'features', 'flow', 'personalization', 'community'];
const navHrefs = ['#hero', '#features', '#flow', '#personalization', '#community'];

const featureIcons = [Upload, Bot, FileText, BarChart3, Trophy, Users];
const communityIcons = [Bell, Star, Flame, GraduationCap];
const personalizationRowIcons = [LayoutDashboard, LayoutList, Search];
const toolIcons = [Mic, Volume2, FileText, Bell, Lock];

function AuthRedirect() {
  useEffect(() => {
    const session = getStoredSession();
    if (session) {
      navigate(getDefaultRouteForRole(session.user.role), { replace: true });
    }
  }, []);
  return null;
}

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

  const session = getStoredSession();
  const hashPath = currentHash.split('?')[0];
  const isAuthRoute = hashPath === '#login' || hashPath === '#register' || hashPath === '#forgot-password';

  // Parse query params to detect OAuth login error when user is already logged in
  const queryString = currentHash.includes('?') ? currentHash.split('?')[1] : '';
  const urlParams = new URLSearchParams(queryString);
  const isLoginError = hashPath === '#login' && urlParams.get('status') === 'error';

  // State to control popup
  const [showLoggedInPopup, setShowLoggedInPopup] = useState(false);
  const [countdown, setCountdown] = useState(5);

  useEffect(() => {
    if (session && isLoginError) {
      setShowLoggedInPopup(true);
      // Clean query params to prevent showing again on refresh
      window.history.replaceState(null, '', window.location.pathname);
      setCurrentHash('');
    }
  }, [session, isLoginError]);

  // Countdown timer for automatic redirect
  useEffect(() => {
    if (!showLoggedInPopup) return;

    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          clearInterval(timer);
          navigate(getDefaultRouteForRole(session?.user?.role), { replace: true });
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [showLoggedInPopup, session]);

  const handleGoToDashboard = () => {
    setShowLoggedInPopup(false);
    navigate(getDefaultRouteForRole(session?.user?.role), { replace: true });
  };

  // Auth guard redirects:
  // If user is logged in, and tries to access auth routes, and it is NOT an OAuth login error, redirect immediately.
  if (isAuthRoute && session && !isLoginError) {
    return <AuthRedirect />;
  }

  // If user is not logged in, render auth pages normally
  if (!session) {
    if (hashPath === '#login') return <LoginPage />;
    if (hashPath === '#register') return <RegisterPage />;
    if (hashPath === '#forgot-password') return <ForgotPasswordPage />;
  }

  return (
    <div className="landing-shell">
      <header className="topbar">
        <div className="topbar-inner">
          <a className="brand" href="#hero" aria-label="AI-SPEIS homepage">
            <img src="/logo_AI-SPEIS-removebg.png" alt="AI-SPEIS logo" className="brand-logo" />
          </a>

          <nav className="desktop-nav" aria-label={t('aria.navigation')}>
            {navKeys.map((key, index) => {
              const href = navHrefs[index];
              const normalizedHash = currentHash.split('?')[0] || '#hero';
              const isActive = normalizedHash === href || (normalizedHash === '' && href === '#hero');
              return (
                <a
                  key={key}
                  className={`nav-link ${isActive ? 'active' : ''}`}
                  href={href}
                >
                  {t(`nav.${key}`)}
                </a>
              );
            })}
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
            <button
              type="button"
              className="hidden md:inline-block"
              onClick={() => (window.location.hash = '#login')}
            >
              <Button className="px-3 py-1.5 min-h-9 w-auto">{t('buttons.login')}</Button>
            </button>
          </div>
        </div>
      </header>

      <main className="page-content">

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

        <section className="hero-section" id="hero">
          <div className="mx-auto max-w-[1200px] grid grid-cols-1 md:grid-cols-12 gap-6 items-center py-12">
            <div className="md:col-span-7 lg:col-span-6 px-4 md:px-0">
              <span className="text-sm font-semibold tracking-wide text-primary uppercase block mb-3">{t('hero.tag')}</span>
              <h1 className="text-h1 font-extrabold text-text-primary leading-tight mb-4">{t('hero.title')}</h1>
              <p className="text-body text-text-secondary mb-6">{t('hero.text')}</p>

              <div className="flex flex-col sm:flex-row gap-3 sm:items-center">
                <Button
                  type="button"
                  className="inline-flex items-center gap-2 px-5 py-3"
                  onClick={() => (window.location.hash = '#flow')}
                >
                  {t('buttons.startInterview')}
                  <ChevronRight size={18} />
                </Button>

                <button
                  type="button"
                  className="inline-flex items-center gap-2 px-4 py-3 rounded-button bg-surface-2 text-text-primary border border-border"
                  onClick={() => (window.location.hash = '#features')}
                >
                  {t('buttons.howItWorks')}
                </button>
              </div>

              {Array.isArray(heroCards) && heroCards.length > 0 && (
                <div className="mt-8 grid grid-cols-2 sm:grid-cols-3 gap-3">
                  {heroCards.map((card) => (
                    <div key={card.label} className="flex flex-col">
                      <span className="text-sm text-text-secondary">{card.label}</span>
                      <strong className="text-lg text-text-primary">{card.value}</strong>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="md:col-span-5 lg:col-span-6 px-4 md:px-0 flex justify-center">
              <div className="max-w-[520px] w-full">
                <div className="bg-surface-2 border border-border rounded-2xl p-6 shadow-card">
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <span className="h-3 w-3 rounded-full bg-primary inline-block" />
                      <span className="text-sm text-text-secondary">{t('mascot.status')}</span>
                    </div>
                  </div>
                  <img src="/mascot_AI-SPEIS-removebg.png" alt={t('mascot.alt', 'AI-SPEIS mascot')} className="w-full h-auto object-contain" />
                  {t('mascot.note') && (
                    <div className="mt-3 text-sm text-text-secondary flex items-center gap-2">
                      <Bot size={16} />
                      <span>{t('mascot.note')}</span>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </section>
          {showLoggedInPopup && (
            <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm px-4">
              <div className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full mx-4 text-center">
                <div className="relative w-40 h-40 mx-auto mb-6 group">
                  <div className="absolute inset-0 bg-primary-light/10 rounded-full blur-2xl group-hover:bg-primary-light/20 transition-all duration-500" />
                  <img
                    src="/confuse.png"
                    alt="Confused mascot"
                    className="relative w-full h-full object-contain transform group-hover:scale-105 group-hover:rotate-2 transition-transform duration-500 ease-out"
                  />
                </div>

                <h3 className="text-xl font-bold text-text-primary mb-3">
                  {i18n.language === 'vi' ? 'Bạn đã đăng nhập rồi!' : 'You are already logged in!'}
                </h3>

                <p className="text-text-secondary text-sm leading-relaxed mb-6">
                  {i18n.language === 'vi'
                    ? 'Hệ thống phát hiện bạn đã đăng nhập và có phiên làm việc hoạt động. Tự động chuyển hướng về trang điều khiển sau '
                    : 'The system detected that you are already logged in with an active session. Redirecting to your dashboard in '}
                  <span className="font-bold text-primary-dark text-base px-2 py-0.5 rounded-md bg-primary-xlight inline-block min-w-[28px] animate-pulse">
                    {countdown}
                  </span>
                  {i18n.language === 'vi' ? ' giây.' : ' seconds.'}
                </p>

                <button
                  onClick={handleGoToDashboard}
                  className="w-full py-3.5 px-6 rounded-xl font-bold text-white bg-gradient-to-r from-primary to-primary-dark hover:from-primary-dark hover:to-primary shadow-lg hover:shadow-primary/30 transform hover:-translate-y-0.5 transition-all duration-300 flex items-center justify-center gap-2 group cursor-pointer"
                >
                  <span>{i18n.language === 'vi' ? 'Đi tới Dashboard ngay' : 'Go to Dashboard now'}</span>
                  <ChevronRight size={18} className="transform group-hover:translate-x-1 transition-transform duration-300" />
                </button>
              </div>
            </div>
          )}
          </main>
            </div>
  );
}

export default App;
