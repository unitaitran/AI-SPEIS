import React from 'react';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, FileText, Clock, Layers, Users, Package, Lock, Database } from 'lucide-react';
import { navigate, NAVIGATION_EVENT } from '../../../routes/navigation';
import { USER_ROUTES } from '../../../routes/routePaths';

const MENU_GROUPS = [
  {
    label: 'CHÍNH',
    items: [
      { id: 'dashboard', label: 'Trang chủ', icon: LayoutDashboard, path: USER_ROUTES.DASHBOARD },
      { id: 'cv', label: 'CV của tôi', icon: FileText, path: USER_ROUTES.CV },
    ]
  },
  {
    label: 'LUYỆN TẬP',
    items: [
      { id: 'questions', label: 'Câu hỏi', icon: Database, path: USER_ROUTES.QUESTIONS },
      { id: 'history', label: 'Lịch sử phỏng vấn', icon: Clock, path: '#history' },
      { id: 'flashcards', label: 'Flashcards', icon: Layers, path: '#flashcards' },
    ]
  },
  {
    label: 'CỘNG ĐỒNG',
    items: [
      { id: 'community', label: 'Cộng đồng', icon: Users, path: '#community' },
    ]
  },
  {
    label: 'QUẢN LÍ',
    items: [
      { id: 'packages', label: 'Quản lí gói', icon: Package, path: '#packages' },
    ]
  }
];

function UserSidebar({ isOpen, onNavigate }) {
  const { t } = useTranslation('dashboard');
  const [currentPathname, setCurrentPathname] = React.useState(window.location.pathname);

  React.useEffect(() => {
    const syncPathname = () => setCurrentPathname(window.location.pathname);
    window.addEventListener('popstate', syncPathname);
    window.addEventListener(NAVIGATION_EVENT, syncPathname);
    return () => {
      window.removeEventListener('popstate', syncPathname);
      window.removeEventListener(NAVIGATION_EVENT, syncPathname);
    };
  }, []);

  const handleMenuClick = (event, path) => {
    event.preventDefault();
    if (
      path !== USER_ROUTES.DASHBOARD &&
      path !== USER_ROUTES.CV &&
      path !== USER_ROUTES.QUESTIONS
    ) {
      alert(t('common_feature_developing', 'Tính năng đang phát triển'));
      return;
    }
    navigate(path);
    if (onNavigate) onNavigate();
  };

  const getGroupLabel = (group) => {
    switch (group.label) {
      case 'CHÍNH': return t('sidebar.group_main', 'CHÍNH');
      case 'LUYỆN TẬP': return t('sidebar.group_practice', 'LUYỆN TẬP');
      case 'CỘNG ĐỒNG': return t('sidebar.group_community', 'CỘNG ĐỒNG');
      case 'QUẢN LÍ': return t('sidebar.group_management', 'QUẢN LÍ');
      default: return group.label;
    }
  };

  const getItemLabel = (item) => {
    switch (item.id) {
      case 'dashboard': return t('sidebar.menu_dashboard', 'Trang chủ');
      case 'cv': return t('sidebar.menu_cv', 'CV của tôi');
      case 'questions': return t('sidebar.menu_questions', 'Câu hỏi');
      case 'history': return t('sidebar.menu_history', 'Lịch sử phỏng vấn');
      case 'flashcards': return t('sidebar.menu_flashcards', 'Flashcards');
      case 'community': return t('sidebar.menu_community', 'Cộng đồng');
      case 'packages': return t('sidebar.menu_packages', 'Quản lí gói');
      default: return item.label;
    }
  };

  return (
    <aside className={`fixed top-0 left-0 h-full w-[240px] bg-surface-2 border-r border-border flex flex-col z-20 transition-transform duration-300 ${isOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}`} aria-label="User navigation">
      {/* Logo Area */}
      <div className="h-[85px] flex items-center px-6 border-b border-border shrink-0 justify-center">
        <img
          src="/logo_AI-SPEIS-removebg.png"
          alt="AI-SPEIS"
          style={{ height: '5.4rem' }}
          className="max-w-full object-contain"
        />
      </div>

      {/* Navigation Menu */}
      <nav className="flex-1 overflow-y-auto py-6 px-4 flex flex-col">
        <div className="space-y-6">
          {MENU_GROUPS.map((group, idx) => (
            <div key={idx}>
              <p className="text-xs font-semibold text-text-disabled mb-2 px-2 uppercase tracking-wider">{getGroupLabel(group)}</p>
              <ul className="space-y-1">
                {group.items.map((item) => {
                  const isActive = currentPathname === item.path;
                  return (
                    <li key={item.id}>
                      <a
                        href={item.path}
                        onClick={(e) => handleMenuClick(e, item.path)}
                        className={`flex items-center px-3 py-2.5 rounded-md text-sm transition-all duration-300 relative group overflow-hidden ${isActive
                          ? 'text-primary-dark font-bold shadow-sm'
                          : 'text-text-secondary hover:text-primary-dark'
                          }`}
                      >
                        {/* Smooth active background fade */}
                        <div className={`absolute inset-0 bg-gradient-to-r from-primary-light to-primary-xlight transition-opacity duration-300 ${isActive ? 'opacity-100' : 'opacity-0 group-hover:opacity-30'}`} />

                        {/* Slide vertical indicator */}
                        <div className={`absolute left-0 top-0 bottom-0 w-1 bg-primary rounded-r-md transition-all duration-300 origin-left ${isActive ? 'opacity-100 scale-x-100' : 'opacity-0 scale-x-0'}`} />

                        <item.icon
                          size={20}
                          className={`mr-3 relative z-10 transition-colors duration-300 ${isActive ? 'text-primary-dark' : 'text-text-secondary group-hover:text-primary-dark'}`}
                        />
                        <span className="relative z-10 transition-transform duration-300 group-hover:translate-x-0.5">
                          {getItemLabel(item)}
                        </span>
                      </a>
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </div>

        {/* Pro Upgrade Banner */}
        <div className="mt-8">
          <div className="bg-surface-1 border border-border rounded-xl p-4 text-center">
            <div className="flex justify-center mb-2">
              <Lock size={20} className="text-primary-dark" />
            </div>
            <h4 className="text-sm font-semibold text-text-primary mb-1">{t('sidebar.upgrade_pro', 'Nâng cấp Pro')}</h4>
            <p className="text-xs text-text-secondary mb-3">{t('sidebar.unlock_desc', 'Mở khóa không giới hạn lượt phỏng vấn AI.')}</p>
            <button className="w-full bg-text-primary hover:bg-black text-white text-xs font-semibold py-2 px-4 rounded transition-colors cursor-pointer">
              {t('sidebar.upgrade_now', 'NÂNG CẤP NGAY')}
            </button>
          </div>
        </div>
      </nav>

      {/* Start Interview Button */}
      <div className="p-4 shrink-0 bg-surface-2 border-t border-border">
        <button
          className="w-full bg-gradient-to-br from-primary to-[#4A90E2] hover:opacity-90 text-white text-sm font-semibold py-3 px-4 rounded transition-all shadow-sm cursor-pointer"
          onClick={() => {
            navigate(USER_ROUTES.DEVICE_CHECK);
            if (onNavigate) onNavigate();
          }}
        >
          {t('sidebar.start_interview', 'Bắt đầu phỏng vấn')}
        </button>
      </div>
    </aside>
  );
}

export default UserSidebar;
