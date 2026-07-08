import React from 'react';

const Button = ({ children, type = 'button', className = '', ...props }) => {
  return (
    <button
      type={type}
      className={`w-full min-h-[44px] px-4 rounded-button text-white text-body font-bold flex justify-center items-center gap-2 border border-transparent transition-transform duration-200 ease-out focus:outline-none focus:ring-2 focus:ring-primary-light focus:ring-offset-1 disabled:opacity-50 disabled:cursor-not-allowed bg-gradient-to-b from-primary to-primary-dark shadow-card hover:-translate-y-0.5 ${className}`}
      {...props}
    >
      {children}
    </button>
  );
};

export default Button;
