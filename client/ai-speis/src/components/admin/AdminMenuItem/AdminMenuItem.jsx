import React from 'react';
import './AdminMenuItem.css';

const ICON_MAP = {
  'chart-bar': '📊',
  users: '👥',
  'shield-alt': '🛡️',
  'file-alt': '📄',
  box: '📦',
  'credit-card': '💳',
  gift: '🎁',
  'users-crown': '👑',
  'chart-line': '📈',
  robot: '🤖',
  'dollar-sign': '$',
  undo: '↩️',
};

function AdminMenuItem({ item, isActive, onClick }) {
  const icon = ICON_MAP[item.icon] || '•';

  return (
    <div
      className={`admin-menu-item ${isActive ? 'active' : ''}`}
      onClick={onClick}
    >
      <span className="menu-icon">{icon}</span>
      <span className="menu-label">{item.label}</span>
    </div>
  );
}

export default AdminMenuItem;
