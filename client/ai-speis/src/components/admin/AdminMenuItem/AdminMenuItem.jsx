import React from 'react';
import * as LucideIcons from 'lucide-react';

function AdminMenuItem({ item, isActive, onClick }) {
  const IconComponent = LucideIcons[item.icon] || LucideIcons.Circle;

  return (
    <a
      href={item.path}
      className={`relative my-0.5 flex min-h-11 w-full items-center gap-3 whitespace-nowrap rounded-lg px-3 py-2.5 text-left text-sm no-underline transition-all duration-200 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 ${
        isActive
          ? "bg-primary-light font-semibold text-primary-dark before:absolute before:-left-3 before:h-6 before:w-1 before:rounded-r before:bg-primary-dark before:content-['']"
          : 'font-medium text-text-secondary hover:bg-white/70 hover:text-text-primary'
      }`}
      onClick={onClick}
      aria-current={isActive ? 'page' : undefined}
    >
      <div className="flex items-center justify-center text-inherit">
        <IconComponent size={20} />
      </div>
      <span className="flex-1 overflow-hidden text-ellipsis">{item.label}</span>
      {item.hasBadge && (
        <span
          className="absolute right-3 top-1/2 h-2 w-2 -translate-y-1/2 rounded-full bg-error"
          aria-label="Requires attention"
        />
      )}
    </a>
  );
}

export default AdminMenuItem;
