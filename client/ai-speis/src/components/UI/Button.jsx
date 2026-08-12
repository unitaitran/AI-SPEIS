import React from 'react';
import { Loader2 } from 'lucide-react';

const Button = React.forwardRef(({
  children,
  type = 'button',
  variant = 'primary',
  size = 'md',
  fullWidth = false,
  loading = false,
  disabled = false,
  icon: Icon = null,
  rightIcon: RightIcon = null,
  className = '',
  ...props
}, ref) => {

  const baseStyles = "inline-flex items-center justify-center font-semibold transition-all duration-200 ease-out focus:outline-none focus-ring disabled:opacity-50 disabled:cursor-not-allowed select-none";

  const sizeStyles = {
    sm: "min-h-[32px] px-3 py-1.5 text-xs rounded-md gap-1.5",
    md: "min-h-[40px] px-4 py-2 text-sm rounded-md gap-2",
    lg: "min-h-[48px] px-6 py-3 text-base rounded-lg gap-2.5",
  };

  const variantStyles = {
    primary: "bg-primary hover:bg-primary-hover text-white shadow-sm border border-transparent active:scale-[0.98]",
    secondary: "bg-primary-light hover:bg-primary/20 text-primary border border-transparent active:scale-[0.98]",
    ai: "bg-secondary hover:bg-secondary-hover text-white shadow-sm border border-transparent active:scale-[0.98]",
    outline: "bg-surface hover:bg-surface-muted text-text-primary border border-border hover:border-border-strong active:scale-[0.98]",
    ghost: "bg-transparent hover:bg-surface-muted text-text-secondary hover:text-text-primary border border-transparent",
    danger: "bg-error hover:bg-red-700 text-white shadow-sm border border-transparent active:scale-[0.98]",
  };

  const isBtnDisabled = disabled || loading;

  return (
    <button
      ref={ref}
      type={type}
      disabled={isBtnDisabled}
      className={`
        ${baseStyles}
        ${sizeStyles[size] || sizeStyles.md}
        ${variantStyles[variant] || variantStyles.primary}
        ${fullWidth ? 'w-full' : ''}
        ${className}
      `}
      {...props}
    >
      {loading ? (
        <Loader2 className="w-4 h-4 animate-spin text-current shrink-0" />
      ) : Icon ? (
        <Icon className="w-4 h-4 shrink-0" />
      ) : null}
      <span>{children}</span>
      {!loading && RightIcon && <RightIcon className="w-4 h-4 shrink-0" />}
    </button>
  );
});

Button.displayName = 'Button';

export default Button;
