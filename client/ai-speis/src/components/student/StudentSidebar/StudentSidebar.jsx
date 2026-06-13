import React from 'react';
import { Sparkles } from 'lucide-react';
import { STUDENT_MENU_SECTIONS } from '../../../constants/studentMenu';
import { navigate } from '../../../routes/navigation';
import './StudentSidebar.css';

function StudentSidebar({ isOpen, pathname, onNavigate }) {
  const handleNavigate = (event, path) => {
    event.preventDefault();
    navigate(path);
    onNavigate?.();
  };

  return (
    <aside
      className={`student-sidebar ${isOpen ? 'is-open' : ''}`}
      aria-label="Điều hướng sinh viên"
    >
      <div className="student-brand">
        <img src="/logo_AI-SPEIS-removebg.png" alt="AI-SPEIS" />
        <span>Student Portal</span>
      </div>

      <nav className="student-nav">
        {STUDENT_MENU_SECTIONS.map((section) => (
          <div className="student-nav-section" key={section.label}>
            <p>{section.label}</p>
            {section.items.map((item) => {
              const Icon = item.icon;
              const isActive = pathname === item.path;

              return (
                <a
                  key={item.id}
                  href={item.path}
                  className={`student-nav-link ${isActive ? 'is-active' : ''}`}
                  onClick={(event) => handleNavigate(event, item.path)}
                >
                  <Icon size={20} aria-hidden="true" />
                  <span>{item.label}</span>
                </a>
              );
            })}
          </div>
        ))}
      </nav>

      <div className="student-sidebar-footer">
        <div className="upgrade-card">
          <span className="upgrade-card-icon"><Sparkles size={16} /></span>
          <strong>Nâng cấp Pro</strong>
          <p>Mở khóa không giới hạn lượt phỏng vấn AI.</p>
          <button type="button" onClick={() => navigate('/subscription')}>
            Nâng cấp ngay
          </button>
        </div>

        <button
          className="start-interview-button"
          type="button"
          onClick={() => navigate('/interview/setup')}
        >
          <Sparkles size={18} />
          Bắt đầu phỏng vấn
        </button>
      </div>
    </aside>
  );
}

export default StudentSidebar;
