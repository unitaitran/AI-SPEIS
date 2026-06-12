import React from 'react';
import AuthCard from '../../components/Auth/AuthCard';
import Input from '../../components/UI/Input';
import Button from '../../components/UI/Button';

const ForgotPasswordPage = () => {
  return (
    <div className="min-h-screen w-full landing-shell flex items-center justify-center p-4 md:p-6">
      <AuthCard 
        footerText="Vui lòng kiểm tra hộp thư đến sau khi gửi yêu cầu."
      >
        <div className="text-center mb-8">
          <h1 className="text-[32px] font-bold text-text-primary mb-2">Quên mật khẩu</h1>
          <p className="text-[15px] font-normal text-text-secondary">
            Nhập email của bạn để nhận liên kết đặt lại mật khẩu.
          </p>
        </div>
        <form className="flex flex-col gap-5">
          <Input
            label="EMAIL"
            id="email"
            type="email"
            placeholder="name@example.com"
            required
          />
          <Button type="submit">
            GỬI LIÊN KẾT
          </Button>
        </form>
        <div className="text-center mt-6">
          <a href="#login" className="text-[14px] font-semibold text-text-primary underline hover:text-primary transition-colors duration-200">
            Quay lại Đăng nhập
          </a>
        </div>
      </AuthCard>
    </div>
  );
};

export default ForgotPasswordPage;
