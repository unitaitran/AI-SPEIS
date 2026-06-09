import React from 'react';
import './AdminDashboardPage.css';

function AdminDashboardPage() {
  return (
    <div className="admin-dashboard-page">
      <div className="page-header">
        <div className="breadcrumb">
          <span>Admin</span>
          <span className="separator">/</span>
          <span>Module</span>
          <span className="separator">/</span>
          <span>Màn hiển tại</span>
        </div>

        <div className="header-top">
          <div className="title-section">
            <h1 className="page-title">Tiêu đề màn hình</h1>
            <p className="page-description">Mô tả ngắn về chức năng quản trị.</p>
          </div>

          <button className="btn-primary">Hành động chính</button>
        </div>
      </div>

      <div className="page-content">
        <div className="content-box">
          <div className="placeholder-bar"></div>
          <div className="placeholder-bar short"></div>
          <div className="placeholder-bar shorter"></div>
        </div>

        <div className="content-box large">
          <div className="placeholder-text">
            <p>Nội dung chính sẽ hiển thị tại đây</p>
          </div>
        </div>

        <div className="content-box">
          <div className="placeholder-bar"></div>
        </div>
      </div>
    </div>
  );
}

export default AdminDashboardPage;
