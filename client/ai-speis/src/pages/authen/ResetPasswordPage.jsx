import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/Auth/AuthCard';
import Input from '../../components/UI/Input';
import Button from '../../components/UI/Button';
import Alert from '../../components/UI/Alert';
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
    <AuthCard 
      footerText=""
      mascotText={t('reset_mascot', 'Hãy tạo một mật khẩu mới đủ an toàn nhé ⭐')}
    >
      <div className="w-full flex flex-col">
        {/* Header */}
        <div className="text-center mb-6">
          <h1 className="text-xl font-extrabold text-text-primary mb-1">
            {t('reset_title', 'Đặt lại mật khẩu')}
          </h1>
          <p className="text-xs text-text-secondary leading-relaxed">
            {t('reset_subtitle', 'Nhập mật khẩu mới cho tài khoản ứng viên của bạn')}
          </p>
        </div>

        {/* Alerts */}
        {error && (
          <Alert variant="error" className="mb-4 text-xs" onClose={() => setError('')}>
            {error}
          </Alert>
        )}

        {successMsg && (
          <Alert variant="success" className="mb-4 text-xs">
            {successMsg}
          </Alert>
        )}

        {/* Form */}
        {token && (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <Input
              label={t('new_password_label', 'MẬT KHẨU MỚI')}
              id="newPassword"
              type="password"
              placeholder="••••••••"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              disabled={loading}
            />
            <Input
              label={t('confirm_password_label', 'XÁC NHẬN MẬT KHẨU MỚI')}
              id="confirmPassword"
              type="password"
              placeholder="••••••••"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              disabled={loading}
            />
            <Button 
              type="submit" 
              variant="primary" 
              size="md" 
              fullWidth 
              loading={loading}
              className="mt-1"
            >
              {t('reset_button', 'CẬP NHẬT MẬT KHẨU')}
            </Button>
          </form>
        )}

        {/* Back to login link */}
        <div className="text-center mt-5 text-xs">
          <a 
            href="#login" 
            className="font-bold text-text-primary hover:text-primary underline transition-colors focus-ring rounded-sm"
          >
            {t('back_to_login', 'Quay lại đăng nhập')}
          </a>
        </div>
      </div>
    </AuthCard>
  );
};

export default ResetPasswordPage;
