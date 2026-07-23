import { useEffect, useState } from 'react';
import { ChevronRight, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import './styles/reset.css';
import './styles/variables.css';
import './styles/globals.css';
import './App.css';
import LoginPage from './pages/authen/LoginPage';
import RegisterPage from './pages/authen/RegisterPage';
import ForgotPasswordPage from './pages/authen/ForgotPasswordPage';
import ResetPasswordPage from './pages/authen/ResetPasswordPage';
import HomePage from './components/homepage/HomePage';
import { getStoredSession, getDefaultRouteForRole } from './routes/auth';
import { navigate } from './routes/navigation';

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
  const { t, i18n } = useTranslation('homepage');
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

  // Reset password route can be accessed with token in URL hash
  if (hashPath === '#reset-password') {
    return <ResetPasswordPage />;
  }

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
    <>
      <HomePage currentHash={currentHash} onToggleLanguage={toggleLanguage} />

      {showLoggedInPopup && (
        <div className="homepage-popup-overlay" onClick={handleGoToDashboard}>
          <div className="homepage-popup" onClick={(e) => e.stopPropagation()}>
            <div className="homepage-popup__bar" />
            <button onClick={handleGoToDashboard} className="homepage-popup__close" aria-label="Close">
              <X size={18} />
            </button>
            <div className="homepage-popup__image-wrap">
              <div className="homepage-popup__image-glow" />
              <img src="/confuse.png" alt="Confused mascot" className="homepage-popup__image" />
            </div>
            <h3>{t('popup.title')}</h3>
            <p>{t('popup.message', { count: countdown })}</p>
            <button onClick={handleGoToDashboard} className="home-button home-button--primary homepage-popup__action">
              <span>{t('popup.goToDashboard')}</span>
              <ChevronRight size={18} />
            </button>
          </div>
        </div>
      )}
    </>
  );
}

export default App;
