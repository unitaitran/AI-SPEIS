import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, FileText, Database, Clock, Package } from 'lucide-react';
import { navigate, NAVIGATION_EVENT } from '../../../routes/navigation';
import { USER_ROUTES } from '../../../routes/routePaths';

const BOTTOM_NAV_ITEMS = [
  {
    id: 'dashboard',
    label: 'Trang chủ',
    labelEn: 'Home',
    icon: LayoutDashboard,
    path: USER_ROUTES.DASHBOARD,
    isActive: (pathname) =>
      pathname === USER_ROUTES.DASHBOARD ||
      pathname === USER_ROUTES.ROOT ||
      pathname === `${USER_ROUTES.ROOT}/` ||
      pathname === '/dashboard' ||
      pathname === '/dashboard/',
  },
  {
    id: 'cv',
    label: 'CV/JD',
    labelEn: 'CV/JD',
    icon: FileText,
    path: USER_ROUTES.CV,
    isActive: (pathname) =>
      pathname === USER_ROUTES.CV ||
      pathname.startsWith(`${USER_ROUTES.CV}/`) ||
      pathname === USER_ROUTES.CV_DETAIL ||
      pathname.startsWith(`${USER_ROUTES.CV_DETAIL}/`),
  },
  {
    id: 'questions',
    label: 'Câu hỏi',
    labelEn: 'Questions',
    icon: Database,
    path: USER_ROUTES.QUESTIONS,
    isActive: (pathname) =>
      pathname === USER_ROUTES.QUESTIONS ||
      pathname.startsWith(`${USER_ROUTES.QUESTIONS}/`),
  },
  {
    id: 'history',
    label: 'Lịch sử',
    labelEn: 'History',
    icon: Clock,
    path: USER_ROUTES.INTERVIEW_HISTORY,
    isActive: (pathname) =>
      pathname === USER_ROUTES.INTERVIEW_HISTORY ||
      pathname.startsWith(`${USER_ROUTES.INTERVIEW_HISTORY}/`) ||
      pathname.startsWith('/user/interview/campaign-result'),
  },
  {
    id: 'packages',
    label: 'Service',
    labelEn: 'Service',
    icon: Package,
    path: USER_ROUTES.PACKAGES,
    isActive: (pathname) =>
      pathname === USER_ROUTES.PACKAGES ||
      pathname.startsWith(`${USER_ROUTES.PACKAGES}/`) ||
      pathname.startsWith(USER_ROUTES.PAYMENT_RESULT),
  },
];

function UserBottomNav({ onBeforeNavigate }) {
  const { i18n } = useTranslation();
  const [currentPathname, setCurrentPathname] = useState(window.location.pathname);

  useEffect(() => {
    const syncPathname = () => setCurrentPathname(window.location.pathname);
    window.addEventListener('popstate', syncPathname);
    window.addEventListener(NAVIGATION_EVENT, syncPathname);
    return () => {
      window.removeEventListener('popstate', syncPathname);
      window.removeEventListener(NAVIGATION_EVENT, syncPathname);
    };
  }, []);

  const handleNavClick = (event, item) => {
    event.preventDefault();
    if (onBeforeNavigate?.(item.path) === false) return;
    navigate(item.path);
  };

  const isEn = (i18n?.language || '').toLowerCase().startsWith('en');

  return (
    <nav
      className="fixed bottom-0 left-0 right-0 z-30 lg:hidden bg-surface-2/95 backdrop-blur-md border-t border-border shadow-[0_-4px_20px_rgba(0,0,0,0.06)] pb-[env(safe-area-inset-bottom)]"
      aria-label="Mobile Bottom Navigation"
      data-testid="user-bottom-nav"
    >
      <div className="flex items-center justify-around h-[68px] px-1 max-w-lg mx-auto">
        {BOTTOM_NAV_ITEMS.map((item) => {
          const active = item.isActive(currentPathname);
          const Icon = item.icon;
          const label = isEn ? item.labelEn : item.label;

          return (
            <a
              key={item.id}
              href={item.path}
              onClick={(e) => handleNavClick(e, item)}
              className={`flex-1 flex flex-col items-center justify-center h-full py-1 px-1 relative transition-all duration-200 select-none group active:scale-95 ${
                active ? 'text-primary-dark font-bold' : 'text-text-secondary hover:text-primary-dark'
              }`}
              aria-label={label}
              data-active={String(active)}
            >
              {/* Active top glow indicator line */}
              {active && (
                <div className="absolute top-0 left-1/2 -translate-x-1/2 w-10 h-1 bg-primary rounded-full shadow-[0_2px_8px_rgba(var(--color-primary-rgb),0.55)] transition-all duration-300" />
              )}

              {/* Icon Container with active background pill */}
              <div
                className={`flex items-center justify-center w-12 h-8 rounded-full transition-all duration-300 ${
                  active ? 'bg-primary-xlight text-primary-dark shadow-xs' : 'group-hover:bg-surface-3'
                }`}
              >
                <Icon size={22} className={`transition-transform duration-200 ${active ? 'scale-110 stroke-[2.4]' : 'stroke-[1.8]'}`} />
              </div>

              {/* Short crisp label */}
              <span
                className={`text-[11.5px] sm:text-xs font-medium tracking-tight mt-0.5 whitespace-nowrap text-center transition-colors duration-200 ${
                  active ? 'text-primary-dark font-bold' : 'text-text-secondary'
                }`}
              >
                {label}
              </span>
            </a>
          );
        })}
      </div>
    </nav>
  );
}

export default UserBottomNav;
