import React from 'react';

export const Skeleton = ({
  variant = 'text', // 'text' | 'circular' | 'rectangular' | 'card'
  width,
  height,
  className = '',
}) => {
  const variantStyles = {
    text: "h-4 w-full rounded-sm",
    circular: "rounded-full shrink-0",
    rectangular: "rounded-md w-full h-24",
    card: "rounded-lg w-full h-40",
  };

  const style = {};
  if (width) style.width = width;
  if (height) style.height = height;

  return (
    <div
      className={`
        bg-gradient-to-r from-surface-muted via-border/40 to-surface-muted bg-[length:200%_100%] animate-shimmer ${variantStyles[variant] || variantStyles.text} ${className}
      `}
      style={style}
      aria-hidden="true"
    />
  );
};

export const CardSkeleton = ({ className = '' }) => {
  return (
    <div className={`p-5 bg-surface border border-border rounded-lg shadow-sm flex flex-col gap-4 ${className}`}>
      <div className="flex items-center gap-3">
        <Skeleton variant="circular" width={40} height={40} />
        <div className="flex-1 flex flex-col gap-2">
          <Skeleton variant="text" width="60%" />
          <Skeleton variant="text" width="40%" />
        </div>
      </div>
      <Skeleton variant="rectangular" height={80} />
    </div>
  );
};

export default Skeleton;
