import React, { useEffect } from 'react';
import { X } from 'lucide-react';

const Drawer = ({
  isOpen,
  onClose,
  title,
  children,
  position = 'right', // 'left' | 'right'
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

  const positionStyles = {
    left: "left-0 top-0 bottom-0 animate-slideRight",
    right: "right-0 top-0 bottom-0 animate-slideLeft",
  };

  return (
    <div 
      className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-sm transition-opacity"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
    >
      <div
        className={`
          fixed w-full max-w-xs md:max-w-sm bg-surface border-l border-border shadow-xl flex flex-col h-full ${positionStyles[position]} ${className}
        `}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          {title && <h3 className="text-base font-bold text-text-primary">{title}</h3>}
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 text-text-secondary hover:text-text-primary hover:bg-surface-muted rounded-full transition-colors focus-ring"
            aria-label="Close drawer"
          >
            <X size={18} />
          </button>
        </div>

        <div className="p-5 overflow-y-auto flex-1">
          {children}
        </div>
      </div>
    </div>
  );
};

export default Drawer;
