import React from 'react';
import * as LucideIcons from 'lucide-react';
import './AdminMenuItem.css';

function AdminMenuItem({ item, isActive, onClick }) {
  const IconComponent = LucideIcons[item.icon] || LucideIcons.Circle;

  return (
    <button
      type="button"
      className={`admin-menu-item ${isActive ? 'active' : ''}`}
      onClick={onClick}
      aria-current={isActive ? 'page' : undefined}
    >
      <div className="menu-icon-wrapper">
        <IconComponent size={20} className="menu-icon" />
      </div>
      <span className="menu-label">{item.label}</span>
      {item.hasBadge && <span className="menu-badge" aria-label="Requires attention" />}
    </button>
  );
}

export default AdminMenuItem;
