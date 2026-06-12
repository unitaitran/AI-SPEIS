import React from 'react';
import AuthCard from '../../components/Auth/AuthCard';
import LoginForm from '../../components/Auth/LoginForm';

const LoginPage = () => {
  return (
    <div className="min-h-screen w-full landing-shell flex items-center justify-center p-4 md:p-6">
      <AuthCard 
        footerText="AI-SPEIS sử dụng hồ sơ, CV và lịch sử luyện tập để cá nhân hóa câu hỏi phỏng vấn."
      >
        <LoginForm />
      </AuthCard>
    </div>
  );
};

export default LoginPage;
