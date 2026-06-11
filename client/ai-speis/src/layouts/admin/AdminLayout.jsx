import React, { useState } from 'react';
import AdminSidebar from '../../components/admin/AdminSidebar/AdminSidebar';
import AdminTopbar from '../../components/admin/AdminTopbar/AdminTopbar';
import './AdminLayout.css';

function AdminLayout({ children, pathname }) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  const closeSidebar = () => setIsSidebarOpen(false);

  return (
    <div className="admin-layout">
      <AdminSidebar
        isOpen={isSidebarOpen}
        pathname={pathname}
        onNavigate={closeSidebar}
      />
      <button
        className={`sidebar-backdrop ${isSidebarOpen ? 'is-visible' : ''}`}
        type="button"
        aria-label="Close navigation"
        onClick={closeSidebar}
      />
      <div className="admin-main-container">
        <AdminTopbar onMenuClick={() => setIsSidebarOpen(true)} />
        <main className="admin-content-wrapper">
          <div className="admin-content-inner">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}

export default AdminLayout;
