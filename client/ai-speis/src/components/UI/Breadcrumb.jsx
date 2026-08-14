import React from 'react';
import { ChevronRight, Home } from 'lucide-react';

export const Breadcrumb = ({ items = [], className = '' }) => {
  // items: array of { label, onClick, href }
  return (
    <nav className={`flex items-center text-xs text-text-secondary gap-1.5 ${className}`} aria-label="Breadcrumb">
      <span className="flex items-center gap-1 text-text-muted">
        <Home size={14} />
      </span>

      {items.map((item, index) => {
        const isLast = index === items.length - 1;

        return (
          <React.Fragment key={index}>
            <ChevronRight size={14} className="text-text-disabled shrink-0" />
            {isLast ? (
              <span className="font-semibold text-text-primary text-truncate max-w-[200px]" aria-current="page">
                {item.label}
              </span>
            ) : item.onClick ? (
              <button
                type="button"
                onClick={item.onClick}
                className="hover:text-primary transition-colors focus-ring rounded-sm"
              >
                {item.label}
              </button>
            ) : (
              <span className="hover:text-text-primary transition-colors">
                {item.label}
              </span>
            )}
          </React.Fragment>
        );
      })}
    </nav>
  );
};

export default Breadcrumb;
