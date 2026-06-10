import React, { useState } from 'react';
import './AdminTopbar.css';

function AdminTopbar() {
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

  const handleSettingsClick = () => {
    console.log('Settings clicked');
  };

  const handleProfileClick = () => {
    console.log('Profile clicked');
  };

  return (
    <div className="admin-topbar">
      <div className="topbar-left">
        <div className="search-container">
          <span className="search-icon">🔍</span>
          <input
            type="text"
            className="search-input"
            placeholder="Tìm kiếm người dùng, câu hỏi, giao dịch..."
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
          <span className="icon">🔔</span>
        </button>

        <button
          className="topbar-icon-btn settings-btn"
          onClick={handleSettingsClick}
          aria-label="Settings"
        >
          <span className="icon">⚙️</span>
        </button>

        <div className="profile-area">
          <button
            className="profile-btn"
            onClick={handleProfileClick}
            aria-label="Profile menu"
          >
            <div className="profile-info">
              <span className="profile-name">Admin User</span>
            </div>
            <div className="profile-avatar">
              <span className="avatar-placeholder">A</span>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}

export default AdminTopbar;
