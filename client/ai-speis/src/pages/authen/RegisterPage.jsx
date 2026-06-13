import React from 'react';
import AuthCard from '../../components/Auth/AuthCard';
import RegisterForm from '../../components/Auth/RegisterForm';

const RegisterPage = () => {
  return (
    <div className="min-h-screen w-full landing-shell flex items-center justify-center p-4 md:p-6">
      <AuthCard 
        footerText=""
      >
        <RegisterForm />
      </AuthCard>
    </div>
  );
};

export default RegisterPage;
