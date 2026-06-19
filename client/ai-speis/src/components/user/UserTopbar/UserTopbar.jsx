import React, { useEffect, useState, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Menu, Bell, ChevronDown, Ticket, User, LogOut, Settings, Globe } from 'lucide-react';
import { navigate } from '../../../routes/navigation';
import { USER_ROUTES } from '../../../routes/routePaths';

function UserTopbar({ onMenuClick, onOpenProfile, user: propUser }) {
  const { t, i18n } = useTranslation('dashboard');
  const [user, setUser] = useState(null);

  useEffect(() => {
    if (propUser) {
      setUser(propUser);
    } else {
      const userStr = localStorage.getItem('user');
      if (userStr) {
        try {
          setUser(JSON.parse(userStr));
        } catch (e) {
          console.error('Failed to parse user', e);
        }
      }
    }
  }, [propUser]);

  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    if (!isDropdownOpen) return;

    const handleScroll = () => {
      setIsDropdownOpen(false);
    };

    window.addEventListener('scroll', handleScroll, { capture: true, passive: true });
    return () => {
      window.removeEventListener('scroll', handleScroll, { capture: true });
    };
  }, [isDropdownOpen]);

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/#login';
  };

  const toggleLanguage = () => {
    const nextLang = i18n.language.startsWith('vi') ? 'en' : 'vi';
    i18n.changeLanguage(nextLang);
  };

  return (
    <header className="h-[85px] bg-surface-2 border-b border-border flex items-center justify-between px-6 shrink-0 z-10 sticky top-0">
      <div className="flex items-center lg:hidden">
        <button
          className="p-2 -ml-2 text-text-secondary hover:text-text-primary rounded-md hover:bg-surface-3 transition-colors"
          onClick={onMenuClick}
          aria-label="Open menu"
        >
          <Menu size={24} />
        </button>
        {/* Mobile Logo */}
        <img src="/logo_AI-SPEIS-removebg.png" alt="AI-SPEIS" className="ml-2 h-7 object-contain" />
      </div>

      {/* Spacer for desktop since logo is in sidebar */}
      <div className="hidden lg:block flex-1"></div>

      <div className="flex items-center space-x-4 ml-auto">
        {/* Quota Badge */}
        <div className="hidden sm:flex items-center bg-gradient-to-r from-primary-light to-primary-xlight border border-primary-light rounded-full px-3 py-1.5 text-sm font-semibold text-primary-dark shadow-sm">
          <Ticket size={16} className="text-primary-dark mr-2" />
          <span>5 {t('topbar.quota_remaining', 'lượt phỏng vấn')}</span>
        </div>

        {/* Notification */}
        <button className="relative p-2 text-text-secondary hover:text-primary-dark hover:bg-primary-xlight rounded-full transition-colors" aria-label="Notifications">
          <Bell size={20} />
          {/* Notification Badge */}
          <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-error rounded-full border border-surface-2 shadow-sm"></span>
        </button>

        <div className="w-px h-6 bg-border mx-1"></div>

        {/* Profile Dropdown */}
        <div className="relative" ref={dropdownRef}>
          <button
            className="flex items-center space-x-2 p-1 pl-2 pr-3 hover:bg-surface-3 rounded-full transition-colors border border-transparent hover:border-border group"
            onClick={() => setIsDropdownOpen(!isDropdownOpen)}
          >
            <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary to-primary-dark flex items-center justify-center text-white shadow-sm overflow-hidden">
              {user && user.avatar ? (
                <img src={user.avatar} alt="Avatar" className="w-full h-full object-cover" />
              ) : (
                <User size={16} />
              )}
            </div>
            <span className="text-sm font-bold text-text-primary hidden sm:block group-hover:text-primary-dark transition-colors">
              {user ? user.fullName : 'User Name'}
            </span>
            <ChevronDown size={16} className={`text-text-secondary group-hover:text-primary-dark transition-transform ${isDropdownOpen ? 'rotate-180' : ''}`} />
          </button>

          {/* Dropdown Menu */}
          {isDropdownOpen && (
            <div className="absolute right-0 mt-2 w-48 bg-surface-1 border border-border rounded-xl shadow-lg py-2 z-50 animate-in fade-in slide-in-from-top-2">
              <div className="px-4 py-2 border-b border-border mb-2">
                <p className="text-sm font-semibold text-text-primary line-clamp-1">{user ? user.fullName : 'User Name'}</p>
                <p className="text-xs text-text-secondary line-clamp-1">{user ? user.email : ''}</p>
              </div>
              <button
                className="w-full flex items-center px-4 py-2 text-sm text-text-secondary hover:text-primary-dark hover:bg-primary-xlight transition-colors cursor-pointer"
                onClick={() => {
                  setIsDropdownOpen(false);
                  if (onOpenProfile) {
                    onOpenProfile();
                  } else {
                    navigate(USER_ROUTES.PROFILE);
                  }
                }}
              >
                <Settings size={16} className="mr-3" />
                {t('topbar.profile_info', 'Thông tin cá nhân')}
              </button>

              {/* Language Switcher Button */}
              <button
                className="w-full flex items-center px-4 py-2 text-sm text-text-secondary hover:text-primary-dark hover:bg-primary-xlight transition-colors cursor-pointer mt-1"
                onClick={toggleLanguage}
              >
                <Globe size={16} className="mr-3" />
                {i18n.language.startsWith('vi') ? 'English (EN)' : 'Tiếng Việt (VI)'}
              </button>

              <button
                className="w-full flex items-center px-4 py-2 text-sm text-error hover:bg-error/10 transition-colors mt-1 cursor-pointer"
                onClick={handleLogout}
              >
                <LogOut size={16} className="mr-3" />
                {t('topbar.logout', 'Đăng xuất')}
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}

export default UserTopbar;
