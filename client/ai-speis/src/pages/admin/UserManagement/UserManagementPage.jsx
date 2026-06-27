import React, { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Eye,
  UserCog,
  Lock,
  Search,
  AlertCircle,
  Users,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  ChevronUp,
  ChevronDown,
  X,
  Mail,
  Phone,
  Calendar,
  Shield,
  Unlock,
  FileText,
  CreditCard,
  Activity,
} from 'lucide-react';
import { userService } from '../../../services/UserService';
import { getAvatarUrl } from '../../../routes/auth';
import '../../../styles/admin/UserManagementPage.css';

function UserManagementPage() {
  const { t } = useTranslation('admin-users');

  // State management
  const [filters, setFilters] = useState({
    search: '',
    role: 'all',
    status: 'all',
    package: 'all',
  });

  // User Details Modal states
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [detailUser, setDetailUser] = useState(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [detailError, setDetailError] = useState(null);

  const [selectedUsers, setSelectedUsers] = useState(new Set());
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [backendNotImplemented, setBackendNotImplemented] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalUsers, setTotalUsers] = useState(0);
  const [sortBy, setSortBy] = useState('');
  const [sortOrder, setSortOrder] = useState('asc');
  const [confirmModal, setConfirmModal] = useState(null);
  const [roleModal, setRoleModal] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null);

  const totalPages = Math.max(1, Math.ceil(totalUsers / pageSize));
  const startIndex = totalUsers === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endIndex = totalUsers === 0 ? 0 : Math.min(currentPage * pageSize, totalUsers);

  const [debouncedSearch, setDebouncedSearch] = useState(filters.search);

  // Debounce search input to prevent excessive API requests
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, 400);
    return () => clearTimeout(timer);
  }, [filters.search]);

  // Reset to first page whenever filter inputs change
  useEffect(() => {
    setCurrentPage(1);
  }, [filters.role, filters.status, filters.package, debouncedSearch]);

  // Fetch users on mount and when pagination, sorting, or filters change
  useEffect(() => {
    fetchUsers();
  }, [currentPage, pageSize, sortBy, sortOrder, filters.role, filters.status, filters.package, debouncedSearch]);

  useEffect(() => {
    if (currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  const fetchUsers = async () => {
    try {
      setLoading(true);
      setError(null);
      setBackendNotImplemented(false);

      const result = await userService.getUsers({
        page: currentPage,
        pageSize,
        role: filters.role,
        status: filters.status,
        package: filters.package,
        search: debouncedSearch,
        sortBy,
        sortOrder,
      });

      let items = [];
      let total = 0;

      if (Array.isArray(result)) {
        items = result;
        total = result.length;
      } else if (result?.items) {
        items = result.items;
        total = result.total ?? result.items.length;
      } else if (Array.isArray(result?.data)) {
        items = result.data;
        total = result.total ?? result.data.length;
      }

      setUsers(items);
      setTotalUsers(total);
    } catch (err) {
      if (err?.message?.includes('not implemented')) {
        setBackendNotImplemented(true);
        setError(null);
      } else {
        setBackendNotImplemented(false);
        setError(err?.message || 'Unknown error');
      }
      setUsers([]);
      setTotalUsers(0);
    } finally {
      setLoading(false);
    }
  };

  // Handle filter changes
  const handleFilterChange = (e) => {
    const { name, value } = e.target;
    setFilters((prev) => ({ ...prev, [name]: value }));
  };

  const handleSearchChange = (e) => {
    setFilters((prev) => ({ ...prev, search: e.target.value }));
  };


  // Handle reset filters
  const handleReset = () => {
    setFilters({
      search: '',
      role: 'all',
      status: 'all',
      package: 'all',
    });
    setSortBy('');
    setSortOrder('asc');
    setCurrentPage(1);
    setSelectedUsers(new Set());
  };

  const handleViewDetails = async (userId) => {
    setDetailModalOpen(true);
    setLoadingDetail(true);
    setDetailError(null);
    setDetailUser(null);
    try {
      const data = await userService.getUserById(userId);
      setDetailUser(data);
    } catch (err) {
      setDetailError(err?.message || t('loadError'));
    } finally {
      setLoadingDetail(false);
    }
  };

  const closeDetailModal = () => {
    setDetailModalOpen(false);
    setDetailUser(null);
    setDetailError(null);
  };

  // Handle user selection
  const handleSelectUser = (userId) => {
    const newSelected = new Set(selectedUsers);
    if (newSelected.has(userId)) {
      newSelected.delete(userId);
    } else {
      newSelected.add(userId);
    }
    setSelectedUsers(newSelected);
  };

  // Handle select all
  const handleSelectAll = () => {
    if (selectedUsers.size === users.length) {
      setSelectedUsers(new Set());
    } else {
      setSelectedUsers(new Set(users.map((u) => u.userId || u.id)));
    }
  };

  // Handle batch actions (TODO: implement when backend ready)
  const handleLockSelected = () => {
    setConfirmModal({
      type: 'lock',
      count: selectedUsers.size,
      userIds: Array.from(selectedUsers),
    });
  };

  const handleAssignPackage = () => {
    setConfirmModal({
      type: 'assignPackage',
      count: selectedUsers.size,
      userIds: Array.from(selectedUsers),
    });
  };

  const handleConfirmAction = () => {
    if (!confirmModal) {
      return;
    }

    if (confirmModal.type === 'lock') {
      console.log('Confirmed lock selected users:', confirmModal.userIds);
    } else {
      console.log('Confirmed assign package to users:', confirmModal.userIds);
    }

    setConfirmModal(null);
  };

  const handleCancelAction = () => {
    setConfirmModal(null);
  };

  const toggleSort = (field) => {
    if (sortBy === field) {
      setSortOrder((prev) => (prev === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(field);
      setSortOrder('asc');
    }
    setCurrentPage(1);
  };

  const compareValues = (a, b, field) => {
    const normalizeStatusValue = (value) => {
      if (typeof value === 'boolean') {
        return value ? 'active' : 'locked';
      }

      if (typeof value === 'string') {
        return value.trim().toLowerCase();
      }

      return '';
    };

    const getField = (item) => {
      if (!item) {
        return '';
      }

      switch (field) {
        case 'fullName':
          return item.fullName?.toLowerCase() ?? '';
        case 'email':
          return item.email?.toLowerCase() ?? '';
        case 'role':
          return item.role?.toLowerCase() ?? '';
        case 'registerDate':
          return item.registerDate ? new Date(item.registerDate).getTime() : 0;
        case 'status':
          return normalizeStatusValue(item.status);
        default:
          return '';
      }
    };

    const left = getField(a);
    const right = getField(b);

    if (left < right) {
      return -1;
    }
    if (left > right) {
      return 1;
    }
    return 0;
  };

  const displayedUsers = useMemo(() => {
    if (!sortBy) {
      return users;
    }

    return [...users].sort((a, b) => {
      const comparison = compareValues(a, b, sortBy);
      return sortOrder === 'asc' ? comparison : -comparison;
    });
  }, [users, sortBy, sortOrder]);

  const pageButtons = useMemo(() => {
    const buttons = [];
    if (totalPages <= 7) {
      for (let page = 1; page <= totalPages; page += 1) {
        buttons.push(page);
      }
      return buttons;
    }

    const leftBound = Math.max(2, currentPage - 2);
    const rightBound = Math.min(totalPages - 1, currentPage + 2);

    buttons.push(1);

    if (leftBound > 2) {
      buttons.push('start-ellipsis');
    }

    for (let page = leftBound; page <= rightBound; page += 1) {
      buttons.push(page);
    }

    if (rightBound < totalPages - 1) {
      buttons.push('end-ellipsis');
    }

    buttons.push(totalPages);

    return buttons;
  }, [currentPage, totalPages]);

  // Status badge component
  const StatusBadge = ({ status }) => {
    const normalizedStatus = (() => {
      if (typeof status === 'boolean') {
        return status ? 'active' : 'locked';
      }

      if (typeof status === 'string') {
        return status.trim().toLowerCase();
      }

      return '';
    })();

    const statusMap = {
      active: {
        className: 'status-badge status-active',
        label: t('active'),
      },
      locked: {
        className: 'status-badge status-locked',
        label: t('locked'),
      },
    };

    const config = statusMap[normalizedStatus] || statusMap.active;

    return (
      <span className={config.className}>
        {config.label}
      </span>
    );
  };

  // Skeleton loading component
  const SkeletonRow = () => (
    <tr className="skeleton-row">
      <td><div className="skeleton skeleton-checkbox" /></td>
      <td><div className="skeleton skeleton-text" /></td>
      <td><div className="skeleton skeleton-text" /></td>
      <td><div className="skeleton skeleton-text" /></td>
      <td><div className="skeleton skeleton-text" /></td>
      <td><div className="skeleton skeleton-text" /></td>
      <td><div className="skeleton skeleton-text" /></td>
      <td><div className="skeleton skeleton-text" /></td>
      <td><div className="skeleton skeleton-actions" /></td>
    </tr>
  );

  // Empty state component
  const EmptyState = () => (
    <div className="empty-state">
      <Users size={48} />
      <h3>{backendNotImplemented ? t('noUsersBackendTitle') : t('noUsers')}</h3>
      <p>{backendNotImplemented ? t('noUsersBackendDesc') : t('noUsersDesc')}</p>
    </div>
  );

  const ConfirmationModal = () => {
    if (!confirmModal) {
      return null;
    }

    const isLock = confirmModal.type === 'lock';
    const title = isLock ? t('confirmLockTitle') : t('confirmAssignPackageTitle');
    const description = isLock
      ? t('confirmLockDescription', { count: confirmModal.count })
      : t('confirmAssignPackageDescription', { count: confirmModal.count });
    const confirmLabel = isLock ? t('lockSelected') : t('assignPackage');

    return (
      <div className="modal-backdrop" role="dialog" aria-modal="true">
        <div className="modal-card">
          <h3>{title}</h3>
          <p>{description}</p>
          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={handleCancelAction}>
              {t('cancel')}
            </button>
            <button type="button" className="btn-primary" onClick={handleConfirmAction}>
              {confirmLabel}
            </button>
          </div>
        </div>
      </div>
    );
  };

  const UserDetailsModal = () => {
    if (!detailModalOpen) return null;

    const handleBackdropClick = (e) => {
      if (e.target.classList.contains('modal-backdrop')) {
        closeDetailModal();
      }
    };

    return (
      <div 
        className="modal-backdrop user-detail-backdrop" 
        onClick={handleBackdropClick}
        role="dialog" 
        aria-modal="true"
      >
        <div className="modal-card user-detail-card">
          <button 
            type="button" 
            className="close-btn" 
            onClick={closeDetailModal}
            aria-label="Close"
          >
            <X size={20} />
          </button>

          {loadingDetail && (
            <div className="detail-skeleton">
              <div className="skeleton-avatar-row">
                <div className="skeleton skeleton-circle animate-pulse" />
                <div className="skeleton-title-group">
                  <div className="skeleton skeleton-title animate-pulse" />
                  <div className="skeleton skeleton-subtitle animate-pulse" />
                </div>
              </div>
              <div className="skeleton-grid">
                <div className="skeleton skeleton-box animate-pulse" />
                <div className="skeleton skeleton-box animate-pulse" />
                <div className="skeleton skeleton-box animate-pulse" />
                <div className="skeleton skeleton-box animate-pulse" />
              </div>
            </div>
          )}

          {detailError && (
            <div className="detail-error">
              <AlertCircle size={40} className="error-icon" />
              <h4>{t('loadError')}</h4>
              <p>{detailError}</p>
              <button type="button" className="btn-primary" onClick={closeDetailModal}>
                {t('cancel')}
              </button>
            </div>
          )}

          {!loadingDetail && !detailError && detailUser && (
            <>
              {/* Header section */}
              <div className="detail-header">
                <div 
                  className="avatar-wrapper"
                  style={{ cursor: detailUser.imageUrl ? 'pointer' : 'default' }}
                  onClick={() => detailUser.imageUrl && window.open(getAvatarUrl(detailUser.imageUrl), '_blank')}
                  title={detailUser.imageUrl ? 'Bấm để phóng to ảnh đại diện' : undefined}
                >
                  {detailUser.imageUrl ? (
                    <img 
                      src={getAvatarUrl(detailUser.imageUrl)} 
                      alt={detailUser.fullName} 
                      className="user-avatar-img"
                      onError={(e) => {
                        e.target.onerror = null;
                        e.target.style.display = 'none';
                        const placeholder = e.target.nextSibling;
                        if (placeholder) placeholder.style.display = 'flex';
                      }}
                    />
                  ) : null}
                  <div 
                    className="user-avatar-placeholder"
                    style={{ 
                      display: detailUser.imageUrl ? 'none' : 'flex'
                    }}
                  >
                    {detailUser.fullName ? detailUser.fullName.charAt(0).toUpperCase() : '?'}
                  </div>
                </div>
                
                <div className="header-info">
                  <h3 className="user-name">{detailUser.fullName || '-'}</h3>
                  <div className="user-badges">
                    <span className="role-badge">
                      <Shield size={12} />
                      {detailUser.role === 'admin' ? t('admin') : t('user')}
                    </span>
                    <span className={`status-badge ${detailUser.isLocked ? 'status-locked' : 'status-active'}`}>
                      {detailUser.isLocked ? <Lock size={12} /> : <Unlock size={12} />}
                      {detailUser.isLocked ? t('locked') : t('active')}
                    </span>
                  </div>
                </div>
              </div>

              {/* Detail body */}
              <div className="detail-body">
                {detailUser.isLocked && (
                  <div className="lock-banner">
                    <div className="lock-banner-header">
                      <AlertCircle size={16} />
                      <span>{t('accountSecurity')} - {t('locked')}</span>
                    </div>
                    <div className="lock-banner-details">
                      {detailUser.lockReason && (
                        <p><strong>{t('lockReason')}:</strong> {detailUser.lockReason}</p>
                      )}
                      {detailUser.lockedAt && (
                        <p>
                          <strong>{t('lockedAt')}:</strong>{' '}
                          {new Date(detailUser.lockedAt).toLocaleString()}
                        </p>
                      )}
                    </div>
                  </div>
                )}

                {/* Account details */}
                <div className="detail-section">
                  <h4 className="section-title">{t('accountSecurity')}</h4>
                  <div className="info-grid">
                    <div className="info-item">
                      <Mail size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">{t('email')}</span>
                        <span className="info-value">{detailUser.email}</span>
                      </div>
                    </div>
                    <div className="info-item">
                      <Phone size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">{t('phoneNumber')}</span>
                        <span className="info-value">{detailUser.phoneNumber || '-'}</span>
                      </div>
                    </div>
                    <div className="info-item">
                      <Calendar size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">{t('registerDate')}</span>
                        <span className="info-value">
                          {detailUser.createdAt 
                            ? new Date(detailUser.createdAt).toLocaleDateString()
                            : '-'}
                        </span>
                      </div>
                    </div>
                    <div className="info-item">
                      <Shield size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">Xác minh email</span>
                        <span className={`info-value ${detailUser.emailConfirmedAt ? 'text-success' : 'text-warning'}`}>
                          {detailUser.emailConfirmedAt 
                            ? `${t('emailConfirmed')} (${new Date(detailUser.emailConfirmedAt).toLocaleDateString()})`
                            : t('emailUnconfirmed')}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Gói & Quota */}
                <div className="detail-section">
                  <h4 className="section-title">{t('subscriptionQuota')}</h4>
                  <div className="info-grid">
                    <div className="info-item">
                      <CreditCard size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">{t('package')}</span>
                        <span className="info-value package-highlight">{detailUser.package || '-'}</span>
                      </div>
                    </div>
                    <div className="info-item">
                      <Activity size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">{t('quota')}</span>
                        <span className="info-value quota-highlight">{detailUser.quota || '-'}</span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Uploaded CVs list */}
                <div className="detail-section">
                  <h4 className="section-title">{t('cvListTitle')}</h4>
                  {detailUser.cvFiles && detailUser.cvFiles.length > 0 ? (
                    <div className="cv-list">
                      {detailUser.cvFiles.map((cv) => {
                        const sizeInKb = (cv.fileSize / 1024).toFixed(1);
                        return (
                          <div key={cv.cvFileId} className="cv-item-card">
                            <div className="cv-item-left">
                              <FileText className="cv-icon" size={24} />
                              <div className="cv-info">
                                <span className="cv-name" title={cv.fileName}>{cv.fileName}</span>
                                <div className="cv-meta">
                                  <span>{sizeInKb} KB</span>
                                  <span className="meta-separator">•</span>
                                  <span>{new Date(cv.uploadedAt).toLocaleDateString()}</span>
                                </div>
                              </div>
                            </div>
                            <div className="cv-item-right">
                              <span className={`cv-status-badge cv-status-${cv.status.toLowerCase()}`}>
                                {cv.status === 'Success' && t('cvStatusSuccess')}
                                {cv.status === 'Pending' && t('cvStatusPending')}
                                {cv.status === 'Processing' && t('cvStatusProcessing')}
                                {cv.status === 'Failed' && t('cvStatusFailed')}
                              </span>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="empty-profile-banner">
                      <FileText size={24} className="banner-icon" />
                      <span>{t('noCVs')}</span>
                    </div>
                  )}
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    );
  };


  const EditRoleModal = () => {
    if (!roleModal) return null;

    const handleSaveRole = () => {
      const { user, selectedRole } = roleModal;

      // No change - just close
      if ((user.role?.toLowerCase() ?? '') === selectedRole) {
        setRoleModal(null);
        return;
      }

      // Always show confirm modal before saving
      setConfirmAction({
        type: 'changeRole',
        user,
        targetRole: selectedRole,
      });
      setRoleModal(null);
    };

    return (
      <div 
        className="modal-backdrop" 
        onClick={(e) => e.target.classList.contains('modal-backdrop') && setRoleModal(null)}
        role="dialog" 
        aria-modal="true"
      >
        <div className="modal-card">
          <div className="modal-header-row" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--spacing-md)' }}>
            <h3 style={{ margin: 0 }}>{t('editRoleTitle')}</h3>
            <button 
              type="button" 
              className="ghost-btn" 
              onClick={() => setRoleModal(null)}
              style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}
            >
              <X size={20} />
            </button>
          </div>
          <p style={{ marginBottom: 'var(--spacing-md)' }}>
            <strong>{t('fullName')}:</strong> {roleModal.user.fullName}
          </p>
          <div className="form-group" style={{ marginBottom: 'var(--spacing-lg)' }}>
            <label style={{ display: 'block', marginBottom: 'var(--spacing-xs)', fontWeight: 'var(--fw-semibold)' }}>
              {t('selectRole')}
            </label>
            <select
              className="filter-select"
              style={{ width: '100%' }}
              value={roleModal.selectedRole}
              onChange={(e) => setRoleModal({ ...roleModal, selectedRole: e.target.value })}
            >
              <option value="user">{t('user')}</option>
              <option value="admin">{t('admin')}</option>
            </select>
          </div>
          <div className="modal-actions" style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--spacing-md)' }}>
            <button type="button" className="btn-secondary" onClick={() => setRoleModal(null)}>
              {t('cancel')}
            </button>
            <button type="button" className="btn-primary" onClick={handleSaveRole}>
              {t('save')}
            </button>
          </div>
        </div>
      </div>
    );
  };

  const ActionConfirmModal = () => {
    if (!confirmAction) return null;

    const handleConfirm = async () => {
      const { type, user, targetRole } = confirmAction;
      try {
        if (type === 'changeRole' || type === 'upgradeRole') {
          await userService.assignRole(user.userId || user.id, targetRole);
          alert(t('roleUpdatedSuccess'));
        } else if (type === 'lockUser') {
          await userService.lockUser(user.userId || user.id);
          alert(t('statusUpdatedSuccess'));
        } else if (type === 'unlockUser') {
          await userService.unlockUser(user.userId || user.id);
          alert(t('statusUpdatedSuccess'));
        }
        fetchUsers();
        setConfirmAction(null);
      } catch (err) {
        alert(err?.message || 'Có lỗi xảy ra');
        setConfirmAction(null);
      }
    };

    const getRoleLabel = (role) => {
      if (!role) return '';
      return role.charAt(0).toUpperCase() + role.slice(1).toLowerCase();
    };

    let title = '';
    let description = '';
    let confirmBtnLabel = t('save');
    let confirmBtnClass = 'btn-primary';

    if (confirmAction.type === 'changeRole' || confirmAction.type === 'upgradeRole') {
      title = t('editRoleTitle');
      const userName = confirmAction.user?.fullName || confirmAction.user?.email || '';
      const newRoleLabel = getRoleLabel(confirmAction.targetRole);
      description = t('confirmChangeRole', { userName, newRoleLabel, defaultValue: `Bạn có chắc chắn muốn thay đổi role của "${userName}" thành "${newRoleLabel}" không?` });
      confirmBtnLabel = t('save', 'Lưu');
    } else if (confirmAction.type === 'lockUser') {
      title = t('confirmLockTitle');
      description = t('confirmLockUser');
      confirmBtnLabel = t('lockUser');
      confirmBtnClass = 'btn-danger';
    } else if (confirmAction.type === 'unlockUser') {
      title = t('confirmLockTitle');
      description = t('confirmUnlockUser');
      confirmBtnLabel = t('unlockUser');
    }

    return (
      <div 
        className="modal-backdrop" 
        onClick={(e) => e.target.classList.contains('modal-backdrop') && setConfirmAction(null)}
        role="dialog" 
        aria-modal="true"
      >
        <div className="modal-card" style={{ maxWidth: '440px' }}>
          {/* Icon header */}
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '12px', marginBottom: '20px' }}>
            <div style={{
              width: '52px', height: '52px', borderRadius: '50%',
              background: confirmBtnClass === 'btn-danger'
                ? 'rgba(250, 119, 119, 0.12)'
                : 'rgba(111, 182, 232, 0.12)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              color: confirmBtnClass === 'btn-danger' ? 'var(--danger)' : 'var(--primary)',
              fontSize: '24px',
            }}>
              {confirmBtnClass === 'btn-danger' ? '🔒' : '✏️'}
            </div>
            <h3 style={{ margin: 0, fontSize: '1.1rem', color: 'var(--text-primary)' }}>{title}</h3>
          </div>
          <p style={{ 
            margin: '0 0 24px', 
            color: 'var(--text-secondary)', 
            textAlign: 'center',
            lineHeight: '1.6',
            fontSize: '0.95rem',
          }}>{description}</p>
          <div className="modal-actions" style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--spacing-md)' }}>
            <button type="button" className="btn-secondary" onClick={() => setConfirmAction(null)}>
              {t('cancel')}
            </button>
            <button type="button" className={confirmBtnClass} onClick={handleConfirm}>
              {confirmBtnLabel}
            </button>
          </div>
        </div>
      </div>
    );
  };


  // Error state component
  const ErrorState = () => (
    <div className="error-state">
      <AlertCircle size={20} />
      <div>
        <p>{t('loadError')}</p>
        <p className="error-subtext">{t('tryAgain')}</p>
      </div>
    </div>
  );

  return (
    <div className="admin-dashboard-page user-management-page">
      {/* Page Header */}
      <div className="page-header">
        <div className="breadcrumb">
          <span>Admin</span>
          <span className="separator">/</span>
          <span aria-current="page">{t('breadcrumb')}</span>
        </div>

        <div className="header-top">
          <div className="title-section">
            <h1 className="page-title">{t('title')}</h1>
            <p className="page-description">{t('description')}</p>
          </div>
        </div>
      </div>

      <div className="page-content">
        {/* Batch Action Bar */}
        {selectedUsers.size > 0 && (
          <div className="batch-action-bar">
            <span className="batch-info">
              {t('selectedUsers', { count: selectedUsers.size })}
            </span>
            <div className="batch-buttons">
              <button
                className="btn-secondary"
                type="button"
                onClick={handleLockSelected}
              >
                {t('lockSelected')}
              </button>
              <button
                className="btn-primary"
                type="button"
                onClick={handleAssignPackage}
              >
                {t('assignPackage')}
              </button>
            </div>
          </div>
        )}

        {/* Filter Card */}
        {!error && (
          <div className="filter-card">
            <div className="filter-row">
              <div className="filter-group search-group">
                <Search size={20} />
                <input
                  type="text"
                  placeholder={t('search')}
                  name="search"
                  value={filters.search}
                  onChange={handleSearchChange}
                  className="search-input"
                />
              </div>

              <select
                name="role"
                value={filters.role}
                onChange={handleFilterChange}
                className="filter-select"
              >
                <option value="all">{t('allRoles')}</option>
                <option value="user">{t('user')}</option>
                <option value="admin">{t('admin')}</option>
              </select>

              <select
                name="status"
                value={filters.status}
                onChange={handleFilterChange}
                className="filter-select"
              >
                <option value="all">{t('allStatus')}</option>
                <option value="active">{t('active')}</option>
                <option value="locked">{t('locked')}</option>
              </select>

              <select
                name="package"
                value={filters.package}
                onChange={handleFilterChange}
                className="filter-select"
              >
                <option value="all">{t('allPackages')}</option>
                <option value="free">{t('free')}</option>
                <option value="premium">{t('premium')}</option>
                <option value="pro">{t('pro')}</option>
              </select>

              <button
                className="btn-secondary filter-reset-btn"
                type="button"
                onClick={handleReset}
              >
                {t('reset')}
              </button>
            </div>
          </div>
        )}

        {/* Error State */}
        {error && <ErrorState />}

        {/* Table Card */}
        {!error && (
          <div className="table-card">
            {loading ? (
              <table className="users-table">
                <thead>
                  <tr>
                    <th className="col-checkbox">
                      <input
                        type="checkbox"
                        disabled
                        className="select-checkbox"
                      />
                    </th>
                    <th>{t('fullName')}</th>
                    <th>{t('email')}</th>
                    <th>{t('role')}</th>
                    <th>{t('package')}</th>
                    <th>{t('quota')}</th>
                    <th>{t('registerDate')}</th>
                    <th>{t('status')}</th>
                    <th className="col-actions">{t('actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {[...Array(5)].map((_, i) => (
                    <SkeletonRow key={i} />
                  ))}
                </tbody>
              </table>
            ) : users.length === 0 ? (
              <EmptyState />
            ) : (
              <>
                <table className="users-table">
                  <thead>
                    <tr>
                      <th className="col-checkbox">
                        <input
                          type="checkbox"
                          checked={
                            displayedUsers.length > 0
                            && selectedUsers.size === displayedUsers.length
                          }
                          onChange={handleSelectAll}
                          className="select-checkbox"
                        />
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('fullName')}>
                        <span>{t('fullName')}</span>
                        {sortBy === 'fullName' && (
                          sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />
                        )}
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('email')}>
                        <span>{t('email')}</span>
                        {sortBy === 'email' && (
                          sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />
                        )}
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('role')}>
                        <span>{t('role')}</span>
                        {sortBy === 'role' && (
                          sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />
                        )}
                      </th>
                      <th>{t('package')}</th>
                      <th>{t('quota')}</th>
                      <th className="sortable-header" onClick={() => toggleSort('registerDate')}>
                        <span>{t('registerDate')}</span>
                        {sortBy === 'registerDate' && (
                          sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />
                        )}
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('status')}>
                        <span>{t('status')}</span>
                        {sortBy === 'status' && (
                          sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />
                        )}
                      </th>
                      <th className="col-actions">{t('actions')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {displayedUsers.map((user) => {
                      const userIdVal = user.userId || user.id;
                      const registerDateVal = user.createdAt || user.registerDate;
                      return (
                        <tr key={userIdVal}>
                          <td className="col-checkbox">
                            <input
                              type="checkbox"
                              checked={selectedUsers.has(userIdVal)}
                              onChange={() => handleSelectUser(userIdVal)}
                              className="select-checkbox"
                            />
                          </td>
                          <td className="col-name">{user.fullName || '-'}</td>
                          <td className="col-email">{user.email || '-'}</td>
                          <td className="col-role">
                            <span className="role-badge">{user.role || '-'}</span>
                          </td>
                          <td className="col-package">{user.package || '-'}</td>
                          <td className="col-quota">{user.quota || '-'}</td>
                          <td className="col-date">
                            {registerDateVal
                              ? new Date(registerDateVal).toLocaleDateString()
                              : '-'}
                          </td>
                          <td className="col-status">
                            <StatusBadge status={user.status} />
                          </td>
                          <td className="col-actions">
                            <div className="action-buttons">
                              <button
                                className="action-btn"
                                type="button"
                                title={t('viewDetail')}
                                aria-label={t('viewDetail')}
                                onClick={() => handleViewDetails(userIdVal)}
                              >
                                <Eye size={18} />
                              </button>
                              <button
                                className="action-btn"
                                type="button"
                                title={t('assignRole')}
                                aria-label={t('assignRole')}
                                onClick={() => {
                                  setRoleModal({
                                    user: user,
                                    selectedRole: user.role?.toLowerCase() || 'user'
                                  });
                                }}
                              >
                                <UserCog size={18} />
                              </button>
                              <button
                                className="action-btn"
                                type="button"
                                title={user.status ? t('lockUser') : t('unlockUser')}
                                aria-label={user.status ? t('lockUser') : t('unlockUser')}
                                onClick={() => {
                                  setConfirmAction({
                                    type: user.status ? 'lockUser' : 'unlockUser',
                                    user: user
                                  });
                                }}
                              >
                                {user.status ? <Lock size={18} /> : <Unlock size={18} />}
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>

                {/* Pagination */}
                <div className="pagination">
                  <div className="pagination-info">
                    <span>
                      {t('showing')} {startIndex}-{endIndex} {t('of')} {totalUsers} {t('users')}
                    </span>
                    <div className="page-size-selector">
                      <label>{t('pageSize')}</label>
                      <select
                        value={pageSize}
                        onChange={(e) => {
                          setPageSize(Number(e.target.value));
                          setCurrentPage(1);
                        }}
                        className="page-size-select"
                      >
                        <option value={10}>10</option>
                        <option value={20}>20</option>
                        <option value={50}>50</option>
                        <option value={100}>100</option>
                      </select>
                    </div>
                  </div>

                  <div className="pagination-buttons">
                    {/* Desktop: Full pagination */}
                    <div className="pagination-desktop">
                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={currentPage === 1}
                        onClick={() => setCurrentPage(1)}
                        title={t('firstPage')}
                      >
                        <ChevronsLeft size={18} />
                      </button>

                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={currentPage === 1}
                        onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                        title={t('previous')}
                      >
                        <ChevronLeft size={18} />
                      </button>

                      {pageButtons.map((button) => (
                        button === 'start-ellipsis' || button === 'end-ellipsis' ? (
                          <span key={button} className="pagination-ellipsis">
                            …
                          </span>
                        ) : (
                          <button
                            key={button}
                            className={`pagination-btn ${currentPage === button ? 'active' : ''}`}
                            type="button"
                            onClick={() => setCurrentPage(button)}
                          >
                            {button}
                          </button>
                        )
                      ))}

                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={currentPage === totalPages}
                        onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                        title={t('next')}
                      >
                        <ChevronRight size={18} />
                      </button>

                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={currentPage === totalPages}
                        onClick={() => setCurrentPage(totalPages)}
                        title={t('lastPage')}
                      >
                        <ChevronsRight size={18} />
                      </button>
                    </div>

                    {/* Mobile/Tablet: Compact pagination */}
                    <div className="pagination-mobile">
                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={currentPage === 1}
                        onClick={() => setCurrentPage((p) => p - 1)}
                        title={t('previous')}
                      >
                        <ChevronLeft size={18} />
                      </button>

                      <span className="current-page">{currentPage}</span>

                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={currentPage === totalPages}
                        onClick={() => setCurrentPage((p) => p + 1)}
                        title={t('next')}
                      >
                        <ChevronRight size={18} />
                      </button>
                    </div>
                  </div>
                </div>
              </>
            )}
          </div>
        )}
      </div>
      <ConfirmationModal />
      <UserDetailsModal />
      <EditRoleModal />
      <ActionConfirmModal />
    </div>
  );
}

export default UserManagementPage;