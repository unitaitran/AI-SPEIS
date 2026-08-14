import React, { useState } from 'react';

export const Tooltip = ({ content, children, position = 'top', className = '' }) => {
  const [isVisible, setIsVisible] = useState(false);

  const positionStyles = {
    top: "bottom-full left-1/2 -translate-x-1/2 mb-2",
    bottom: "top-full left-1/2 -translate-x-1/2 mt-2",
    left: "right-full top-1/2 -translate-y-1/2 mr-2",
    right: "left-full top-1/2 -translate-y-1/2 ml-2",
  };

  return (
    <div 
      className="relative inline-block"
      onMouseEnter={() => setIsVisible(true)}
      onMouseLeave={() => setIsVisible(false)}
      onFocus={() => setIsVisible(true)}
      onBlur={() => setIsVisible(false)}
    >
      {children}
      {isVisible && content && (
        <div 
          role="tooltip"
          className={`
            absolute ${positionStyles[position]} z-50 px-2.5 py-1 text-[11px] font-semibold text-white bg-slate-900 rounded shadow-md whitespace-nowrap pointer-events-none animate-pageEntrance ${className}
          `}
        >
          {content}
        </div>
      )}
    </div>
  );
};

export default Tooltip;
