import React, { useState } from 'react';
import { Search, Bell, ChevronDown, Menu, User } from 'lucide-react';

function AdminTopbar({ onMenuClick }) {
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
    <div className="sticky top-0 z-[90] flex h-16 shrink-0 items-center justify-between border-b border-border bg-surface-3/95 px-4 backdrop-blur-md md:px-8">
      <div className="flex max-w-[520px] flex-1 items-center gap-2">
        <button
          className="grid h-10 w-10 flex-none place-items-center rounded-lg border border-border bg-white text-text-primary transition-colors duration-200 hover:bg-primary-xlight focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 md:hidden"
          type="button"
          onClick={onMenuClick}
          aria-label="Open navigation"
        >
          <Menu size={22} />
        </button>
        <div className="hidden min-h-10 w-full items-center rounded-lg border border-border bg-white/80 px-3 py-2 transition-all duration-200 focus-within:border-primary focus-within:bg-surface-2 focus-within:ring-4 focus-within:ring-primary/20 md:flex">
          <Search size={18} className="mr-2 shrink-0 text-text-secondary" />
          <input
            type="text"
            className="min-w-0 flex-1 bg-transparent text-sm text-text-primary outline-none placeholder:text-text-disabled"
            placeholder="Search users, questions, transactions..."
            value={searchValue}
            onChange={handleSearchChange}
            onKeyDown={handleSearchSubmit}
          />
        </div>
      </div>

      <div className="ml-auto flex items-center gap-2">
        <button
          className="relative flex h-10 w-10 items-center justify-center rounded-full border border-transparent bg-white/60 text-text-secondary transition-all duration-200 hover:border-border hover:bg-white hover:text-primary-dark focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
          onClick={handleNotificationClick}
          aria-label="Notifications"
        >
          <Bell size={20} />
          <span className="absolute right-2 top-2 h-2 w-2 rounded-full border-2 border-surface-3 bg-error" />
        </button>

        <div className="mx-1 hidden h-8 w-px bg-border md:block" />

        <div className="relative">
          <button
            className="flex items-center gap-2.5 rounded-lg bg-transparent py-1 pl-1 pr-2 transition-all duration-200 hover:bg-primary-xlight focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
            onClick={handleProfileClick}
            aria-label="Admin Profile Menu"
          >
            <div className="flex h-9 w-9 items-center justify-center rounded-full border border-border bg-white text-primary-dark">
              <User size={20} />
            </div>
            <div className="hidden flex-col items-start text-left md:flex">
              <span className="text-sm font-semibold text-text-primary">Admin</span>
              <span className="text-xs text-text-secondary">Super Admin</span>
            </div>
            <ChevronDown size={16} className="hidden text-text-secondary md:block" />
          </button>
        </div>
      </div>
    </div>
  );
}

export default AdminTopbar;
