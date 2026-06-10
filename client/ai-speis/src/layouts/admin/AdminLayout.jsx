import React from 'react';
import AdminSidebar from '../../components/admin/AdminSidebar/AdminSidebar';
import AdminTopbar from '../../components/admin/AdminTopbar/AdminTopbar';
import './AdminLayout.css';

function AdminLayout({ children }) {
  return (
    <div className="admin-layout">
      <AdminSidebar />
      <div className="admin-main-container">
        <AdminTopbar />
        <div className="admin-content-wrapper">
          {children}
        </div>
      </div>
    </div>
  );
}

export default AdminLayout;
