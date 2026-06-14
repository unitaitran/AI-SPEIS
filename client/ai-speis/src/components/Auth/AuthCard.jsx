import React from 'react';

const AuthCard = ({ children, footerText }) => {
  return (
    <div className="w-full min-h-screen bg-surface-2 flex animate-in fade-in duration-700 ease-out">
      
      {/* Left Column - Mascot (Hidden on mobile) */}
      <div className="hidden lg:flex flex-1 flex-col items-center justify-center bg-gradient-to-br from-primary-xlight to-surface-1 p-10 relative overflow-hidden border-r border-border">
        {/* Background decorative blobs */}
        <div className="absolute top-[-10%] left-[-10%] w-[400px] h-[400px] bg-primary rounded-full mix-blend-multiply filter blur-[80px] opacity-20"></div>
        <div className="absolute bottom-[-10%] right-[-10%] w-[350px] h-[350px] bg-primary-light rounded-full mix-blend-multiply filter blur-[80px] opacity-30"></div>
        
        {/* Speech Bubble & Mascot */}
        <div className="relative animate-in slide-in-from-bottom-8 fade-in duration-700 delay-300 z-10 flex flex-col items-center">
          <div className="bg-white px-6 py-4 rounded-2xl rounded-br-none shadow-[0_8px_30px_rgb(0,0,0,0.08)] mb-4 text-primary-dark font-bold text-lg relative animate-bounce-slight">
            Chào bạn! Cùng đăng nhập nhé ⭐
            <div className="absolute bottom-[-10px] right-[30px] w-0 h-0 border-l-[10px] border-l-transparent border-t-[12px] border-t-white border-r-[10px] border-r-transparent"></div>
          </div>
          <img 
            src="/mascot_AI-SPEIS-removebg.png" 
            alt="AI-SPEIS Mascot" 
            className="w-[280px] h-auto object-contain drop-shadow-2xl hover:scale-105 transition-transform duration-500"
          />
        </div>
      </div>

      {/* Right Column - Form Area */}
      <div className="w-full lg:flex-1 flex flex-col items-center justify-center h-full bg-surface-2 relative px-6 py-8 md:px-10 md:py-8 overflow-y-auto custom-scrollbar">
        <div className="w-full max-w-[480px] flex flex-col items-center justify-center my-auto py-8">
          {/* Logo Area */}
          <div className="flex justify-center mb-6 shrink-0">
            <img 
              src="/logo_AI-SPEIS-removebg.png" 
              alt="AI-SPEIS Logo" 
              className="h-[72px] w-auto object-contain"
            />
          </div>

          {/* Content Area */}
          <div className="w-full flex flex-col">
            {children}
          </div>

          {/* Footer Note */}
          {footerText && (
            <div className="mt-6 pt-6 border-t border-border w-full text-center shrink-0">
              <p className="text-[12px] font-normal text-text-secondary leading-relaxed">
                {footerText}
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default AuthCard;
