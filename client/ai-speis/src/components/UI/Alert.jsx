import React from 'react';
import { AlertCircle, CheckCircle2, AlertTriangle, Info, X } from 'lucide-react';

export const Alert = ({
  variant = 'info', // 'info' | 'success' | 'warning' | 'error'
  title,
  children,
  onClose,
  className = '',
}) => {
  const variantStyles = {
    info: {
      bg: "bg-info-light border-info/30 text-info",
      text: "text-text-primary",
      icon: Info,
    },
    success: {
      bg: "bg-success-light border-success/30 text-success",
      text: "text-text-primary",
      icon: CheckCircle2,
    },
    warning: {
      bg: "bg-warning-light border-warning/30 text-warning",
      text: "text-text-primary",
      icon: AlertTriangle,
    },
    error: {
      bg: "bg-error-light border-error/30 text-error",
      text: "text-text-primary",
      icon: AlertCircle,
    },
  };

  const style = variantStyles[variant] || variantStyles.info;
  const Icon = style.icon;

  return (
    <div className={`flex items-start gap-3 p-4 rounded-lg border ${style.bg} ${className}`} role="alert">
      <Icon className="w-5 h-5 shrink-0 mt-0.5" />
      <div className="flex-1 text-sm">
        {title && <h4 className="font-bold mb-0.5">{title}</h4>}
        <div className={style.text}>{children}</div>
      </div>
      {onClose && (
        <button
          type="button"
          onClick={onClose}
          className="p-1 rounded hover:bg-black/5 text-current transition-colors focus-ring"
          aria-label="Đóng thông báo"
        >
          <X size={16} />
        </button>
      )}
    </div>
  );
};

export default Alert;
