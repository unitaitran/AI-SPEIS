import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/Auth/AuthCard';
import LoginForm from '../../components/Auth/LoginForm';

const LoginPage = () => {
  const { t } = useTranslation('login');
  const [successMessage, setSuccessMessage] = useState('');
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    const hash = window.location.hash;
    if (hash.startsWith('#login?')) {
      const queryString = hash.split('?')[1];
      const urlParams = new URLSearchParams(queryString);
      
      const status = urlParams.get('status');
      const message = urlParams.get('message');

      if (status === 'success' && message) {
        setSuccessMessage(message);
      } else if (status === 'error' && message) {
        setErrorMessage(message);
      }

      // Remove query params from URL
      window.history.replaceState(null, '', window.location.pathname + '#login');
    }
  }, []);

  return (
    <div className="min-h-screen w-full flex flex-col relative">
      {/* Toast notifications */}
      <div className="absolute top-6 left-1/2 -translate-x-1/2 w-full max-w-md px-4 z-50 flex flex-col gap-2">
        {successMessage && (
          <div className="bg-success-light border border-success text-success px-4 py-3 rounded-xl text-sm font-medium shadow-sm animate-fade-in text-center">
            {successMessage}
          </div>
        )}
        {errorMessage && (
          <div className="bg-error-light border border-error text-error px-4 py-3 rounded-xl text-sm font-medium shadow-sm animate-fade-in text-center">
            {errorMessage}
          </div>
        )}
      </div>

      <AuthCard 
        footerText={t('footer_text')}
        mascotText={t('mascot_greeting')}
      >
        <LoginForm />
      </AuthCard>
    </div>
  );
};

export default LoginPage;
