import React from 'react';

const Card = React.forwardRef(({
  children,
  variant = 'default', // 'default' | 'elevated' | 'outlined' | 'ai'
  padding = 'md',      // 'none' | 'sm' | 'md' | 'lg'
  hoverable = false,
  className = '',
  onClick,
  ...props
}, ref) => {
  const baseStyles = "bg-surface rounded-lg border transition-all duration-200";

  const variantStyles = {
    default: "border-border shadow-sm",
    elevated: "border-transparent shadow-md hover:shadow-lg",
    outlined: "border-border-strong shadow-none",
    ai: "border-secondary-light bg-gradient-to-br from-surface to-secondary-xlight/30 shadow-sm",
  };

  const paddingStyles = {
    none: "p-0",
    sm: "p-3",
    md: "p-5",
    lg: "p-6",
  };

  const hoverStyles = hoverable || onClick
    ? "cursor-pointer hover:-translate-y-0.5 hover:shadow-md hover:border-primary-light"
    : "";

  return (
    <div
      ref={ref}
      onClick={onClick}
      className={`
        ${baseStyles}
        ${variantStyles[variant] || variantStyles.default}
        ${paddingStyles[padding] || paddingStyles.md}
        ${hoverStyles}
        ${className}
      `}
      {...props}
    >
      {children}
    </div>
  );
});

Card.displayName = 'Card';

export default Card;
