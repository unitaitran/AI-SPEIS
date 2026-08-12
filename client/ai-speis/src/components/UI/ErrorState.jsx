import React from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';
import Button from './Button';

export const ErrorState = ({
  title = 'Đã có lỗi xảy ra',
  message = 'Không thể tải dữ liệu vào lúc này. Vui lòng thử lại sau.',
  onRetry,
  className = '',
}) => {
  return (
    <div className={`flex flex-col items-center justify-center text-center p-8 bg-error-light/40 rounded-lg border border-error/20 ${className}`}>
      <div className="w-12 h-12 rounded-full bg-error-light text-error flex items-center justify-center mb-3">
        <AlertCircle size={24} />
      </div>
      <h3 className="text-base font-bold text-text-primary mb-1">{title}</h3>
      <p className="text-xs text-text-secondary max-w-sm mb-4 leading-relaxed">{message}</p>
      {onRetry && (
        <Button variant="outline" size="sm" icon={RefreshCw} onClick={onRetry}>
          Thử lại
        </Button>
      )}
    </div>
  );
};

export default ErrorState;
