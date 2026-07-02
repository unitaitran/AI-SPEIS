import React from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/Auth/AuthCard';
import RegisterForm from '../../components/Auth/RegisterForm';

const RegisterPage = () => {
  const { t } = useTranslation('register');

  return (
    <div className="h-screen overflow-hidden w-full flex flex-col relative">
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