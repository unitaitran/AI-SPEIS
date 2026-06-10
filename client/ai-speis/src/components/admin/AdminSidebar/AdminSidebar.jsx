import React, { useState } from 'react';
import AdminMenuItem from '../AdminMenuItem/AdminMenuItem';
import { ADMIN_MENU_ITEMS } from '../../../constants/adminMenu';
import './AdminSidebar.css';

function AdminSidebar() {
  const [activeMenu, setActiveMenu] = useState('dashboard');

  const handleMenuClick = (itemId) => {
    setActiveMenu(itemId);
  };

  const handleLogout = () => {
    // TODO: Implement logout logic
    console.log('Logout clicked');
  };

  return (
    <div className="admin-sidebar">
      <div className="sidebar-header">
        <div className="logo">
          <span className="logo-icon">🎓</span>
          <div className="logo-text">
            <div className="logo-title">AI-SPEIS Admin</div>
            <div className="logo-subtitle">Admin Console</div>
          </div>
        </div>
      </div>

      <div className="sidebar-divider"></div>

      <div className="sidebar-menu">
        {ADMIN_MENU_ITEMS.map((item) => (
          <AdminMenuItem
            key={item.id}
            item={item}
            isActive={activeMenu === item.id}
            onClick={() => handleMenuClick(item.id)}
          />
        ))}
      </div>

      <div className="sidebar-footer">
        <button className="logout-btn" onClick={handleLogout}>
          <span className="logout-icon">🚪</span>
          <span>Đăng xuất</span>
        </button>
      </div>
    </div>
  );
}

export default AdminSidebar;
