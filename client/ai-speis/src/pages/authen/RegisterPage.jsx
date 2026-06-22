import React from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/auth/AuthCard';
import RegisterForm from '../../components/auth/RegisterForm';

const RegisterPage = () => {
  const { t } = useTranslation('register');

  return (
    <div className="min-h-screen w-full flex flex-col relative">
      <AuthCard 
        footerText=""
        mascotText={t('mascot_greeting')}
      >
        <RegisterForm />
      </AuthCard>
    </div>
  );
};

export default RegisterPage;