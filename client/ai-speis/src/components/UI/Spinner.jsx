import React from 'react';
import { Loader2 } from 'lucide-react';

export const Spinner = ({ size = 'md', className = '', label = 'Đang tải...' }) => {
  const sizeStyles = {
    sm: "w-4 h-4",
    md: "w-6 h-6",
    lg: "w-10 h-10",
  };

  return (
    <div className={`inline-flex items-center justify-center gap-2 text-primary ${className}`} role="status">
      <Loader2 className={`animate-spin ${sizeStyles[size] || sizeStyles.md}`} />
      <span className="sr-only">{label}</span>
    </div>
  );
};

export const LoadingOverlay = ({ label = 'Đang xử lý...' }) => {
  return (
    <div className="fixed inset-0 z-50 flex flex-col items-center justify-center bg-surface/80 backdrop-blur-sm">
      <Spinner size="lg" />
      <p className="mt-3 text-sm font-semibold text-text-primary">{label}</p>
    </div>
  );
};

export default Spinner;
