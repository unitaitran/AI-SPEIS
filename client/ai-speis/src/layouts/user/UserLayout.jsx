import React, { useState, useEffect } from 'react';
import UserSidebar from '../../components/user/UserSidebar/UserSidebar';
import UserTopbar from '../../components/user/UserTopbar/UserTopbar';
import ProfileModal from '../../components/user/ProfileModal/ProfileModal';

function UserLayout({ children, collapseSidebar = false, immersive = false }) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const [user, setUser] = useState(null);

  useEffect(() => {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        setUser(JSON.parse(userStr));
      } catch (e) {
        console.error('Failed to parse user', e);
      }
    }
  }, []);

  const closeSidebar = () => setIsSidebarOpen(false);

  const handleUserUpdated = (updatedUser) => {
    setUser(updatedUser);
  };

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
      <UserSidebar
        isOpen={isSidebarOpen}
        collapsed={collapseSidebar}
        onNavigate={closeSidebar}
      />

      {/* Main Content Area */}
      <div className={`flex-1 flex flex-col min-w-0 transition-all duration-300 ${collapseSidebar ? 'lg:ml-[72px]' : 'lg:ml-[240px]'}`}>
        <UserTopbar
          onMenuClick={() => setIsSidebarOpen(true)}
          onOpenProfile={() => setIsProfileOpen(true)}
          user={user}
        />

        {/* Scrollable Content */}
        <main className={`flex-1 min-h-0 ${immersive ? 'overflow-hidden' : 'overflow-y-auto'}`}>
          <div className={immersive
            ? 'h-full min-h-0 w-full'
            : 'max-w-[1200px] mx-auto p-4 md:p-6 lg:p-8 w-full'}>
            {children}
          </div>
        </main>
      </div>

      {isProfileOpen && (
        <ProfileModal
          onClose={() => setIsProfileOpen(false)}
          onUserUpdated={handleUserUpdated}
        />
      )}
    </div>
  );
}

export default UserLayout;
