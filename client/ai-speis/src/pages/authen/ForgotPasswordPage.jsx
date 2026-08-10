import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/Auth/AuthCard';
import Input from '../../components/UI/Input';
import Button from '../../components/UI/Button';
import Alert from '../../components/UI/Alert';
import { ENDPOINTS } from '../../config/api';

const ForgotPasswordPage = () => {
  const { t } = useTranslation('login');
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setSuccessMsg('');

    try {
      const response = await fetch(ENDPOINTS.FORGOT_PASSWORD, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email })
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.message || t('forgot_failed', 'Gửi yêu cầu thất bại. Vui lòng kiểm tra lại email.'));
      }

      setSuccessMsg(data.message || t('forgot_success', 'Link đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra hộp thư email của bạn.'));
      setEmail('');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthCard 
      footerText={t('forgot_footer', 'Nếu bạn cần hỗ trợ thêm, vui lòng liên hệ bộ phận hỗ trợ AI-SPEIS.')}
      mascotText={t('forgot_mascot', 'Đừng lo! Tôi sẽ giúp bạn lấy lại mật khẩu ⭐')}
    >
      <div className="w-full flex flex-col">
        {/* Header */}
        <div className="text-center mb-6">
          <h1 className="text-xl font-extrabold text-text-primary mb-1">
            {t('forgot_title', 'Quên mật khẩu?')}
          </h1>
          <p className="text-xs text-text-secondary leading-relaxed">
            {t('forgot_subtitle', 'Nhập địa chỉ email đăng ký để nhận liên kết đặt lại mật khẩu.')}
          </p>
        </div>

        {/* Alerts */}
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

        {/* Form */}
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
          <Button 
            type="submit" 
            variant="primary" 
            size="md" 
            fullWidth 
            loading={loading}
            className="mt-1"
          >
            {t('forgot_button', 'GỬI LINK KHÔI PHỤC')}
          </Button>
        </form>

        {/* Back to login link */}
        <div className="text-center mt-5 text-xs">
          <a 
            href="#login" 
            className="font-bold text-text-primary hover:text-primary underline transition-colors focus-ring rounded-sm"
          >
            {t('back_to_login', 'Quay lại trang đăng nhập')}
          </a>
        </div>
      </div>
    </AuthCard>
  );
};

export default ForgotPasswordPage;
