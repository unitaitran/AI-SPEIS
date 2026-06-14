import React, { useState } from 'react';
import UserSidebar from '../../components/user/UserSidebar/UserSidebar';
import UserTopbar from '../../components/user/UserTopbar/UserTopbar';

function UserLayout({ children }) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  const closeSidebar = () => setIsSidebarOpen(false);

  return (
    <div className="flex h-screen bg-surface-1 font-sans text-text-primary overflow-hidden">
      {/* Sidebar Overlay for Mobile */}
      {isSidebarOpen && (
        <div 
          className="fixed inset-0 bg-black/20 z-10 lg:hidden transition-opacity"
          onClick={closeSidebar}
          aria-hidden="true"
        />
      )}

      {/* Sidebar */}
      <UserSidebar isOpen={isSidebarOpen} onNavigate={closeSidebar} />

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0 lg:ml-[240px] transition-all duration-300">
        <UserTopbar onMenuClick={() => setIsSidebarOpen(true)} />
        
        {/* Scrollable Content */}
        <main className="flex-1 overflow-y-auto">
          <div className="max-w-[1200px] mx-auto p-4 md:p-6 lg:p-8 w-full">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}

export default UserLayout;
