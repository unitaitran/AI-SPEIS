import React, { useEffect, useState } from 'react';
import { Menu, Bell, ChevronDown, Ticket, User } from 'lucide-react';

function UserTopbar({ onMenuClick }) {
  const [user, setUser] = useState(null);

  useEffect(() => {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        setUser(JSON.parse(userStr));
      } catch (e) {
        console.error('Failed to parse user', e);
      }
    }
  }, []);

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
        {/* Mobile Logo */}
        <img src="/logo_AI-SPEIS-removebg.png" alt="AI-SPEIS" className="ml-2 h-7 object-contain" />
      </div>

      {/* Spacer for desktop since logo is in sidebar */}
      <div className="hidden lg:block flex-1"></div>

      <div className="flex items-center space-x-4 ml-auto">
        {/* Quota Badge */}
        <div className="hidden sm:flex items-center bg-gradient-to-r from-primary-light to-primary-xlight border border-primary-light rounded-full px-3 py-1.5 text-sm font-semibold text-primary-dark shadow-sm">
          <Ticket size={16} className="text-primary-dark mr-2" />
          <span>5 lượt phỏng vấn</span>
        </div>

        {/* Notification */}
        <button className="relative p-2 text-text-secondary hover:text-primary-dark hover:bg-primary-xlight rounded-full transition-colors" aria-label="Notifications">
          <Bell size={20} />
          {/* Notification Badge */}
          <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-error rounded-full border border-surface-2 shadow-sm"></span>
        </button>

        <div className="w-px h-6 bg-border mx-1"></div>

        {/* Profile Dropdown */}
        <button className="flex items-center space-x-2 p-1 pl-2 pr-3 hover:bg-surface-3 rounded-full transition-colors border border-transparent hover:border-border group">
          <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary to-primary-dark flex items-center justify-center text-white shadow-sm">
            <User size={16} />
          </div>
          <span className="text-sm font-bold text-text-primary hidden sm:block group-hover:text-primary-dark transition-colors">
            {user ? user.fullName : 'User Name'}
          </span>
          <ChevronDown size={16} className="text-text-secondary group-hover:text-primary-dark transition-colors" />
        </button>
      </div>
    </header>
  );
}

export default UserTopbar;
