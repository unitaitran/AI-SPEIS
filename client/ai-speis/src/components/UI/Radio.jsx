import React from 'react';

export const Radio = React.forwardRef(({
  label,
  id,
  name,
  value,
  checked,
  onChange,
  disabled = false,
  className = '',
  ...props
}, ref) => {
  return (
    <label 
      htmlFor={id} 
      className={`inline-flex items-center gap-2.5 cursor-pointer select-none text-sm font-normal text-text-primary ${disabled ? 'opacity-50 cursor-not-allowed' : ''}`}
    >
      <input
        type="radio"
        id={id}
        name={name}
        value={value}
        checked={checked}
        onChange={onChange}
        ref={ref}
        disabled={disabled}
        className={`
          w-4 h-4 text-primary accent-primary cursor-pointer border-border-strong focus-ring
          ${disabled ? 'cursor-not-allowed' : ''}
          ${className}
        `}
        {...props}
      />
      {label && <span>{label}</span>}
    </label>
  );
});

Radio.displayName = 'Radio';

export const RadioGroup = ({ label, error, children, className = '' }) => {
  return (
    <div className={`flex flex-col gap-2 w-full ${className}`}>
      {label && (
        <span className="text-xs font-semibold text-text-primary uppercase tracking-wider">
          {label}
        </span>
      )}
      <div className="flex flex-col gap-2">
        {children}
      </div>
      {error && <span className="text-xs font-medium text-error">{error}</span>}
    </div>
  );
};

export default Radio;
