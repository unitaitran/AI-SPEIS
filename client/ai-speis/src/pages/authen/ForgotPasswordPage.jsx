import React from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/Auth/AuthCard';
import Input from '../../components/UI/Input';
import Button from '../../components/UI/Button';

const ForgotPasswordPage = () => {
  const { t } = useTranslation('login');

  return (
    <div className="min-h-screen w-full flex flex-col relative">
      <AuthCard 
        footerText={t('forgot_footer')}
        mascotText={t('forgot_mascot')}
      >
        <div className="text-center mb-8 animate-in fade-in slide-in-from-bottom-2 duration-500 fill-mode-both delay-100">
          <h1 className="text-[32px] font-bold text-text-primary mb-2">{t('forgot_title')}</h1>
          <p className="text-[15px] font-normal text-text-secondary">
            {t('forgot_subtitle')}
          </p>
        </div>
        <form className="flex flex-col gap-5 animate-in fade-in slide-in-from-bottom-4 duration-700 fill-mode-both delay-200">
          <Input
            label={t('email_label')}
            id="email"
            type="email"
            placeholder={t('email_placeholder')}
            required
          />
          <Button type="submit">
            {t('forgot_button')}
          </Button>
        </form>
        <div className="text-center mt-6 animate-in fade-in duration-500 fill-mode-both delay-300">
          <a href="#login" className="text-[14px] font-semibold text-text-primary underline hover:text-primary transition-colors duration-200">
            {t('back_to_login')}
          </a>
        </div>
      </AuthCard>
    </div>
  );
};

export default ForgotPasswordPage;
