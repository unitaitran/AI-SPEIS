import React, { useState } from 'react';
import Input from '../UI/Input';
import Button from '../UI/Button';
import Checkbox from '../UI/Checkbox';
import { ENDPOINTS } from '../../config/api';
import { getDefaultRouteForRole } from '../../routes/auth';
import { navigate } from '../../routes/navigation';

const LoginForm = () => {
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
        throw new Error(data.message || 'Đăng nhập thất bại');
      }

      // Xử lý đăng nhập thành công
      localStorage.setItem('token', data.jwtToken);
      localStorage.setItem('user', JSON.stringify({
        userId: data.userId,
        fullName: data.fullName,
        email: data.email,
        role: data.role
      }));
      
      navigate(getDefaultRouteForRole(data.role), { replace: true });
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
    <div className="w-full">
      <div className="text-center mb-8 animate-in fade-in slide-in-from-bottom-2 duration-500 fill-mode-both delay-100">
        <h1 className="text-[32px] font-bold text-text-primary mb-2">Đăng nhập</h1>
        <p className="text-[15px] font-normal text-text-secondary">
          Tiếp tục luyện phỏng vấn và theo dõi tiến độ của bạn.
        </p>
      </div>

      {error && (
        <div className="mb-4 p-3 bg-error-light border border-error rounded-xl text-error text-[14px]">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="flex flex-col gap-5 animate-in fade-in slide-in-from-bottom-4 duration-700 fill-mode-both delay-200">
        <Input
          label="Email"
          id="email"
          type="email"
          placeholder="abc@gmail.com"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        
        <Input
          label="Mật khẩu"
          id="password"
          type="password"
          placeholder="Nhập mật khẩu"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />

        <div className="flex items-center justify-between mt-1 mb-2">
          <Checkbox label="Ghi nhớ đăng nhập" id="rememberMe" />
          <a href="#forgot-password" className="text-[14px] text-text-primary hover:text-primary transition-colors duration-200">
            Quên mật khẩu?
          </a>
        </div>

        <Button type="submit" disabled={loading}>
          {loading ? 'Đang xử lý...' : 'Đăng nhập'}
        </Button>
      </form>

      <div className="relative flex items-center justify-center mt-6 mb-6 animate-in fade-in duration-500 fill-mode-both delay-300">
        <div className="absolute inset-x-0 h-px bg-border-strong opacity-50"></div>
        <span className="relative bg-surface-2 px-4 text-[13px] font-medium text-text-secondary uppercase tracking-wider">hoặc</span>
      </div>

      <button 
        onClick={handleGoogleLogin}
        className="w-full min-h-[44px] mb-8 px-4 rounded-[14px] text-text-primary bg-surface-2 border border-border shadow-[0_2px_4px_rgba(31,45,61,0.05)] text-[14px] font-semibold flex justify-center items-center gap-3 transition-all duration-300 hover:bg-surface-1 hover:border-border-strong hover:-translate-y-0.5 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-primary-light focus:ring-offset-1 animate-in fade-in slide-in-from-bottom-2 duration-500 fill-mode-both delay-[400ms]"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="20px" height="20px">
          <path fill="#FFC107" d="M43.611,20.083H42V20H24v8h11.303c-1.649,4.657-6.08,8-11.303,8c-6.627,0-12-5.373-12-12c0-6.627,5.373-12,12-12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C12.955,4,4,12.955,4,24c0,11.045,8.955,20,20,20c11.045,0,20-8.955,20-20C44,22.659,43.862,21.35,43.611,20.083z"/>
          <path fill="#FF3D00" d="M6.306,14.691l6.571,4.819C14.655,15.108,18.961,12,24,12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C16.318,4,9.656,8.337,6.306,14.691z"/>
          <path fill="#4CAF50" d="M24,44c5.166,0,9.86-1.977,13.409-5.192l-6.19-5.238C29.211,35.091,26.715,36,24,36c-5.202,0-9.619-3.317-11.283-7.946l-6.522,5.025C9.505,39.556,16.227,44,24,44z"/>
          <path fill="#1976D2" d="M43.611,20.083H42V20H24v8h11.303c-0.792,2.237-2.231,4.166-4.087,5.571c0.001-0.001,0.002-0.001,0.003-0.002l6.19,5.238C36.971,39.205,44,34,44,24C44,22.659,43.862,21.35,43.611,20.083z"/>
        </svg>
        Tiếp tục với Google
      </button>

      <div className="text-center animate-in fade-in duration-500 fill-mode-both delay-500">
        <span className="text-[14px] text-text-secondary">Chưa có tài khoản? </span>
        <a href="#register" className="text-[14px] font-semibold text-text-primary underline hover:text-primary transition-colors duration-200">
          Đăng ký
        </a>
      </div>
    </div>
  );
};

export default LoginForm;
