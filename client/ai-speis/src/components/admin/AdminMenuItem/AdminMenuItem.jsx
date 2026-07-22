import React from 'react';
import * as LucideIcons from 'lucide-react';
import { useTranslation } from 'react-i18next';

function AdminMenuItem({ item, isActive, onClick }) {
  const { t } = useTranslation('admin-dashboard');
  const IconComponent = LucideIcons[item.icon] || LucideIcons.Circle;

  return (
    <a
      href={item.path}
      className={`relative my-0.5 flex min-h-11 w-full items-center gap-3 whitespace-nowrap rounded-xl px-3 py-2.5 text-left text-sm no-underline transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] active:scale-[0.97] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 ${
        isActive
          ? "bg-primary-light/80 font-semibold text-primary-dark shadow-sm before:absolute before:-left-3 before:h-6 before:w-1 before:rounded-r-full before:bg-gradient-to-b before:from-primary before:to-primary-dark before:content-['']"
          : 'font-medium text-text-secondary hover:bg-white/60 hover:text-text-primary hover:shadow-[0_2px_8px_rgba(31,45,61,0.06)]'
      }`}
      onClick={onClick}
      aria-current={isActive ? 'page' : undefined}
    >
      <div className={`flex items-center justify-center transition-colors duration-300 ${isActive ? 'text-primary-dark' : 'text-inherit'}`}>
        <IconComponent size={20} />
      </div>
      <span className="flex-1 overflow-hidden text-ellipsis">{item.label}</span>
      {item.hasBadge && (
        <span
          className="absolute right-3 top-1/2 h-2 w-2 -translate-y-1/2 animate-pulse rounded-full bg-error shadow-[0_0_6px_rgba(231,111,111,0.5)]"
          aria-label={t('requiresAttention', 'Requires attention')}
        />
      )}
    </a>
  );
}

export default AdminMenuItem;
