import React from 'react';
import { Menu, Bell, ChevronDown, Ticket, User } from 'lucide-react';

function UserTopbar({ onMenuClick }) {
  return (
    <header className="h-16 bg-surface-2 border-b border-border flex items-center justify-between px-6 shrink-0 z-10 sticky top-0">
      <div className="flex items-center lg:hidden">
        <button
          className="p-2 -ml-2 text-text-secondary hover:text-text-primary rounded-md hover:bg-surface-3 transition-colors"
          onClick={onMenuClick}
          aria-label="Open menu"
        >
          <Menu size={24} />
        </button>
        {/* Mobile Logo Optional */}
        <span className="ml-2 font-bold text-text-primary text-lg">AI-SPEIS</span>
      </div>

      {/* Spacer for desktop since logo is in sidebar */}
      <div className="hidden lg:block flex-1"></div>

      <div className="flex items-center space-x-4 ml-auto">
        {/* Quota Badge */}
        <div className="hidden sm:flex items-center bg-surface-1 border border-border rounded-full px-3 py-1.5 text-sm font-semibold text-text-primary">
          <Ticket size={16} className="text-primary-dark mr-2" />
          <span>5 lượt phỏng vấn</span>
        </div>

        {/* Notification */}
        <button className="relative p-2 text-text-secondary hover:text-text-primary hover:bg-surface-3 rounded-full transition-colors" aria-label="Notifications">
          <Bell size={20} />
          {/* Notification Badge */}
          <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-error rounded-full border border-surface-2"></span>
        </button>

        <div className="w-px h-6 bg-border mx-1"></div>

        {/* Profile Dropdown */}
        <button className="flex items-center space-x-2 p-1 pl-2 pr-3 hover:bg-surface-3 rounded-full transition-colors border border-transparent hover:border-border">
          <div className="w-8 h-8 rounded-full bg-primary-light flex items-center justify-center text-primary-dark">
            <User size={16} />
          </div>
          <span className="text-sm font-semibold text-text-primary hidden sm:block">User Name</span>
          <ChevronDown size={16} className="text-text-secondary" />
        </button>
      </div>
    </header>
  );
}

export default UserTopbar;
