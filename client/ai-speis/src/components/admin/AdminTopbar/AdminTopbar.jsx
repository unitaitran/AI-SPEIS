import React, { useState } from 'react';
import { Search, Bell, ChevronDown, Menu, User } from 'lucide-react';
import './AdminTopbar.css';

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
    <div className="admin-topbar">
      <div className="topbar-left">
        <button
          className="mobile-menu-btn"
          type="button"
          onClick={onMenuClick}
          aria-label="Open navigation"
        >
          <Menu size={22} />
        </button>
        <div className="search-container">
          <Search size={18} className="search-icon" />
          <input
            type="text"
            className="search-input"
            placeholder="Search users, questions, transactions..."
            value={searchValue}
            onChange={handleSearchChange}
            onKeyDown={handleSearchSubmit}
          />
        </div>
      </div>

      <div className="topbar-right">
        <button
          className="topbar-icon-btn notification-btn"
          onClick={handleNotificationClick}
          aria-label="Notifications"
        >
          <Bell size={20} className="icon" />
          <span className="badge"></span>
        </button>

        <div className="profile-divider" />

        <div className="profile-area">
          <button
            className="profile-btn"
            onClick={handleProfileClick}
            aria-label="Admin Profile Menu"
          >
            <div className="profile-avatar">
              <User size={20} className="avatar-icon" />
            </div>
            <div className="profile-info">
              <span className="profile-name">Admin</span>
              <span className="profile-role">Super Admin</span>
            </div>
            <ChevronDown size={16} className="profile-chevron" />
          </button>
        </div>
      </div>
    </div>
  );
}

export default AdminTopbar;
