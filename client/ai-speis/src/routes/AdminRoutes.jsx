import React, { useEffect } from 'react';
import { ADMIN_MENU_ITEMS, ADMIN_ROUTES } from '../constants/adminMenu';
import AdminLayout from '../layouts/admin/AdminLayout';
import AdminDashboardPage from '../pages/admin/Dashboard/AdminDashboardPage';
import UserManagementPage from '../pages/admin/UserManagement/UserManagementPage';
import { navigate } from './navigation';
import QuestionManagementPage from '../pages/admin/QuestionManagement/QuestionManagementPage';

function AdminRoutePlaceholder({ title }) {
  return (
    <div className="admin-dashboard-page">
      <div className="page-header">
        <div className="breadcrumb">
          <span>Admin</span>
          <span className="separator">/</span>
          <span aria-current="page">{title}</span>
        </div>
        <div className="header-top">
          <div className="title-section">
            <h1 className="page-title">{title}</h1>
            <p className="page-description">This admin page has not been implemented yet.</p>
          </div>
        </div>
      </div>
    </div>
  );
}

function AdminRoutes({ pathname }) {
  const isAdminRoot = pathname === '/admin' || pathname === '/admin/';
  const activePathname = isAdminRoot ? ADMIN_ROUTES.DASHBOARD : pathname;

  useEffect(() => {
    if (isAdminRoot) {
      navigate(ADMIN_ROUTES.DASHBOARD, { replace: true });
    }
  }, [isAdminRoot]);

  const currentMenuItem = ADMIN_MENU_ITEMS.find((item) => item.path === activePathname);
  let content;

  if (activePathname === ADMIN_ROUTES.DASHBOARD) {
    content = <AdminDashboardPage />;
  } else if (activePathname === ADMIN_ROUTES.USERS) {
    content = <UserManagementPage />;  
  }else if (activePathname === ADMIN_ROUTES.QUESTIONS) {
  content = <QuestionManagementPage />;
}
   else {
    content = <AdminRoutePlaceholder title={currentMenuItem?.label || 'Page not found'} />;
  }

  return (
    <AdminLayout pathname={activePathname}>
      {content}
    </AdminLayout>
  );
}

export default AdminRoutes;
