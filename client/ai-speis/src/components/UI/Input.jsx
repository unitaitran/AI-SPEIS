import React, { useState } from 'react';
import { Eye, EyeOff } from 'lucide-react';

const Input = React.forwardRef(({
  label,
  id,
  type = 'text',
  error,
  helperText,
  icon: Icon = null,
  rightElement,
  className = '',
  disabled = false,
  required = false,
  ...props
}, ref) => {
  const [showPassword, setShowPassword] = useState(false);
  
  const isPassword = type === 'password';
  const inputType = isPassword ? (showPassword ? 'text' : 'password') : type;

  return (
    <div className="flex flex-col gap-1.5 w-full">
      {label && (
        <label htmlFor={id} className="text-xs font-semibold text-text-primary uppercase tracking-wider flex items-center gap-1">
          {label}
          {required && <span className="text-error" aria-hidden="true">*</span>}
        </label>
      )}
      <div className="relative flex items-center">
        {Icon && (
          <div className="absolute left-3.5 pointer-events-none text-text-secondary">
            <Icon size={18} />
          </div>
        )}
        <input
          ref={ref}
          id={id}
          type={inputType}
          disabled={disabled}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? `${id}-error` : helperText ? `${id}-helper` : undefined}
          className={`
            w-full px-3.5 py-2.5 bg-surface border rounded-md text-sm font-normal text-text-primary placeholder:text-text-disabled outline-none transition-all duration-200 shadow-sm
            ${Icon ? 'pl-10' : ''}
            ${isPassword || rightElement ? 'pr-10' : ''}
            ${error 
              ? 'border-error focus:border-error focus:ring-2 focus:ring-error-light' 
              : 'border-border hover:border-border-strong focus:border-primary focus-ring'
            }
            ${disabled ? 'bg-surface-muted text-text-disabled cursor-not-allowed opacity-70' : ''}
            ${className}
          `}
          {...props}
        />
        {isPassword && (
          <button
            type="button"
            className="absolute right-3 text-text-secondary hover:text-primary transition-colors focus:outline-none p-1 rounded-sm"
            onClick={() => setShowPassword(!showPassword)}
            tabIndex="-1"
            aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
          >
            {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        )}
        {!isPassword && rightElement && (
          <div className="absolute right-3 flex items-center text-text-secondary">
            {rightElement}
          </div>
        )}
      </div>
      {error ? (
        <span id={`${id}-error`} className="text-xs font-medium text-error mt-0.5" role="alert">
          {error}
        </span>
      ) : helperText ? (
        <span id={`${id}-helper`} className="text-xs font-normal text-text-muted mt-0.5">
          {helperText}
        </span>
      ) : null}
    </div>
  );
});

Input.displayName = 'Input';

export default Input;
