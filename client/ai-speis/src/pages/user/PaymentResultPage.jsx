import React, { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { CheckCircle2, AlertTriangle, RefreshCw, ArrowRight } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import paymentService from '../../services/PaymentService';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import '../../styles/user/PaymentResultPage.css';

function PaymentResultPage() {
  const [searchParams] = useSearchParams();
  const [status, setStatus] = useState('processing'); // processing, success, error
  const [message, setMessage] = useState('');

  useEffect(() => {
    const verifyPayment = async () => {
      const orderId = searchParams.get('orderId');
      const resultCode = searchParams.get('resultCode');
      const momoMessage = searchParams.get('message');

      if (!orderId) {
        setStatus('error');
        setMessage('Không tìm thấy thông tin đơn hàng.');
        return;
      }

      if (resultCode && resultCode !== '0') {
        setStatus('error');
        setMessage(momoMessage || 'Thanh toán không thành công. Vui lòng thử lại.');
        return;
      }

      try {
        const response = await paymentService.verifyPaymentResult(orderId);
        if (response.success) {
          setStatus('success');
          setMessage('Thanh toán thành công! Gói Premium của bạn đã được kích hoạt.');
          
          // Redirect to dashboard after 3 seconds
          setTimeout(() => {
            navigate(USER_ROUTES.DASHBOARD);
          }, 3000);
        } else {
          setStatus('error');
          setMessage(response.message || 'Lỗi khi xác minh giao dịch.');
        }
      } catch (err) {
        setStatus('error');
        setMessage(err.message || 'Có lỗi xảy ra khi xác minh thanh toán.');
      }
    };

    verifyPayment();
  }, [searchParams]);

  const handleRetry = () => {
    navigate(USER_ROUTES.PACKAGES);
  };

  const handleGoToDashboard = () => {
    navigate(USER_ROUTES.DASHBOARD);
  };

  return (
    <UserLayout>
      <div className="payment-result-page">
        <div className="payment-result-card animate-pageEntrance">
          {status === 'processing' && (
            <div className="payment-result-content processing">
              <RefreshCw size={64} className="animate-spin text-primary mx-auto mb-6" />
              <h2 className="text-2xl font-bold text-text-primary mb-2">Đang xác minh thanh toán</h2>
              <p className="text-text-secondary">Vui lòng chờ trong giây lát, chúng tôi đang kiểm tra kết quả giao dịch với MoMo...</p>
            </div>
          )}

          {status === 'success' && (
            <div className="payment-result-content success">
              <div className="success-icon-wrapper">
                <CheckCircle2 size={64} className="text-success mx-auto" />
              </div>
              <h2 className="text-2xl font-bold text-text-primary mb-2 mt-6">Thanh toán thành công!</h2>
              <p className="text-text-secondary mb-8">{message}</p>
              
              <button onClick={handleGoToDashboard} className="result-action-btn primary-btn">
                Đến Dashboard <ArrowRight size={18} />
              </button>
            </div>
          )}

          {status === 'error' && (
            <div className="payment-result-content error">
              <div className="error-icon-wrapper">
                <AlertTriangle size={64} className="text-error mx-auto" />
              </div>
              <h2 className="text-2xl font-bold text-text-primary mb-2 mt-6">Thanh toán thất bại</h2>
              <p className="text-text-secondary mb-8">{message}</p>
              
              <button onClick={handleRetry} className="result-action-btn primary-btn mb-3">
                Thử lại
              </button>
              <button onClick={handleGoToDashboard} className="result-action-btn secondary-btn">
                Về trang chủ
              </button>
            </div>
          )}
        </div>
      </div>
    </UserLayout>
  );
}

export default PaymentResultPage;
