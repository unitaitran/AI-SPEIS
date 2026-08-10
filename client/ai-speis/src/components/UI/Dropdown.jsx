import React, { useState, useRef, useEffect } from 'react';

export const Dropdown = ({
  trigger,
  items = [], // array of { id, label, icon: Icon, onClick, danger, divider }
  align = 'right', // 'left' | 'right'
  className = '',
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const alignStyles = align === 'right' ? 'right-0' : 'left-0';

  return (
    <div className="relative inline-block text-left" ref={dropdownRef}>
      <div onClick={() => setIsOpen(!isOpen)} className="cursor-pointer">
        {trigger}
      </div>

      {isOpen && (
        <div 
          className={`
            absolute ${alignStyles} mt-2 w-48 bg-surface border border-border rounded-lg shadow-lg py-1 z-40 animate-slideDown ${className}
          `}
          role="menu"
        >
          {items.map((item, index) => {
            if (item.divider) {
              return <div key={`divider-${index}`} className="my-1 border-t border-border" />;
            }

            const Icon = item.icon;

            return (
              <button
                key={item.id || index}
                type="button"
                role="menuitem"
                onClick={(e) => {
                  e.stopPropagation();
                  setIsOpen(false);
                  if (item.onClick) item.onClick();
                }}
                className={`
                  w-full px-3.5 py-2 text-xs font-semibold flex items-center gap-2.5 transition-colors text-left
                  ${item.danger 
                    ? 'text-error hover:bg-error-light' 
                    : 'text-text-primary hover:bg-surface-muted'
                  }
                `}
              >
                {Icon && <Icon size={16} className={item.danger ? 'text-error' : 'text-text-secondary'} />}
                <span>{item.label}</span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
};

export default Dropdown;
