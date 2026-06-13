import React from 'react';

const Checkbox = React.forwardRef(({ label, id, ...props }, ref) => {
  return (
    <div className="flex items-center gap-2">
      <input
        type="checkbox"
        id={id}
        ref={ref}
        className="w-4 h-4 cursor-pointer accent-primary border-border-strong rounded-sm focus:ring-primary"
        {...props}
      />
      {label && (
        <label htmlFor={id} className="text-[14px] font-normal text-text-primary cursor-pointer select-none">
          {label}
        </label>
      )}
    </div>
  );
});

Checkbox.displayName = 'Checkbox';

export default Checkbox;
