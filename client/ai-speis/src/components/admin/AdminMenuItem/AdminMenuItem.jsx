import React from 'react';
import * as LucideIcons from 'lucide-react';
import './AdminMenuItem.css';

function AdminMenuItem({ item, isActive, onClick }) {
  const IconComponent = LucideIcons[item.icon] || LucideIcons.Circle;

  return (
    <div
      className={`admin-menu-item ${isActive ? 'active' : ''}`}
      onClick={onClick}
    >
      <div className="menu-icon-wrapper">
        <IconComponent size={20} className="menu-icon" />
      </div>
      <span className="menu-label">{item.label}</span>
      {item.hasBadge && <div className="menu-badge" />}
    </div>
  );
}

export default AdminMenuItem;
