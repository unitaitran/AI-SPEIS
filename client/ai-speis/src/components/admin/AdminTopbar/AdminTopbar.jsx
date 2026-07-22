import { Bell, Globe, Menu, User, LogOut } from 'lucide-react';
import { useTranslation } from 'react-i18next';

function AdminTopbar({ onMenuClick }) {
  const { t, i18n } = useTranslation('admin-dashboard');

  const handleNotificationClick = () => {
    console.log('Notification clicked');
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/#login';
  };

  const toggleLanguage = () => {
    i18n.changeLanguage(i18n.language === 'vi' ? 'en' : 'vi');
  };


  return (
    <div className="sticky top-0 z-[90] flex h-16 shrink-0 items-center justify-between border-b border-border/40 bg-white/70 px-4 backdrop-blur-xl md:px-8">
      <div className="flex max-w-[520px] flex-1 items-center gap-2">
        <button
          className="grid h-10 w-10 flex-none place-items-center rounded-xl border border-border/60 bg-white text-text-primary transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:bg-primary-xlight hover:shadow-sm active:scale-[0.95] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30 md:hidden"
          type="button"
          onClick={onMenuClick}
          aria-label={t('openNavigation', 'Open navigation')}
        >
          <Menu size={22} />
        </button>
      </div>

      <div className="ml-auto flex items-center gap-2">
        <button
          type="button"
          className="inline-flex h-10 items-center justify-center gap-2 rounded-xl border border-border/60 bg-white/60 px-3 text-xs font-semibold text-text-secondary transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:border-primary/30 hover:bg-primary-xlight hover:text-primary-dark hover:shadow-sm focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
          onClick={toggleLanguage}
          aria-label={t('toggleLanguage', 'Toggle language')}
        >
          <Globe size={16} />
          <span>EN | VI</span>
        </button>

        <button
          className="relative flex h-10 w-10 items-center justify-center rounded-xl border border-transparent bg-white/50 text-text-secondary transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:border-border/60 hover:bg-white hover:text-primary-dark hover:shadow-sm active:scale-[0.95] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary/30"
          onClick={handleNotificationClick}
          aria-label={t('notifications', 'Notifications')}
        >
          <Bell size={20} />
          <span className="absolute right-2 top-2 h-2 w-2 animate-pulse rounded-full border-2 border-white bg-error shadow-[0_0_6px_rgba(231,111,111,0.5)]" />
        </button>

        <div className="mx-1.5 hidden h-8 w-px bg-border/40 md:block" />

        <div className="relative flex items-center gap-2">
          <div className="flex items-center gap-2.5 rounded-xl bg-transparent py-1 pl-1 pr-2">
            <div className="flex h-9 w-9 items-center justify-center rounded-full border border-border/60 bg-white text-primary-dark shadow-sm">
              <User size={20} />
            </div>
            <div className="hidden flex-col items-start text-left md:flex">
              <span className="text-sm font-semibold text-text-primary">{t('superAdmin', 'Admin')}</span>
            </div>
          </div>
          
          <div className="mx-1.5 hidden h-8 w-px bg-border/40 md:block" />

          <button
            className="flex h-10 w-10 items-center justify-center rounded-xl border border-transparent bg-white/50 text-text-secondary transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] hover:border-error/30 hover:bg-error-light hover:text-error hover:shadow-sm active:scale-[0.95] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-error/30"
            onClick={handleLogout}
            title={t('logout', 'Logout')}
          >
            <LogOut size={20} />
          </button>
        </div>
      </div>
    </div>
  );
}

export default AdminTopbar;
