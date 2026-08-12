import React, { useState } from 'react';
import { User } from 'lucide-react';

const Avatar = ({
  src,
  alt = 'User avatar',
  name = '',
  size = 'md', // 'sm' | 'md' | 'lg' | 'xl'
  status = null, // 'online' | 'offline' | 'busy'
  className = '',
}) => {
  const [imageError, setImageError] = useState(false);

  const sizeStyles = {
    sm: "w-7 h-7 text-xs",
    md: "w-9 h-9 text-sm",
    lg: "w-12 h-12 text-base",
    xl: "w-16 h-16 text-lg",
  };

  const statusSizeStyles = {
    sm: "w-2 h-2 border-1",
    md: "w-2.5 h-2.5 border-2",
    lg: "w-3.5 h-3.5 border-2",
    xl: "w-4 h-4 border-2",
  };

  const statusColorStyles = {
    online: "bg-success",
    offline: "bg-text-disabled",
    busy: "bg-error",
  };

  const getInitials = (str) => {
    if (!str) return '';
    const parts = str.trim().split(' ');
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  };

  const initials = getInitials(name);

  return (
    <div className={`relative inline-block shrink-0 ${className}`}>
      <div className={`
        ${sizeStyles[size] || sizeStyles.md}
        rounded-full bg-primary-light text-primary font-bold flex items-center justify-center overflow-hidden border border-primary/20 select-none
      `}>
        {src && !imageError ? (
          <img
            src={src}
            alt={alt}
            onError={() => setImageError(true)}
            className="w-full h-full object-cover"
          />
        ) : initials ? (
          <span>{initials}</span>
        ) : (
          <User className="w-1/2 h-1/2 text-primary" />
        )}
      </div>
      {status && (
        <span
          className={`
            absolute bottom-0 right-0 rounded-full border-surface
            ${statusSizeStyles[size] || statusSizeStyles.md}
            ${statusColorStyles[status] || statusColorStyles.online}
          `}
          aria-hidden="true"
        />
      )}
    </div>
  );
};

export default Avatar;
