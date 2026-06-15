import React from 'react';
import { useTranslation } from 'react-i18next'; // ➕ Import hook dịch
import { Globe } from 'lucide-react';
import AuthCard from '../../components/Auth/AuthCard';
import RegisterForm from '../../components/Auth/RegisterForm';

const RegisterPage = () => {
  const { t, i18n } = useTranslation('register');
  const isVi = (i18n.language || '').toLowerCase().includes('vi');
    const toggleLanguage = () => {
    const nextLang = isVi ? 'en' : 'vi';
    i18n.changeLanguage(nextLang);
  };
  return (
    
    <div className="min-h-screen w-full landing-shell flex items-center justify-center p-4 md:p-6">
      <div className="absolute top-6 right-6 md:top-8 md:right-10 z-10">
        <button
          type="button"
          className="ghost-button language-button"
          onClick={toggleLanguage}
          aria-label={t('aria.languageSwitch', 'Chuyển đổi ngôn ngữ')}
        >
          <Globe size={18} />
          <span>{isVi ? 'VI / EN' : 'EN / VI'}</span>
        </button>
      </div>
      <AuthCard 
        footerText=""
      >
        <RegisterForm />
      </AuthCard>
    </div>
  );
};

export default RegisterPage;