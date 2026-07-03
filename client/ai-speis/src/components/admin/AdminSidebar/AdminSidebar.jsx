import React from 'react';
import { LogOut, Globe } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import AdminMenuItem from '../AdminMenuItem/AdminMenuItem';
import { ADMIN_MENU_ITEMS } from '../../../constants/adminMenu';
import { navigate } from '../../../routes/navigation';

function AdminSidebar({ isOpen, pathname, onNavigate }) {
  const { t, i18n } = useTranslation(['admin-dashboard', 'admin-users']);

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
      className={`fixed left-0 top-0 z-[120] flex h-screen w-[240px] flex-col overflow-y-auto overflow-x-hidden border-r border-border/60 bg-gradient-to-b from-surface-3 to-surface-1 transition-all duration-500 ease-[cubic-bezier(0.16,1,0.3,1)] md:translate-x-0 ${
        isOpen ? 'translate-x-0 shadow-[0_16px_48px_rgba(31,45,61,0.14)]' : '-translate-x-full'
      }`}
      aria-label="Admin navigation"
    >
      <div className="flex h-[85px] shrink-0 items-center justify-center border-b border-border/50 px-6">
        <img
          src="/logo_AI-SPEIS-removebg.png"
          alt="AI-SPEIS"
          className="h-[5.4rem] max-w-full object-contain drop-shadow-sm"
        />
      </div>

      <nav className="flex-1 overflow-y-auto px-3 py-4 pb-6">
        <p className="mx-3 mb-3 text-[10px] font-semibold uppercase leading-[1.4] tracking-[0.12em] text-text-secondary/70">
          {t('management', 'Management')}
        </p>
        {ADMIN_MENU_ITEMS.map((item) => (
          <AdminMenuItem
            key={item.id}
            item={{ ...item, label: t(`menu${item.label}`, item.label) }}
            isActive={pathname === item.path}
            onClick={(event) => handleMenuClick(event, item.path)}
          />
        ))}
      </nav>

      <div className="shrink-0 border-t border-border/50 p-4 flex flex-col gap-2">
        <button
          className="flex min-h-11 w-full items-center justify-between gap-2.5 rounded-xl border border-border/60 bg-white/50 px-3 py-2.5 text-sm font-medium text-text-secondary backdrop-blur-sm transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:border-primary/30 hover:bg-primary-light/50 hover:text-primary hover:shadow-sm active:scale-[0.97] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
          onClick={() => i18n.changeLanguage(i18n.language === 'vi' ? 'en' : 'vi')}
        >
          <div className="flex items-center gap-2.5">
            <Globe size={20} className="shrink-0" />
            <span>{i18n.language === 'vi' ? 'Tiếng Việt' : 'English'}</span>
          </div>
          <span className="text-xs font-bold uppercase opacity-60 bg-black/5 px-2 py-1 rounded-md">{i18n.language === 'vi' ? 'VI' : 'EN'}</span>
        </button>
        <button
          className="flex min-h-11 w-full items-center gap-2.5 rounded-xl border border-border/60 bg-white/50 px-3 py-2.5 text-sm font-medium text-text-secondary backdrop-blur-sm transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:border-error/30 hover:bg-error-light hover:text-error hover:shadow-sm active:scale-[0.97] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
          onClick={handleLogout}
        >
          <LogOut size={20} className="shrink-0" />
          <span>{t('logout', 'Logout')}</span>
        </button>
      </div>
    </aside>
  );
}

export default AdminSidebar;
