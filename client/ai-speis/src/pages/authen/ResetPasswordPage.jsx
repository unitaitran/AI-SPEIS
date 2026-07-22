import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/Auth/AuthCard';
import Input from '../../components/UI/Input';
import Button from '../../components/UI/Button';
import { ENDPOINTS } from '../../config/api';

const ResetPasswordPage = () => {
  const { t } = useTranslation('login');
  const [token, setToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  useEffect(() => {
    const hash = window.location.hash;
    if (hash.startsWith('#reset-password?')) {
      const queryString = hash.split('?')[1];
      const urlParams = new URLSearchParams(queryString);
      const tokenVal = urlParams.get('token');
      if (tokenVal) {
        setToken(tokenVal);
      } else {
        setError(t('invalid_reset_token', 'Mã xác nhận đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.'));
      }
    } else {
      setError(t('invalid_reset_token', 'Mã xác nhận đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.'));
    }
  }, [t]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!token) {
      setError(t('invalid_reset_token', 'Mã xác nhận đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.'));
      return;
    }

    if (newPassword !== confirmPassword) {
      setError(t('pwd_mismatch', 'Mật khẩu xác nhận không khớp'));
      return;
    }

    setLoading(true);
    setError('');
    setSuccessMsg('');

    try {
      const response = await fetch(ENDPOINTS.RESET_PASSWORD, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          token,
          newPassword,
          confirmPassword
        })
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.message || t('reset_failed', 'Đặt lại mật khẩu thất bại.'));
      }

      setSuccessMsg(t('reset_success', 'Đặt lại mật khẩu thành công. Đang chuyển hướng...'));
      
      // Redirect to login page with success status after 2 seconds
      setTimeout(() => {
        const successMessage = t('reset_success_redirect_msg', 'Đặt lại mật khẩu thành công. Vui lòng đăng nhập với mật khẩu mới.');
        window.location.hash = `#login?status=success&message=${encodeURIComponent(successMessage)}`;
      }, 2000);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen w-full flex flex-col relative">
      <AuthCard 
        footerText=""
        mascotText={t('reset_mascot')}
      >
        <div className="text-center mb-8 animate-in fade-in slide-in-from-bottom-2 duration-500 fill-mode-both delay-100">
          <h1 className="text-[32px] font-bold text-text-primary mb-2">{t('reset_title')}</h1>
          <p className="text-[15px] font-normal text-text-secondary">
            {t('reset_subtitle')}
          </p>
        </div>

        {error && (
          <div className="mb-3 p-3 bg-error-light border border-error rounded-xl text-error text-[13px] animate-in fade-in duration-300">
            {error}
          </div>
        )}

        {successMsg && (
          <div className="mb-3 p-3 bg-success-light border border-success rounded-xl text-success text-[13px] animate-in fade-in duration-300">
            {successMsg}
          </div>
        )}

        {token && (
          <form onSubmit={handleSubmit} className="flex flex-col gap-5 animate-in fade-in slide-in-from-bottom-4 duration-700 fill-mode-both delay-200">
            <Input
              label={t('new_password_label')}
              id="newPassword"
              type="password"
              placeholder={t('password_placeholder')}
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              disabled={loading}
            />
            <Input
              label={t('confirm_password_label')}
              id="confirmPassword"
              type="password"
              placeholder={t('password_placeholder')}
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              disabled={loading}
            />
            <Button type="submit" disabled={loading}>
              {loading ? t('processing', 'Đang xử lý...') : t('reset_button')}
            </Button>
          </form>
        )}

        <div className="text-center mt-6 animate-in fade-in duration-500 fill-mode-both delay-300">
          <a href="#login" className="text-[14px] font-semibold text-text-primary underline hover:text-primary transition-colors duration-200">
            {t('back_to_login')}
          </a>
        </div>
      </AuthCard>
    </div>
  );
};

export default ResetPasswordPage;
