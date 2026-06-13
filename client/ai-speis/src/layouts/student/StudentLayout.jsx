import React, { useState } from 'react';
import StudentSidebar from '../../components/student/StudentSidebar/StudentSidebar';
import StudentTopbar from '../../components/student/StudentTopbar/StudentTopbar';
import './StudentLayout.css';

function StudentLayout({ children, pathname }) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  return (
    <div className="student-layout">
      <StudentSidebar
        isOpen={isSidebarOpen}
        pathname={pathname}
        onNavigate={() => setIsSidebarOpen(false)}
      />
      <button
        type="button"
        className={`student-sidebar-backdrop ${isSidebarOpen ? 'is-visible' : ''}`}
        aria-label="Đóng thanh điều hướng"
        onClick={() => setIsSidebarOpen(false)}
      />

      <div className="student-main">
        <StudentTopbar onMenuClick={() => setIsSidebarOpen(true)} />
        <main className="student-content">
          <div className="student-content-inner">{children}</div>
        </main>
      </div>
    </div>
  );
}

export default StudentLayout;
