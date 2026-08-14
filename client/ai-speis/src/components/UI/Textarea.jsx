import React from 'react';

const Textarea = React.forwardRef(({
  label,
  id,
  error,
  helperText,
  rows = 4,
  maxLength,
  value,
  className = '',
  disabled = false,
  required = false,
  ...props
}, ref) => {
  const currentLength = typeof value === 'string' ? value.length : 0;

  return (
    <div className="flex flex-col gap-1.5 w-full">
      <div className="flex justify-between items-center">
        {label && (
          <label htmlFor={id} className="text-xs font-semibold text-text-primary uppercase tracking-wider flex items-center gap-1">
            {label}
            {required && <span className="text-error" aria-hidden="true">*</span>}
          </label>
        )}
        {maxLength && (
          <span className="text-xs text-text-muted">
            {currentLength}/{maxLength}
          </span>
        )}
      </div>
      <textarea
        ref={ref}
        id={id}
        rows={rows}
        maxLength={maxLength}
        value={value}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        className={`
          w-full px-3.5 py-2.5 bg-surface border rounded-md text-sm font-normal text-text-primary placeholder:text-text-disabled outline-none transition-all duration-200 shadow-sm resize-y
          ${error 
            ? 'border-error focus:border-error focus:ring-2 focus:ring-error-light' 
            : 'border-border hover:border-border-strong focus:border-primary focus-ring'
          }
          ${disabled ? 'bg-surface-muted text-text-disabled cursor-not-allowed opacity-70' : ''}
          ${className}
        `}
        {...props}
      />
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

Textarea.displayName = 'Textarea';

export default Textarea;
