import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import AuthCard from '../../components/Auth/AuthCard';
import Input from '../../components/UI/Input';
import Button from '../../components/UI/Button';
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
        throw new Error(data.message || t('forgot_failed', 'Gửi yêu cầu thất bại. Vui lòng thử lại.'));
      }

      setSuccessMsg(data.message || t('forgot_success', 'Yêu cầu đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra email của bạn.'));
      setEmail('');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen w-full flex flex-col relative">
      <AuthCard 
        footerText={t('forgot_footer')}
        mascotText={t('forgot_mascot')}
      >
        <div className="text-center mb-8 animate-in fade-in slide-in-from-bottom-2 duration-500 fill-mode-both delay-100">
          <h1 className="text-[32px] font-bold text-text-primary mb-2">{t('forgot_title')}</h1>
          <p className="text-[15px] font-normal text-text-secondary">
            {t('forgot_subtitle')}
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

        <form onSubmit={handleSubmit} className="flex flex-col gap-5 animate-in fade-in slide-in-from-bottom-4 duration-700 fill-mode-both delay-200">
          <Input
            label={t('email_label')}
            id="email"
            type="email"
            placeholder={t('email_placeholder')}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            disabled={loading}
          />
          <Button type="submit" disabled={loading}>
            {loading ? t('processing', 'Đang xử lý...') : t('forgot_button')}
          </Button>
        </form>
        <div className="text-center mt-6 animate-in fade-in duration-500 fill-mode-both delay-300">
          <a href="#login" className="text-[14px] font-semibold text-text-primary underline hover:text-primary transition-colors duration-200">
            {t('back_to_login')}
          </a>
        </div>
      </AuthCard>
    </div>
  );
};

export default ForgotPasswordPage;
