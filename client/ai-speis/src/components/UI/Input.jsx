import React, { useState } from 'react';
import { Eye, EyeOff } from 'lucide-react';

const Input = React.forwardRef(({ label, id, type = 'text', error, rightElement, ...props }, ref) => {
  const [showPassword, setShowPassword] = useState(false);
  
  const isPassword = type === 'password';
  const inputType = isPassword ? (showPassword ? 'text' : 'password') : type;

  return (
    <div className="flex flex-col gap-1.5 w-full">
      {label && (
        <label htmlFor={id} className="text-label font-semibold text-text-primary uppercase tracking-wide">
          {label}
        </label>
      )}
      <div className="relative">
        <input
          ref={ref}
          id={id}
          type={inputType}
          className={`w-full px-4 py-3 bg-surface-2 border rounded-input text-body font-normal text-text-primary placeholder:text-text-disabled outline-none transition-shadow duration-200 shadow-sm
            ${error ? 'border-error focus:border-error focus:ring-2 focus:ring-error/20 focus:shadow-none' : 'border-border focus:border-primary focus:ring-4 focus:ring-primary-xlight focus:shadow-none hover:border-border-strong'}
            ${isPassword || rightElement ? 'pr-11' : ''}
          `}
          {...props}
        />
        {isPassword && (
          <button
            type="button"
            className="absolute right-3 top-1/2 -translate-y-1/2 text-text-secondary hover:text-primary transition-colors focus:outline-none p-1"
            onClick={() => setShowPassword(!showPassword)}
            tabIndex="-1"
            aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
          >
            {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        )}
        {!isPassword && rightElement && (
          <div className="absolute right-3 top-1/2 -translate-y-1/2 flex items-center pointer-events-none text-text-secondary">
            {rightElement}
          </div>
        )}
      </div>
      {error && <span className="text-label font-normal text-error mt-0.5">{error}</span>}
    </div>
  );
});

Input.displayName = 'Input';

export default Input;
