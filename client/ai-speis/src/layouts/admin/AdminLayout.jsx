import React, { useState } from 'react';
import AdminSidebar from '../../components/admin/AdminSidebar/AdminSidebar';
import AdminTopbar from '../../components/admin/AdminTopbar/AdminTopbar';

function AdminLayout({ children, pathname }) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  const closeSidebar = () => setIsSidebarOpen(false);

  return (
    <div className="flex min-h-screen w-full bg-surface-1">
      <AdminSidebar
        isOpen={isSidebarOpen}
        pathname={pathname}
        onNavigate={closeSidebar}
      />
      <button
        className={`fixed inset-0 z-[110] bg-text-primary/20 backdrop-blur-sm transition-all duration-500 md:hidden ${
          isSidebarOpen ? 'opacity-100' : 'pointer-events-none opacity-0'
        }`}
        type="button"
        aria-label="Close navigation"
        onClick={closeSidebar}
      />
      <div className="flex min-h-screen min-w-0 flex-1 flex-col md:ml-[240px]">
        <AdminTopbar onMenuClick={() => setIsSidebarOpen(true)} />
        <main className="flex-1 bg-surface-1 p-4 md:p-6 lg:p-8">
          <div className="mx-auto w-full max-w-[1200px]">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}

export default AdminLayout;
