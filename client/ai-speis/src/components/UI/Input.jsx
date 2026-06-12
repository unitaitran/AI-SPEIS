import React from 'react';

const Input = React.forwardRef(({ label, id, type = 'text', error, ...props }, ref) => {
  return (
    <div className="flex flex-col gap-1.5 w-full">
      {label && (
        <label htmlFor={id} className="text-[12px] font-bold text-text-primary uppercase tracking-wide">
          {label}
        </label>
      )}
      <input
        ref={ref}
        id={id}
        type={type}
        className={`w-full px-4 py-3 bg-surface-2 border rounded-[12px] text-[14px] font-normal text-text-primary placeholder:text-text-disabled outline-none transition-all duration-200 shadow-[0_2px_4px_rgba(31,45,61,0.02)]
          ${error ? 'border-error focus:border-error focus:ring-2 focus:ring-error-light focus:shadow-none' : 'border-border focus:border-primary focus:ring-4 focus:ring-primary-xlight focus:shadow-none hover:border-border-strong'}
        `}
        {...props}
      />
      {error && <span className="text-[12px] font-normal text-error mt-0.5">{error}</span>}
    </div>
  );
});

Input.displayName = 'Input';

export default Input;
