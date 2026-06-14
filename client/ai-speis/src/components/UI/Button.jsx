import React from 'react';

const Button = ({ children, type = 'button', className = '', ...props }) => {
  return (
    <button
      type={type}
      className={`w-full min-h-[44px] px-4 rounded-[14px] text-white text-[14px] font-bold flex justify-center items-center gap-2 border border-transparent transition-all duration-250 ease-out focus:outline-none focus:ring-2 focus:ring-primary-light focus:ring-offset-1 disabled:opacity-50 disabled:cursor-not-allowed ${className}`}
      style={{
        background: 'linear-gradient(180deg, var(--primary), var(--primary-dark))',
        boxShadow: '0 14px 30px rgba(111, 182, 232, 0.24)'
      }}
      onMouseEnter={(e) => {
        if (!props.disabled) e.currentTarget.style.transform = 'translateY(-2px)';
      }}
      onMouseLeave={(e) => {
        if (!props.disabled) e.currentTarget.style.transform = 'none';
      }}
      {...props}
    >
      {children}
    </button>
  );
};

export default Button;
