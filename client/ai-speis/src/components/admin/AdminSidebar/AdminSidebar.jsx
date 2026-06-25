import React from 'react';
import { LogOut } from 'lucide-react';
import AdminMenuItem from '../AdminMenuItem/AdminMenuItem';
import { ADMIN_MENU_ITEMS } from '../../../constants/adminMenu';
import { navigate } from '../../../routes/navigation';

function AdminSidebar({ isOpen, pathname, onNavigate }) {
  const handleMenuClick = (event, path) => {
    event.preventDefault();
    navigate(path);
    onNavigate?.();
  };

  const handleLogout = () => {
    // TODO: Implement logout logic
    localStorage.removeItem('token');
    navigate('/login');
    console.log('Logout clicked');
  };

  return (
    <aside
      className={`fixed left-0 top-0 z-[120] flex h-screen w-[240px] flex-col overflow-y-auto overflow-x-hidden border-r border-border bg-surface-3 transition-transform duration-300 md:translate-x-0 ${
        isOpen ? 'translate-x-0 shadow-[0_4px_12px_rgba(31,45,61,0.08)]' : '-translate-x-full'
      }`}
      aria-label="Admin navigation"
    >
      <div className="flex h-[85px] shrink-0 items-center justify-center border-b border-border px-6">
        <img
          src="/logo_AI-SPEIS-removebg.png"
          alt="AI-SPEIS"
          className="h-[5.4rem] max-w-full object-contain"
        />
      </div>

      <nav className="flex-1 overflow-y-auto px-3 py-4 pb-6">
        <p className="mx-3 mb-2 text-xs font-semibold uppercase leading-[1.4] tracking-[0.08em] text-text-secondary">
          Management
        </p>
        {ADMIN_MENU_ITEMS.map((item) => (
          <AdminMenuItem
            key={item.id}
            item={item}
            isActive={pathname === item.path}
            onClick={(event) => handleMenuClick(event, item.path)}
          />
        ))}
      </nav>

      <div className="shrink-0 border-t border-border p-4">
        <button
          className="flex min-h-11 w-full items-center gap-2 rounded-lg border border-border bg-white/60 px-3 py-2.5 text-sm font-medium text-text-secondary transition-all duration-200 hover:border-error/30 hover:bg-error-light hover:text-error focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
          onClick={handleLogout}
        >
          <LogOut size={20} className="shrink-0" />
          <span>Logout</span>
        </button>
      </div>
    </aside>
  );
}

export default AdminSidebar;
