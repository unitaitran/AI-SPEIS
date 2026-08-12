import React, { useState } from 'react';
import { useTranslation } from 'react-i18next'; 
import Input from '../UI/Input';
import Button from '../UI/Button';
import Checkbox from '../UI/Checkbox';
import Alert from '../UI/Alert';
import { ENDPOINTS } from '../../config/api';
import { getDefaultRouteForRole } from '../../routes/auth';
import { navigate } from '../../routes/navigation';

const LoginForm = () => {
  const { t } = useTranslation('login'); 
  
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      const response = await fetch(ENDPOINTS.LOGIN, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.message || t('login_failed', 'Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.'));
      }

      // Save token and user details
      localStorage.setItem('token', data.jwtToken);
      localStorage.setItem('user', JSON.stringify({
        userId: data.userId,
        fullName: data.fullName,
        email: data.email,
        role: data.role,
        avatar: data.imageUrl,
        isPremium: data.isPremium,
        remainingInterviewQuota: data.remainingInterviewQuota
      }));
      
      const targetRoute = getDefaultRouteForRole(data.role);
      navigate(targetRoute, { replace: true });
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleLogin = () => {
    window.location.href = ENDPOINTS.GOOGLE_LOGIN;
  };

  return (
    <div className="w-full flex flex-col">
      {/* Header Title */}
      <div className="text-center mb-6">
        <h1 className="text-xl font-extrabold text-text-primary mb-1">
          {t('title', 'Đăng nhập tài khoản')}
        </h1>
        <p className="text-xs text-text-secondary">
          {t('subtitle', 'Tiếp tục luyện phỏng vấn và theo dõi tiến độ cá nhân')}
        </p>
      </div>

      {/* Error Alert */}
      {error && (
        <Alert variant="error" className="mb-4 text-xs" onClose={() => setError('')}>
          {error}
        </Alert>
      )}

      {/* Login Form */}
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <Input
          label={t('email_label', 'EMAIL DĂNG NHẬP')}
          id="email"
          type="email"
          placeholder={t('email_placeholder', 'name@example.com')}
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          disabled={loading}
        />
        
        <Input
          label={t('password_label', 'MẬT KHẨU')}
          id="password"
          type="password"
          placeholder={t('password_placeholder', '••••••••')}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          disabled={loading}
        />

        <div className="flex items-center justify-between mt-1 text-xs">
          <Checkbox 
            label={t('remember_me', 'Ghi nhớ đăng nhập')} 
            id="rememberMe" 
            disabled={loading}
          />
          <a 
            href="#forgot-password" 
            className="font-semibold text-primary hover:text-primary-hover hover:underline transition-colors focus-ring rounded-sm"
          >
            {t('forgot_password', 'Quên mật khẩu?')}
          </a>
        </div>

        <Button 
          type="submit" 
          variant="primary" 
          size="md" 
          fullWidth 
          loading={loading}
          className="mt-2"
        >
          {t('login_button', 'Đăng nhập')}
        </Button>
      </form>

      {/* Divider */}
      <div className="relative flex items-center justify-center my-5">
        <div className="absolute inset-x-0 h-px bg-border" />
        <span className="relative bg-surface px-3 text-[11px] font-semibold text-text-muted uppercase tracking-wider">
          {t('or', 'hoặc')}
        </span>
      </div>

      {/* Google Login Button */}
      <button 
        type="button"
        onClick={handleGoogleLogin}
        disabled={loading}
        className="w-full min-h-[40px] px-4 rounded-md text-text-primary bg-surface border border-border hover:border-border-strong hover:bg-surface-muted shadow-sm text-xs font-semibold flex items-center justify-center gap-2.5 transition-all focus-ring disabled:opacity-50 disabled:cursor-not-allowed"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="18px" height="18px" className="shrink-0">
          <path fill="#FFC107" d="M43.611,20.083H42V20H24v8h11.303c-1.649,4.657-6.08,8-11.303,8c-6.627,0-12-5.373-12-12c0-6.627,5.373-12,12-12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C12.955,4,4,12.955,4,24c0,11.045,8.955,20,20,20c11.045,0,20-8.955,20-20C44,22.659,43.862,21.35,43.611,20.083z"/>
          <path fill="#FF3D00" d="M6.306,14.691l6.571,4.819C14.655,15.108,18.961,12,24,12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C16.318,4,9.656,8.337,6.306,14.691z"/>
          <path fill="#4CAF50" d="M24,44c5.166,0,9.86-1.977,13.409-5.192l-6.19-5.238C29.211,35.091,26.715,36,24,36c-5.202,0-9.619-3.317-11.283-7.946l-6.522,5.025C9.505,39.556,16.227,44,24,44z"/>
          <path fill="#1976D2" d="M43.611,20.083H42V20H24v8h11.303c-0.792,2.237-2.231,4.166-4.087,5.571c0.001-0.001,0.002-0.001,0.003-0.002l6.19,5.238C36.971,39.205,44,34,44,24C44,22.659,43.862,21.35,43.611,20.083z"/>
        </svg>
        <span>{t('continue_with_google', 'Tiếp tục với Google')}</span>
      </button>

      {/* Signup Prompt Link */}
      <div className="text-center mt-3 text-xs text-text-secondary">
        <span>{t('no_account', 'Chưa có tài khoản?')} </span>
        <a 
          href="#register" 
          className="font-bold text-text-primary hover:text-primary underline transition-colors focus-ring rounded-sm"
        >
          {t('sign_up', 'Đăng ký ngay')}
        </a>
      </div>
    </div>
  );
};

export default LoginForm;