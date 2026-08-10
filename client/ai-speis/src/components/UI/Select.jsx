import React from 'react';
import { ChevronDown } from 'lucide-react';

const Select = React.forwardRef(({
  label,
  id,
  options = [],
  error,
  helperText,
  placeholder = 'Select an option',
  className = '',
  disabled = false,
  required = false,
  value,
  onChange,
  ...props
}, ref) => {
  return (
    <div className="flex flex-col gap-1.5 w-full">
      {label && (
        <label htmlFor={id} className="text-xs font-semibold text-text-primary uppercase tracking-wider flex items-center gap-1">
          {label}
          {required && <span className="text-error" aria-hidden="true">*</span>}
        </label>
      )}
      <div className="relative flex items-center">
        <select
          ref={ref}
          id={id}
          value={value}
          onChange={onChange}
          disabled={disabled}
          aria-invalid={Boolean(error)}
          className={`
            w-full px-3.5 py-2.5 pr-10 bg-surface border rounded-md text-sm font-normal text-text-primary outline-none transition-all duration-200 shadow-sm appearance-none cursor-pointer
            ${error 
              ? 'border-error focus:border-error focus:ring-2 focus:ring-error-light' 
              : 'border-border hover:border-border-strong focus:border-primary focus-ring'
            }
            ${disabled ? 'bg-surface-muted text-text-disabled cursor-not-allowed opacity-70' : ''}
            ${className}
          `}
          {...props}
        >
          {placeholder && (
            <option value="" disabled>
              {placeholder}
            </option>
          )}
          {options.map((option) => {
            const optValue = typeof option === 'object' ? option.value : option;
            const optLabel = typeof option === 'object' ? option.label : option;
            return (
              <option key={optValue} value={optValue}>
                {optLabel}
              </option>
            );
          })}
        </select>
        <div className="absolute right-3.5 pointer-events-none text-text-secondary">
          <ChevronDown size={18} />
        </div>
      </div>
      {error ? (
        <span className="text-xs font-medium text-error mt-0.5" role="alert">
          {error}
        </span>
      ) : helperText ? (
        <span className="text-xs font-normal text-text-muted mt-0.5">
          {helperText}
        </span>
      ) : null}
    </div>
  );
});

Select.displayName = 'Select';

export default Select;
