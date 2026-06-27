import React, { useEffect, useState } from 'react';
import { decodeJwt } from './auth';

export default function TokenMonitor() {
  const [showPopup, setShowPopup] = useState(false);

  useEffect(() => {
    const checkToken = () => {
      const token = localStorage.getItem('token');
      if (token) {
        const payload = decodeJwt(token);
        if (payload && payload.exp) {
          const currentTime = Math.floor(Date.now() / 1000);
          if (payload.exp < currentTime) {
            setShowPopup(true);
          }
        }
      } else {
        setShowPopup(false);
      }
    };
    
    checkToken();
    const interval = setInterval(checkToken, 5000);
    return () => clearInterval(interval);
  }, []);

  if (!showPopup) return null;

  return (
    <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/50 backdrop-blur-sm animate-in fade-in">
      <div className="bg-white rounded-2xl shadow-xl p-8 max-w-sm w-full mx-4 flex flex-col items-center text-center">
        <div className="w-16 h-16 bg-error-light rounded-full flex items-center justify-center text-error mb-4">
          <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
        </div>
        <h3 className="text-xl font-bold text-text-primary mb-2">Phiên đăng nhập hết hạn</h3>
        <p className="text-text-secondary mb-6">Vui lòng đăng nhập lại để tiếp tục sử dụng hệ thống.</p>
        <button 
          className="w-full py-3 px-4 bg-primary text-white rounded-xl font-semibold hover:bg-primary-dark transition-colors"
          onClick={() => {
            localStorage.removeItem('token');
            localStorage.removeItem('user');
            setShowPopup(false);
            window.location.href = '/login';
          }}
        >
          Đăng nhập lại
        </button>
      </div>
    </div>
  );
}
