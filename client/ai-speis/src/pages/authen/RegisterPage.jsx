import React from 'react';
import AuthCard from '../../components/Auth/AuthCard';
import RegisterForm from '../../components/Auth/RegisterForm';

const RegisterPage = () => {
  return (
    <div className="min-h-screen w-full flex flex-col relative">
      <AuthCard 
        footerText=""
      >
        <RegisterForm />
      </AuthCard>
    </div>
  );
};

export default RegisterPage;
