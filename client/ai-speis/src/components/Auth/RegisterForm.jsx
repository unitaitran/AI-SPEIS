import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import Input from '../UI/Input';
import Button from '../UI/Button';
import Checkbox from '../UI/Checkbox';
import Alert from '../UI/Alert';
import { ENDPOINTS } from '../../config/api';

const RegisterForm = () => {
  const { t } = useTranslation('register');

  const [formData, setFormData] = useState({
    fullName: '',
    email: '',
    phoneNumber: '',
    password: '',
    confirmPassword: '',
    agreeTerms: false
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  const handleChange = (e) => {
    const { id, value, type, checked } = e.target;
    setFormData(prev => ({
      ...prev,
      [id]: type === 'checkbox' ? checked : value
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setSuccessMsg('');

    if (formData.password !== formData.confirmPassword) {
      setError(t('pwd_mismatch', 'Mật khẩu xác nhận không khớp'));
      setLoading(false);
      return;
    }

    try {
      const response = await fetch(ENDPOINTS.REGISTER, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fullName: formData.fullName,
          email: formData.email,
          phoneNumber: formData.phoneNumber,
          password: formData.password,
          confirmPassword: formData.confirmPassword
        })
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.message || t('register_fail', 'Đăng ký thất bại. Vui lòng kiểm tra lại thông tin.'));
      }

      setSuccessMsg(data.message || t('register_success', 'Đăng ký thành công. Vui lòng kiểm tra email để kích hoạt tài khoản.'));
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
      {/* Title & Subtitle */}
      <div className="text-center mb-5">
        <h1 className="text-xl font-extrabold text-text-primary mb-1">
          {t('title', 'Tạo tài khoản mới')}
        </h1>
        <p className="text-xs text-text-secondary">
          {t('subtitle', 'Bắt đầu trải nghiệm phỏng vấn thử cá nhân hóa với AI')}
        </p>
      </div>

      {/* Error / Success Alerts */}
      {error && (
        <Alert variant="error" className="mb-4 text-xs" onClose={() => setError('')}>
          {error}
        </Alert>
      )}

      {successMsg && (
        <Alert variant="success" className="mb-4 text-xs" onClose={() => setSuccessMsg('')}>
          {successMsg}
        </Alert>
      )}

      {/* Registration Form */}
      <form onSubmit={handleSubmit} className="flex flex-col gap-3.5">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Input
            label={t('fullname_label', 'HỌ VÀ TÊN')}
            id="fullName"
            type="text"
            placeholder={t('fullname_placeholder', 'Nguyễn Văn A')}
            value={formData.fullName}
            onChange={handleChange}
            required
            disabled={loading}
          />
          
          <Input
            label={t('phone_label', 'SỐ ĐIỆN THOẠI')}
            id="phoneNumber"
            type="tel"
            placeholder={t('phone_placeholder', '0912345678')}
            value={formData.phoneNumber}
            onChange={handleChange}
            required
            disabled={loading}
          />
        </div>
        
        <Input
          label={t('email_label', 'EMAIL')}
          id="email"
          type="email"
          placeholder={t('email_placeholder', 'name@example.com')}
          value={formData.email}
          onChange={handleChange}
          required
          disabled={loading}
        />

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Input
            label={t('password_label', 'MẬT KHẨU')}
            id="password"
            type="password"
            placeholder="••••••••"
            value={formData.password}
            onChange={handleChange}
            required
            disabled={loading}
          />

          <Input
            label={t('confirm_password_label', 'XÁC NHẬN MẬT KHẨU')}
            id="confirmPassword"
            type="password"
            placeholder="••••••••"
            value={formData.confirmPassword}
            onChange={handleChange}
            required
            disabled={loading}
          />
        </div>

        <div className="mt-1 mb-1">
          <Checkbox 
            label={t('agree_terms', 'Tôi đồng ý với Điều khoản sử dụng và Chính sách bảo mật')} 
            id="agreeTerms" 
            checked={formData.agreeTerms}
            onChange={handleChange}
            required
            disabled={loading}
          />
        </div>

        <Button 
          type="submit" 
          variant="primary" 
          size="md" 
          fullWidth 
          loading={loading}
          className="mt-1"
        >
          {t('submit_button', 'TẠO TÀI KHOẢN')}
        </Button>
      </form>

      {/* Divider */}
      <div className="relative flex items-center justify-center my-4">
        <div className="absolute inset-x-0 h-px bg-border" />
        <span className="relative bg-surface px-3 text-[11px] font-semibold text-text-muted uppercase tracking-wider">
          {t('or', 'hoặc')}
        </span>
      </div>

      {/* Google Signup Button */}
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
        <span>{t('continue_with_google', 'Đăng ký nhanh với Google')}</span>
      </button>

      {/* Login Prompt Link */}
      <div className="text-center mt-3 text-xs text-text-secondary">
        <span>{t('already_have_account', 'Đã có tài khoản? ')}</span>
        <a 
          href="#login" 
          className="font-bold text-text-primary hover:text-primary underline transition-colors focus-ring rounded-sm"
        >
          {t('login_link', 'Đăng nhập ngay')}
        </a>
      </div>
    </div>
  );
};

export default RegisterForm;