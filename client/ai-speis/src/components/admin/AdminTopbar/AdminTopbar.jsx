import React, { useState } from 'react';
import { Search, Bell, ChevronDown, Menu, User } from 'lucide-react';
import { useTranslation } from 'react-i18next';

function AdminTopbar({ onMenuClick }) {
  const { t } = useTranslation('admin-dashboard');
  const [searchValue, setSearchValue] = useState('');

  const handleSearchChange = (e) => {
    setSearchValue(e.target.value);
  };

  const handleSearchSubmit = (e) => {
    if (e.key === 'Enter') {
      console.log('Search:', searchValue);
    }
  };

  const handleNotificationClick = () => {
    console.log('Notification clicked');
  };

  const handleProfileClick = () => {
    console.log('Profile clicked');
  };

  return (
    <div className="sticky top-0 z-[90] flex h-16 shrink-0 items-center justify-between border-b border-border/40 bg-white/70 px-4 backdrop-blur-xl md:px-8">
      <div className="flex max-w-[520px] flex-1 items-center gap-2">
        <button
          className="grid h-10 w-10 flex-none place-items-center rounded-xl border border-border/60 bg-white text-text-primary transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:bg-primary-xlight hover:shadow-sm active:scale-[0.95] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 md:hidden"
          type="button"
          onClick={onMenuClick}
          aria-label="Open navigation"
        >
          <Menu size={22} />
        </button>
        <div className="hidden min-h-10 w-full items-center rounded-xl border border-border/50 bg-white/80 px-3 py-2 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] focus-within:border-primary focus-within:bg-white focus-within:shadow-[0_0_0_4px_rgba(111,182,232,0.15)] md:flex">
          <Search size={18} className="mr-2.5 shrink-0 text-text-secondary/70" />
          <input
            type="text"
            className="min-w-0 flex-1 bg-transparent text-sm text-text-primary outline-none placeholder:text-text-disabled"
            placeholder={t('searchPlaceholder', 'Search users, questions, transactions...')}
            value={searchValue}
            onChange={handleSearchChange}
            onKeyDown={handleSearchSubmit}
          />
        </div>
      </div>

      <div className="ml-auto flex items-center gap-2">
        <button
          className="relative flex h-10 w-10 items-center justify-center rounded-xl border border-transparent bg-white/50 text-text-secondary transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:border-border/60 hover:bg-white hover:text-primary-dark hover:shadow-sm active:scale-[0.95] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
          onClick={handleNotificationClick}
          aria-label="Notifications"
        >
          <Bell size={20} />
          <span className="absolute right-2 top-2 h-2 w-2 animate-pulse rounded-full border-2 border-white bg-error shadow-[0_0_6px_rgba(231,111,111,0.5)]" />
        </button>

        <div className="mx-1.5 hidden h-8 w-px bg-border/40 md:block" />

        <div className="relative">
          <button
            className="flex items-center gap-2.5 rounded-xl bg-transparent py-1 pl-1 pr-2 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:bg-primary-xlight/60 active:scale-[0.97] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
            onClick={handleProfileClick}
            aria-label="Admin Profile Menu"
          >
            <div className="flex h-9 w-9 items-center justify-center rounded-full border border-border/60 bg-white text-primary-dark shadow-sm transition-all duration-300 hover:border-primary/40 hover:shadow-[0_0_0_3px_rgba(111,182,232,0.15)]">
              <User size={20} />
            </div>
            <div className="hidden flex-col items-start text-left md:flex">
              <span className="text-sm font-semibold text-text-primary">Admin</span>
              <span className="text-xs text-text-secondary/70">{t('superAdmin', 'Super Admin')}</span>
            </div>
            <ChevronDown size={16} className="hidden text-text-secondary/60 transition-transform duration-300 md:block" />
          </button>
        </div>
      </div>
    </div>
  );
}

export default AdminTopbar;
