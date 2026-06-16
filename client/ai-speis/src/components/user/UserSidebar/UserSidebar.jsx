import React from 'react';
import { LayoutDashboard, FileText, Clock, Layers, Users, Package, Lock, Database } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
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
      { id: 'questions', label: 'Câu hỏi', icon: Database, path: '#questions' },
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
    if (path !== USER_ROUTES.DASHBOARD && path !== USER_ROUTES.CV) {
      alert('Tính năng đang phát triển');
      return;
    }
    navigate(path);
    if (onNavigate) onNavigate();
  };

  return (
    <aside className={`fixed top-0 left-0 h-full w-[240px] bg-surface-2 border-r border-border flex flex-col z-20 transition-transform duration-300 ${isOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}`} aria-label="User navigation">
      {/* Logo Area */}
      <div className="h-16 flex items-center px-6 border-b border-border shrink-0 justify-center">
        <img src="/logo_AI-SPEIS-removebg.png" alt="AI-SPEIS" className="h-10 object-contain" />
      </div>

      {/* Navigation Menu */}
      <nav className="flex-1 overflow-y-auto py-6 px-4 flex flex-col">
        <div className="space-y-6">
          {MENU_GROUPS.map((group, idx) => (
            <div key={idx}>
              <p className="text-xs font-semibold text-text-disabled mb-2 px-2 uppercase tracking-wider">{group.label}</p>
              <ul className="space-y-1">
                {group.items.map((item) => {
                  const isActive = currentPathname === item.path;
                  return (
                    <li key={item.id}>
                      <a
                        href={item.path}
                        onClick={(e) => handleMenuClick(e, item.path)}
                        className={`flex items-center px-3 py-2.5 rounded-md text-sm transition-all relative ${isActive
                          ? 'bg-gradient-to-r from-primary-light to-primary-xlight text-primary-dark font-bold shadow-sm'
                          : 'text-text-secondary hover:bg-surface-3 hover:text-primary-dark'
                          }`}
                      >
                        {isActive && (
                          <div className="absolute left-0 top-0 bottom-0 w-1 bg-primary rounded-r-md"></div>
                        )}
                        <item.icon size={20} className={`mr-3 ${isActive ? 'text-primary-dark' : 'text-text-secondary'}`} />
                        {item.label}
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
            <h4 className="text-sm font-semibold text-text-primary mb-1">Nâng cấp Pro</h4>
            <p className="text-xs text-text-secondary mb-3">Mở khóa không giới hạn lượt phỏng vấn AI.</p>
            <button className="w-full bg-text-primary hover:bg-black text-white text-xs font-semibold py-2 px-4 rounded transition-colors">
              NÂNG CẤP NGAY
            </button>
          </div>
        </div>
      </nav>

      {/* Start Interview Button */}
      <div className="p-4 shrink-0 bg-surface-2 border-t border-border">
        <button className="w-full bg-gradient-to-br from-primary to-[#4A90E2] hover:opacity-90 text-white text-sm font-semibold py-3 px-4 rounded transition-all shadow-sm">
          Bắt đầu phỏng vấn
        </button>
      </div>
    </aside>
  );
}

export default UserSidebar;
