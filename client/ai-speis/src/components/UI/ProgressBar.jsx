import React from 'react';

export const ProgressBar = ({
  value = 0,
  max = 100,
  variant = 'primary', // 'primary' | 'secondary' | 'success' | 'warning' | 'error'
  size = 'md',          // 'sm' | 'md' | 'lg'
  showLabel = false,
  className = '',
}) => {
  const percentage = Math.min(Math.max(0, (value / max) * 100), 100);

  const sizeStyles = {
    sm: "h-1.5",
    md: "h-2.5",
    lg: "h-4",
  };

  const variantStyles = {
    primary: "bg-primary",
    secondary: "bg-secondary",
    success: "bg-success",
    warning: "bg-warning",
    error: "bg-error",
  };

  return (
    <div className={`w-full flex flex-col gap-1 ${className}`}>
      {showLabel && (
        <div className="flex justify-between items-center text-xs font-semibold text-text-secondary">
          <span>Tiến trình</span>
          <span>{Math.round(percentage)}%</span>
        </div>
      )}
      <div 
        className={`w-full bg-surface-muted rounded-full overflow-hidden ${sizeStyles[size] || sizeStyles.md}`}
        role="progressbar"
        aria-valuenow={value}
        aria-valuemin={0}
        aria-valuemax={max}
      >
        <div
          className={`h-full rounded-full transition-all duration-300 ease-out ${variantStyles[variant] || variantStyles.primary}`}
          style={{ width: `${percentage}%` }}
        />
      </div>
    </div>
  );
};

export default ProgressBar;
