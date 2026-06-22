import React from 'react';
import { LogOut, GraduationCap } from 'lucide-react';
import AdminMenuItem from '../AdminMenuItem/AdminMenuItem';
import { ADMIN_MENU_ITEMS } from '../../../constants/adminMenu';
import { navigate } from '../../../routes/navigation';
import './AdminSidebar.css';

function AdminSidebar({ isOpen, pathname, onNavigate }) {
  const handleMenuClick = (event, path) => {
    event.preventDefault();
    navigate(path);
    onNavigate?.();
  };

  const handleLogout = () => {
    // TODO: Implement logout logic
    console.log('Logout clicked');
  };

  return (
    <aside className={`admin-sidebar ${isOpen ? 'is-open' : ''}`} aria-label="Admin navigation">
      <div className="sidebar-header">
        <div className="sidebar-logo">
          <img
            src="/logo_AI-SPEIS-removebg.png"
            alt="AI-SPEIS Logo"
            className="admin-logo"
          />
        </div>
      </div>

      <div className="sidebar-divider"></div>

      <nav className="sidebar-menu">
        <p className="sidebar-section-label">Management</p>
        {ADMIN_MENU_ITEMS.map((item) => (
          <AdminMenuItem
            key={item.id}
            item={item}
            isActive={pathname === item.path}
            onClick={(event) => handleMenuClick(event, item.path)}
          />
        ))}
      </nav>

      <div className="sidebar-footer">
        <button className="logout-btn" onClick={handleLogout}>
          <LogOut size={20} className="logout-icon" />
          <span>Logout</span>
        </button>
      </div>
    </aside>
  );
}

export default AdminSidebar;
