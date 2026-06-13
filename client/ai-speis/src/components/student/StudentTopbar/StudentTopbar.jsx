import React from 'react';
import { Bell, ChevronDown, Menu, UserRound } from 'lucide-react';
import { navigate } from '../../../routes/navigation';
import './StudentTopbar.css';

function StudentTopbar({ onMenuClick }) {
  return (
    <header className="student-topbar">
      <button
        type="button"
        className="student-mobile-menu"
        aria-label="Mở thanh điều hướng"
        onClick={onMenuClick}
      >
        <Menu size={22} />
      </button>

      <div className="student-topbar-actions">
        <button className="quota-chip" type="button" onClick={() => navigate('/subscription')}>
          <strong>5</strong>
          <span>lượt phỏng vấn</span>
        </button>

        <button className="student-icon-button" type="button" aria-label="Thông báo">
          <Bell size={20} />
          <span className="notification-dot" />
        </button>

        <button
          className="student-profile-menu"
          type="button"
          onClick={() => navigate('/profile')}
          aria-label="Mở hồ sơ cá nhân"
        >
          <span className="student-profile-avatar"><UserRound size={19} /></span>
          <span className="student-profile-copy">
            <strong>Nguyễn Minh Anh</strong>
            <small>Sinh viên</small>
          </span>
          <ChevronDown size={16} />
        </button>
      </div>
    </header>
  );
}

export default StudentTopbar;
