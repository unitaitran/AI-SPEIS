import React, { useState, useEffect } from 'react';
import UserSidebar from '../../components/user/UserSidebar/UserSidebar';
import UserTopbar from '../../components/user/UserTopbar/UserTopbar';
import UserBottomNav from '../../components/user/UserBottomNav/UserBottomNav';
import ProfileModal from '../../components/user/ProfileModal/ProfileModal';

function UserLayout({
  children,
  compactSidebar = false,
  collapseSidebar = false,
  hideSidebar = false,
  immersive = false,
  onBeforeNavigate,
}) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [isProfileOpen, setIsProfileOpen] = useState(false);
  const [user, setUser] = useState(null);
  const sidebarIsCompact = compactSidebar || collapseSidebar;

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
    <div className="flex h-screen w-full max-w-full bg-surface-1 font-sans text-text-primary overflow-hidden">
      {/* Sidebar Overlay for Mobile */}
      {!hideSidebar && isSidebarOpen && (
        <div
          className="fixed inset-0 bg-black/20 z-10 lg:hidden transition-opacity"
          onClick={closeSidebar}
          aria-hidden="true"
        />
      )}

      {/* Sidebar */}
      {!hideSidebar && (
        <UserSidebar
          isOpen={isSidebarOpen}
          compact={sidebarIsCompact}
          collapsed={sidebarIsCompact}
          onNavigate={closeSidebar}
          onBeforeNavigate={onBeforeNavigate}
        />
      )}

      {/* Main Content Area */}
      <div className={`flex-1 flex flex-col min-w-0 w-full max-w-full transition-all duration-300 overflow-x-hidden ${hideSidebar ? 'lg:ml-0' : sidebarIsCompact ? 'lg:ml-[72px]' : 'lg:ml-[240px]'}`}>
        {!immersive && (
          <UserTopbar
            onMenuClick={() => setIsSidebarOpen(true)}
            onOpenProfile={() => setIsProfileOpen(true)}
            user={user}
          />
        )}

        {/* Scrollable Content */}
        <main className={`flex-1 w-full max-w-full min-w-0 ${immersive ? 'overflow-hidden' : 'overflow-y-auto overflow-x-hidden'}`}>
          <div className={immersive ? 'h-full w-full' : 'max-w-[1200px] mx-auto p-4 md:p-6 lg:p-8 w-full min-w-0 pb-24 lg:pb-8'}>
            {children}
          </div>
        </main>
      </div>

      {/* Bottom Navigation for Mobile */}
      {!hideSidebar && !immersive && (
        <UserBottomNav onBeforeNavigate={onBeforeNavigate} />
      )}

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
