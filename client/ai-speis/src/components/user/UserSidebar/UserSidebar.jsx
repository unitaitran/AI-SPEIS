import React from 'react';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, FileText, Clock, Package, Lock, Database, Mic } from 'lucide-react';
import { navigate, NAVIGATION_EVENT } from '../../../routes/navigation';
import { USER_ROUTES } from '../../../routes/routePaths';
import notify from '../../../utils/notification';
import { beginNewInterviewCampaign } from '../../../utils/interviewContext';
import { API_BASE_URL } from '../../../config/api';

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
    ]
  },
  {
    label: 'QUẢN LÍ',
    items: [
      { id: 'packages', label: 'Quản lí gói', icon: Package, path: USER_ROUTES.PACKAGES },
    ]
  }
];

function UserSidebar({ isOpen, compact = false, onNavigate, onBeforeNavigate }) {
  const { t } = useTranslation('dashboard');
  const [currentPathname, setCurrentPathname] = React.useState(window.location.pathname);
  const [isPremium, setIsPremium] = React.useState(() => {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        const u = JSON.parse(userStr);
        return Boolean(u?.isPremium || u?.IsPremium);
      } catch (e) {}
    }
    return false;
  });

  React.useEffect(() => {
    const syncPathname = () => setCurrentPathname(window.location.pathname);
    window.addEventListener('popstate', syncPathname);
    window.addEventListener(NAVIGATION_EVENT, syncPathname);
    return () => {
      window.removeEventListener('popstate', syncPathname);
      window.removeEventListener(NAVIGATION_EVENT, syncPathname);
    };
  }, []);

  React.useEffect(() => {
    const updateLocalUserIsPremium = (isPrem) => {
      const userStr = localStorage.getItem('user');
      if (userStr) {
        try {
          const u = JSON.parse(userStr);
          if (u.isPremium !== isPrem) {
            u.isPremium = isPrem;
            localStorage.setItem('user', JSON.stringify(u));
          }
        } catch (e) {}
      }
    };

    const checkQuota = async () => {
      try {
        const token = localStorage.getItem('token');
        if (!token) return;
        const res = await fetch(`${API_BASE_URL}/api/InterviewSession/quota`, {
          headers: { Authorization: `Bearer ${token}` }
        });
        if (res.ok) {
          const data = await res.json();
          const isPrem = data && data.planName === 'Premium';
          setIsPremium(isPrem);
          updateLocalUserIsPremium(isPrem);
        }
      } catch {
        // Ignore fetch errors
      }
    };

    const handleQuotaChanged = (event) => {
      const nextPlanName = event.detail?.planName;
      if (typeof nextPlanName === 'string') {
        const isPrem = nextPlanName === 'Premium';
        setIsPremium(isPrem);
        updateLocalUserIsPremium(isPrem);
      } else {
        checkQuota();
      }
    };

    checkQuota();
    window.addEventListener('interview:quota-changed', handleQuotaChanged);
    return () => {
      window.removeEventListener('interview:quota-changed', handleQuotaChanged);
    };
  }, []);

  const handleMenuClick = (event, path) => {
    event.preventDefault();
    if (
      path !== USER_ROUTES.DASHBOARD &&
      path !== USER_ROUTES.CV &&
      path !== USER_ROUTES.QUESTIONS &&
      path !== USER_ROUTES.PACKAGES
    ) {
      notify.info(t('common_feature_developing', 'Tính năng đang phát triển'), {
        title: t('common_information', 'Thông tin'),
      });
      return;
    }
    if (onBeforeNavigate?.(path) === false) return;
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
    <aside className={`fixed top-0 left-0 h-full w-[240px] ${compact ? 'lg:w-[72px]' : 'lg:w-[240px]'} bg-surface-2 border-r border-border flex flex-col z-20 transition-all duration-300 ${isOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}`} aria-label="User navigation">
      {/* Logo Area */}
      <div className={`h-[85px] flex items-center border-b border-border shrink-0 justify-center ${compact ? 'lg:px-2 px-6' : 'px-6'}`}>
        <img
          src="/logo_AI-SPEIS-removebg.png"
          alt="AI-SPEIS"
          style={{ height: '5.4rem' }}
          className={`max-w-full object-contain ${compact ? 'lg:h-14 lg:w-14' : ''}`}
        />
      </div>

      {/* Navigation Menu */}
      <nav className={`flex-1 overflow-y-auto py-6 flex flex-col ${compact ? 'lg:px-2 px-4' : 'px-4'}`}>
        <div className="space-y-6">
          {MENU_GROUPS.map((group, idx) => (
            <div key={idx}>
              <p className={`text-xs font-semibold text-text-disabled mb-2 px-2 uppercase tracking-wider ${compact ? 'lg:hidden' : ''}`}>{getGroupLabel(group)}</p>
              <ul className="space-y-1">
                {group.items.map((item) => {
                  const isActive = currentPathname === item.path;
                  return (
                    <li key={item.id}>
                      <a
                        href={item.path}
                        onClick={(e) => handleMenuClick(e, item.path)}
                        title={compact ? getItemLabel(item) : undefined}
                        className={`flex items-center px-3 py-2.5 rounded-md text-sm transition-all duration-300 relative group overflow-hidden ${compact ? 'lg:justify-center lg:min-h-11' : ''} ${isActive
                          ? 'text-primary-dark font-bold shadow-sm'
                          : 'text-text-secondary hover:text-primary-dark'
                          }`}
                        aria-label={compact ? getItemLabel(item) : undefined}
                      >
                        {/* Smooth active background fade */}
                        <div className={`absolute inset-0 bg-gradient-to-r from-primary-light to-primary-xlight transition-opacity duration-300 ${isActive ? 'opacity-100' : 'opacity-0 group-hover:opacity-30'}`} />

                        {/* Slide vertical indicator */}
                        <div className={`absolute left-0 top-0 bottom-0 w-1 bg-primary rounded-r-md transition-all duration-300 origin-left ${isActive ? 'opacity-100 scale-x-100' : 'opacity-0 scale-x-0'}`} />

                        <item.icon
                          size={20}
                          className={`${compact ? 'lg:mr-0 mr-3' : 'mr-3'} relative z-10 transition-colors duration-300 ${isActive ? 'text-primary-dark' : 'text-text-secondary group-hover:text-primary-dark'}`}
                        />
                        <span className={`relative z-10 transition-transform duration-300 group-hover:translate-x-0.5 ${compact ? 'lg:sr-only' : ''}`}>
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
        {!isPremium && (
          <div className={`mt-8 ${compact ? 'lg:hidden' : ''}`}>
            <div className="bg-surface-1 border border-border rounded-xl p-4 text-center">
              <div className="flex justify-center mb-2">
                <Lock size={20} className="text-primary-dark" />
              </div>
              <h4 className="text-sm font-semibold text-text-primary mb-1">{t('sidebar.upgrade_pro', 'Nâng cấp Pro')}</h4>
              <p className="text-xs text-text-secondary mb-3">{t('sidebar.unlock_desc', 'Mở khóa không giới hạn lượt phỏng vấn AI.')}</p>
              <button
                className="w-full bg-text-primary hover:bg-black text-white text-xs font-semibold py-2 px-4 rounded transition-colors cursor-pointer"
                onClick={() => {
                  if (onBeforeNavigate?.(USER_ROUTES.PACKAGES) === false) return;
                  navigate(`${USER_ROUTES.PACKAGES}?purchase=true`);
                  if (onNavigate) onNavigate();
                }}
              >
                {t('sidebar.upgrade_now', 'NÂNG CẤP NGAY')}
              </button>
            </div>
          </div>
        )}
      </nav>

      {/* Start Interview Button */}
      <div className={`shrink-0 bg-surface-2 border-t border-border ${compact ? 'p-2 lg:p-3' : 'p-4'}`}>
        <button
          className={`w-full bg-gradient-to-br from-primary to-[#4A90E2] hover:opacity-90 text-white text-sm font-semibold py-3 rounded transition-all shadow-sm cursor-pointer flex items-center justify-center ${compact ? 'lg:px-0 px-4' : 'px-4'}`}
            onClick={() => {
              if (onBeforeNavigate?.(USER_ROUTES.INTERVIEW_MODE) === false) return;
              beginNewInterviewCampaign();
              navigate(USER_ROUTES.INTERVIEW_MODE);
            if (onNavigate) onNavigate();
          }}
        >
          {compact ? <Mic size={20} className="hidden lg:block" /> : null}
          <span className={compact ? 'lg:sr-only' : ''}>{t('sidebar.start_interview', 'Bắt đầu phỏng vấn')}</span>
        </button>
      </div>
    </aside>
  );
}

export default UserSidebar;
