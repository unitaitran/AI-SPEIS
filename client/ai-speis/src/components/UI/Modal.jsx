import React, { useEffect } from 'react';
import { X } from 'lucide-react';

const Modal = ({
  isOpen,
  onClose,
  title,
  subtitle,
  children,
  footer,
  size = 'md', // 'sm' | 'md' | 'lg' | 'xl'
  closeOnOverlayClick = true,
  className = '',
}) => {
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };

    if (isOpen) {
      document.body.style.overflow = 'hidden';
      window.addEventListener('keydown', handleKeyDown);
    }

    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const sizeStyles = {
    sm: "max-w-md",
    md: "max-w-lg",
    lg: "max-w-2xl",
    xl: "max-w-4xl",
  };

  return (
    <div 
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm animate-pageEntrance"
      onClick={closeOnOverlayClick ? onClose : undefined}
      role="dialog"
      aria-modal="true"
      aria-labelledby={title ? "modal-title" : undefined}
    >
      <div
        className={`
          w-full bg-surface border border-border rounded-xl shadow-xl overflow-hidden flex flex-col max-h-[90vh] animate-modalSlideIn ${sizeStyles[size] || sizeStyles.md} ${className}
        `}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Modal Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-border bg-surface">
          <div>
            {title && (
              <h3 id="modal-title" className="text-lg font-bold text-text-primary">
                {title}
              </h3>
            )}
            {subtitle && (
              <p className="text-xs text-text-secondary mt-0.5">{subtitle}</p>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 text-text-secondary hover:text-text-primary hover:bg-surface-muted rounded-full transition-colors focus-ring"
            aria-label="Đóng modal"
          >
            <X size={18} />
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 overflow-y-auto flex-1">
          {children}
        </div>

        {/* Modal Footer */}
        {footer && (
          <div className="px-6 py-4 border-t border-border bg-surface-muted flex items-center justify-end gap-3">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
};

export default Modal;
