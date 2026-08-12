import React from 'react';

const Checkbox = React.forwardRef(({
  label,
  id,
  error,
  disabled = false,
  className = '',
  ...props
}, ref) => {
  return (
    <div className="flex flex-col gap-1">
      <label 
        htmlFor={id} 
        className={`inline-flex items-center gap-2.5 cursor-pointer select-none text-sm font-normal text-text-primary ${disabled ? 'opacity-50 cursor-not-allowed' : ''}`}
      >
        <input
          type="checkbox"
          id={id}
          ref={ref}
          disabled={disabled}
          className={`
            w-4 h-4 rounded border-border-strong text-primary accent-primary cursor-pointer transition-all duration-150 focus-ring
            ${disabled ? 'cursor-not-allowed' : ''}
            ${className}
          `}
          {...props}
        />
        {label && <span>{label}</span>}
      </label>
      {error && <span className="text-xs text-error font-medium pl-6.5">{error}</span>}
    </div>
  );
});

Checkbox.displayName = 'Checkbox';

export default Checkbox;
