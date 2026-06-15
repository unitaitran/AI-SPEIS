import React from 'react';
import { useTranslation } from 'react-i18next';
import { Globe } from 'lucide-react'; // <-- Đảm bảo bạn đã import icon này
import AuthCard from '../../components/Auth/AuthCard';
import LoginForm from '../../components/Auth/LoginForm';

const LoginPage = () => {
  const { t, i18n } = useTranslation('login');
  const isVi = (i18n.language || '').toLowerCase().includes('vi');
  const toggleLanguage = () => {
    const nextLang = isVi ? 'en' : 'vi';
    i18n.changeLanguage(nextLang);
  };

  return (
    <div className="min-h-screen w-full landing-shell relative flex items-center justify-center p-4 md:p-6">
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
        footerText={t('footer_text', 'AI-SPEIS sử dụng hồ sơ, CV và lịch sử luyện tập để cá nhân hóa câu hỏi phỏng vấn.')}
      >
        <LoginForm />
      </AuthCard>
    </div>
  );
};

export default LoginPage;