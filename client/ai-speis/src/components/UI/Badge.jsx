import React from 'react';

const Badge = ({
  children,
  variant = 'primary', // 'primary' | 'secondary' | 'ai' | 'success' | 'warning' | 'error' | 'neutral'
  size = 'md',          // 'sm' | 'md'
  pill = false,
  icon: Icon = null,
  className = '',
  ...props
}) => {
  const baseStyles = "inline-flex items-center font-semibold tracking-wide gap-1 shrink-0";

  const sizeStyles = {
    sm: "px-1.5 py-0.5 text-[10px]",
    md: "px-2.5 py-1 text-xs",
  };

  const radiusStyles = pill ? "rounded-full" : "rounded-sm";

  const variantStyles = {
    primary: "bg-primary-light text-primary border border-primary/20",
    secondary: "bg-accent-light text-accent border border-accent/20",
    ai: "bg-secondary-light text-secondary border border-secondary/20 font-bold",
    success: "bg-success-light text-success border border-success/20",
    warning: "bg-warning-light text-warning border border-warning/20",
    error: "bg-error-light text-error border border-error/20",
    neutral: "bg-surface-muted text-text-secondary border border-border",
  };

  return (
    <span
      className={`
        ${baseStyles}
        ${sizeStyles[size] || sizeStyles.md}
        ${radiusStyles}
        ${variantStyles[variant] || variantStyles.primary}
        ${className}
      `}
      {...props}
    >
      {Icon && <Icon className="w-3 h-3 shrink-0" />}
      <span>{children}</span>
    </span>
  );
};

export default Badge;
