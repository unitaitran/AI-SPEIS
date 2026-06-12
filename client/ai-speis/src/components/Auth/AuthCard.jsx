import React from 'react';

const AuthCard = ({ children, footerText }) => {
  return (
    <div className="w-full max-w-[480px] mx-auto bg-surface-2 border border-border rounded-[24px] shadow-[0_12px_30px_rgba(63,127,174,0.08)] overflow-hidden flex flex-col h-full md:h-auto px-6 py-10 md:p-10">
      
      {/* Logo Area */}
      <div className="flex justify-center mb-8">
        <img 
          src="/logo_AI-SPEIS-removebg.png" 
          alt="AI-SPEIS Logo" 
          className="h-12 w-auto object-contain"
        />
      </div>

      {/* Content Area */}
      <div className="flex-1 flex flex-col">
        {children}
      </div>

      {/* Footer Note */}
      {footerText && (
        <div className="mt-8 pt-6 border-t border-border text-center">
          <p className="text-[12px] font-normal text-text-secondary leading-relaxed">
            {footerText}
          </p>
        </div>
      )}
    </div>
  );
};

export default AuthCard;
