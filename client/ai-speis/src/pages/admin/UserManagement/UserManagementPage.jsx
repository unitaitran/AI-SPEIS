import React, { useCallback, useEffect, useMemo, useState } from 'react';
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

  const getUserId = (user) => user?.userId ?? user?.id ?? user?._id ?? null;

  const [filters, setFilters] = useState({
    search: '',
    role: 'all',
    status: 'all',
    package: 'all',
  });
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
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [detailUser, setDetailUser] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState(null);
  const [roleModal, setRoleModal] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null);
  const [debouncedSearch, setDebouncedSearch] = useState('');

  const totalPages = Math.max(1, Math.ceil(totalUsers / pageSize));
  const startIndex = totalUsers === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endIndex = totalUsers === 0 ? 0 : Math.min(currentPage * pageSize, totalUsers);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, 400);

    return () => clearTimeout(timer);
  }, [filters.search]);

  useEffect(() => {
    setCurrentPage(1);
  }, [filters.role, filters.status, filters.package, debouncedSearch]);

  const fetchUsers = useCallback(async () => {
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
  }, [currentPage, pageSize, filters.role, filters.status, filters.package, debouncedSearch, sortBy, sortOrder]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  useEffect(() => {
    if (currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  const handleFilterChange = (e) => {
    const { name, value } = e.target;
    setFilters((prev) => ({ ...prev, [name]: value }));
  };

  const handleSearchChange = (e) => {
    setFilters((prev) => ({ ...prev, search: e.target.value }));
  };

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

  const handleViewDetails = async (userOrId) => {
    const id = typeof userOrId === 'object' ? getUserId(userOrId) : userOrId;
    if (!id) return;

    setShowDetailModal(true);
    setDetailLoading(true);
    setDetailError(null);
    setDetailUser(null);
    try {
      // Prefer getUserDetail if available; fallback to getUserById
      const data = (userService.getUserDetail) ? await userService.getUserDetail(id) : await userService.getUserById(id);
      setDetailUser(data);
    } catch (err) {
      setDetailError(err?.message || t('loadError'));
    } finally {
      setDetailLoading(false);
    }
  };

  const closeDetailModal = () => {
    setShowDetailModal(false);
    setDetailUser(null);
    setDetailError(null);
  };

  // Handle user selection
  const handleSelectUser = (userOrId) => {
    const id = typeof userOrId === 'object' ? getUserId(userOrId) : userOrId;
    if (!id) return;

    const newSelected = new Set(selectedUsers);
    if (newSelected.has(id)) {
      newSelected.delete(id);
    } else {
      newSelected.add(id);
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

    // Batch action confirmed (actual implementation depends on backend readiness)
    setConfirmModal(null);
  };

  const handleCancelAction = () => {
    setConfirmModal(null);
  };

  const getDetailField = (source, keys) => {
    if (!source) {
      return null;
    }

    for (let i = 0; i < keys.length; i += 1) {
      const value = source[keys[i]];
      if (value !== undefined && value !== null && value !== '') {
        return value;
      }
    }

    return null;
  };

  const formatDateValue = (value) => {
    if (!value) {
      return null;
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return String(value);
    }

    return date.toLocaleString();
  };

  // (handleOpenDetail removed - consolidated into handleViewDetails)

  // (duplicate handleSelectUser removed - unified earlier)
  const normalizeStatus = (status) => {
  if (status == null) return '';

  if (typeof status === 'string') {
    return status.toLowerCase();
  }

  if (typeof status === 'boolean') {
    return status ? 'active' : 'locked';
  }

  if (typeof status === 'number') {
    return status === 1 ? 'active' : 'locked';
  }

  if (typeof status === 'object') {
    return (
      status.name?.toLowerCase() ||
      status.value?.toLowerCase() ||
      status.status?.toLowerCase() ||
      ''
    );
  }

  return '';
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
          return normalizeStatus(item.status);
        default:
          return '';
      }
    };

    const left = getField(a);
    const right = getField(b);

    if (left < right) return -1;
    if (left > right) return 1;
    return 0;
  };

  // Sorting helper
  const toggleSort = (field) => {
    if (sortBy === field) {
      setSortOrder((prev) => (prev === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(field);
      setSortOrder('asc');
    }
    setCurrentPage(1);
  };

  // Open role modal
  const handleOpenRoleModal = (user) => {
    if (!user) return;
    setRoleModal({ user, selectedRole: (user.role || 'user') });
  };

  // Toggle lock/unlock - open confirm action
  const handleToggleLock = (user) => {
    if (!user) return;
    const status = normalizeStatus(user?.status ?? user?.isLocked);
    if (status === 'locked') {
      setConfirmAction({ type: 'unlockUser', user });
    } else {
      setConfirmAction({ type: 'lockUser', user });
    }
  };

  // Error state UI
  const ErrorState = () => (
    <div className="error-state">
      <AlertCircle size={24} />
      <div>
        <p>{t('loadError') || 'Unable to load data.'}</p>
        {error && <p className="error-subtext">{error}</p>}
      </div>
    </div>
  );

  const displayedUsers = useMemo(() => {
    if (!sortBy) {
      return users;
    }

    return [...users].sort((a, b) => {
      const comparison = compareValues(a, b, sortBy);
      return sortOrder === 'asc' ? comparison : -comparison;
    });
  }, [compareValues, users, sortBy, sortOrder]);

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
    const normalizedStatus = normalizeStatus(status);
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

    return <span className={config.className}>{config.label}</span>;
  };

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

  const EmptyState = () => (
    <div className="empty-state">
      <Users size={48} />
      <h3>{backendNotImplemented ? t('noUsersBackendTitle') : t('noUsers')}</h3>
      <p>{backendNotImplemented ? t('noUsersBackendDesc') : t('noUsersDesc')}</p>
    </div>
  );

  // Unified detail modal (replaces duplicate DetailModal + UserDetailsModal)
  const DetailModal = () => {
    if (!showDetailModal) return null;

    const avatarUrl = getDetailField(detailUser, ['avatarUrl', 'avatar', 'photoUrl', 'picture']) || detailUser?.imageUrl;
    const fullName = getDetailField(detailUser, ['fullName', 'name']) || detailUser?.fullName;

    const registrationDate = formatDateValue(getDetailField(detailUser, ['registrationDate', 'registerDate', 'createdAt', 'createdDate']) || detailUser?.createdAt);

    const handleBackdropClick = (e) => {
      if (e.target.classList && e.target.classList.contains('modal-backdrop')) closeDetailModal();
    };

    return (
      <div className="modal-backdrop user-detail-backdrop" onClick={handleBackdropClick} role="dialog" aria-modal="true">
        <div className="modal-card detail-modal-card user-detail-card">
          <button type="button" className="close-btn" onClick={closeDetailModal} aria-label="Close">
            <X size={20} />
          </button>

          {detailLoading ? (
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
          ) : detailError ? (
            <div className="detail-error">
              <AlertCircle size={40} className="error-icon" />
              <h4>{t('loadError')}</h4>
              <p>{detailError}</p>
              <button type="button" className="btn-primary" onClick={closeDetailModal}>
                {t('cancel')}
              </button>
            </div>
          ) : detailUser ? (
            <>
              <div className="detail-header">
                <div className="avatar-wrapper" title={avatarUrl ? t('avatarLabel') : undefined} onClick={() => avatarUrl && window.open(getAvatarUrl(avatarUrl), '_blank')}>
                  {avatarUrl ? (
                    <img src={getAvatarUrl(avatarUrl)} alt={fullName || t('avatarLabel')} className="user-avatar-img" />
                  ) : (
                    <div className="user-avatar-placeholder">{fullName ? fullName.charAt(0).toUpperCase() : '?'}</div>
                  )}
                </div>

                <div className="header-info">
                  <h3 className="user-name">{fullName || '-'}</h3>
                  <div className="user-badges">
                    <span className="role-badge">
                      <Shield size={12} />
                      {detailUser.role === 'admin' ? t('admin') : t('user')}
                    </span>
                    <span className={`status-badge ${normalizeStatus(detailUser.status ?? detailUser.isLocked) === 'locked' ? 'status-locked' : 'status-active'}`}>
                      {normalizeStatus(detailUser.status ?? detailUser.isLocked) === 'locked' ? <Lock size={12} /> : <Unlock size={12} />}
                      {normalizeStatus(detailUser.status ?? detailUser.isLocked) === 'locked' ? t('locked') : t('active')}
                    </span>
                  </div>
                </div>
              </div>

              <div className="detail-body">
                {normalizeStatus(detailUser.status ?? detailUser.isLocked) === 'locked' && (
                  <div className="lock-banner">
                    <div className="lock-banner-header">
                      <AlertCircle size={16} />
                      <span>{t('accountSecurity')} - {t('locked')}</span>
                    </div>
                    <div className="lock-banner-details">
                      {detailUser.lockReason && (<p><strong>{t('lockReason')}:</strong> {detailUser.lockReason}</p>)}
                      {detailUser.lockedAt && (<p><strong>{t('lockedAt')}:</strong> {new Date(detailUser.lockedAt).toLocaleString()}</p>)}
                    </div>
                  </div>
                )}

                <div className="detail-section">
                  <h4 className="section-title">{t('accountSecurity')}</h4>
                  <div className="info-grid">
                    <div className="info-item">
                      <Mail size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">{t('email')}</span>
                        <span className="info-value">{detailUser.email || '-'}</span>
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
                        <span className="info-value">{registrationDate || '-'}</span>
                      </div>
                    </div>
                    <div className="info-item">
                      <Shield size={16} className="info-icon" />
                      <div className="info-content">
                        <span className="info-label">{t('emailVerified')}</span>
                        <span className={`info-value ${detailUser.emailConfirmedAt ? 'text-success' : 'text-warning'}`}>
                          {detailUser.emailConfirmedAt ? `${t('emailConfirmed')} (${new Date(detailUser.emailConfirmedAt).toLocaleDateString()})` : t('emailUnconfirmed')}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>

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
                              <span className={`cv-status-badge cv-status-${String(cv.status).toLowerCase()}`}>
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
          ) : (
            <div className="detail-empty">{t('noDetailAvailable')}</div>
          )}
        </div>
      </div>
    );
  };

  const EditRoleModal = () => {
    if (!roleModal) {
      return null;
    }

    const handleSaveRole = async () => {
      const { user, selectedRole } = roleModal;
      
      const isUpgradingToAdmin = user.role?.toLowerCase() === 'user' && selectedRole === 'admin';
      
      if (isUpgradingToAdmin) {
        setConfirmAction({
          type: 'upgradeRole',
          user,
          targetRole: selectedRole
        });
        setRoleModal(null);
        return;
      }

      if (user.role?.toLowerCase() === selectedRole) {
        setRoleModal(null);
        return;
      }

      try {
        await userService.assignRole(user.userId || user.id, selectedRole);
        fetchUsers();
        setRoleModal(null);
        alert(t('roleUpdatedSuccess'));
      } catch (err) {
        alert(err?.message || 'Có lỗi xảy ra');
      }
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
            <strong>{t('fullName')}:</strong> {roleModal.user.fullName || '-'}
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
    if (!confirmAction) {
      return null;
    }

    const handleConfirm = async () => {
      const { type, user, targetRole, userIds } = confirmAction;

      try {
        if (type === 'upgradeRole') {
          await userService.assignRole(user.userId || user.id, targetRole);
          alert(t('roleUpdatedSuccess'));
        } else if (type === 'lockUser') {
          await userService.lockUser(getUserId(user));
          window.alert(t('statusUpdatedSuccess'));
        } else if (type === 'unlockUser') {
          await userService.unlockUser(getUserId(user));
          window.alert(t('statusUpdatedSuccess'));
        } else if (type === 'lockSelected') {
          await Promise.all(userIds.map((id) => userService.lockUser(id)));
          window.alert(t('statusUpdatedSuccess'));
        } else if (type === 'assignPackage') {
          await userService.assignPackage();
        }

        await fetchUsers();
        setSelectedUsers(new Set());
        setConfirmAction(null);
      } catch (err) {
        window.alert(err?.message || 'Có lỗi xảy ra');
        setConfirmAction(null);
      }
    };

    const getRoleLabel = (role) => {
      if (!role) return '';
      return role.charAt(0).toUpperCase() + role.slice(1).toLowerCase();
    };

    let title = '';
    let description = '';

    if (confirmAction.type === 'changeRole' || confirmAction.type === 'upgradeRole') {
      title = t('editRoleTitle');
      description = t('confirmUpgradeToAdmin');
    } else if (confirmAction.type === 'lockUser') {
      title = t('confirmLockTitle');
      description = t('confirmLockUser');
    } else if (confirmAction.type === 'unlockUser') {
      title = t('confirmLockTitle');
      description = t('confirmUnlockUser');
    }

    const confirmBtnClass = (confirmAction.type === 'lockUser' || confirmAction.type === 'lockSelected') ? 'btn-danger' : 'btn-primary';

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
            <button type="button" className="btn-primary" onClick={handleConfirm}>
              {confirmAction.type === 'upgradeRole' ? t('save') : (confirmAction.type === 'lockUser' ? t('lockUser') : t('unlockUser'))}
            </button>
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="admin-dashboard-page user-management-page">
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
        {selectedUsers.size > 0 && (
          <div className="batch-action-bar">
            <span className="batch-info">
              {t('selectedUsers', { count: selectedUsers.size })}
            </span>
            <div className="batch-buttons">
              <button className="btn-secondary" type="button" onClick={handleLockSelected}>
                {t('lockSelected')}
              </button>
              <button className="btn-primary" type="button" onClick={handleAssignPackage}>
                {t('assignPackage')}
              </button>
            </div>
          </div>
        )}

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

              <select name="role" value={filters.role} onChange={handleFilterChange} className="filter-select">
                <option value="all">{t('allRoles')}</option>
                <option value="user">{t('user')}</option>
                <option value="admin">{t('admin')}</option>
              </select>

              <select name="status" value={filters.status} onChange={handleFilterChange} className="filter-select">
                <option value="all">{t('allStatus')}</option>
                <option value="active">{t('active')}</option>
                <option value="locked">{t('locked')}</option>
              </select>

              <select name="package" value={filters.package} onChange={handleFilterChange} className="filter-select">
                <option value="all">{t('allPackages')}</option>
                <option value="free">{t('free')}</option>
                <option value="premium">{t('premium')}</option>
                <option value="pro">{t('pro')}</option>
              </select>

              <button className="btn-secondary filter-reset-btn" type="button" onClick={handleReset}>
                {t('reset')}
              </button>
            </div>
          </div>
        )}

        {error && <ErrorState />}

        {!error && (
          <div className="table-card">
            {loading ? (
              <table className="users-table">
                <thead>
                  <tr>
                    <th className="col-checkbox">
                      <input type="checkbox" disabled className="select-checkbox" />
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
                  {[...Array(5)].map((_, index) => (
                    <SkeletonRow key={index} />
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
                          checked={displayedUsers.length > 0 && selectedUsers.size === displayedUsers.length}
                          onChange={handleSelectAll}
                          className="select-checkbox"
                        />
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('fullName')}>
                        <span>{t('fullName')}</span>
                        {sortBy === 'fullName' && (sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('email')}>
                        <span>{t('email')}</span>
                        {sortBy === 'email' && (sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('role')}>
                        <span>{t('role')}</span>
                        {sortBy === 'role' && (sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
                      </th>
                      <th>{t('package')}</th>
                      <th>{t('quota')}</th>
                      <th className="sortable-header" onClick={() => toggleSort('registerDate')}>
                        <span>{t('registerDate')}</span>
                        {sortBy === 'registerDate' && (sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
                      </th>
                      <th className="sortable-header" onClick={() => toggleSort('status')}>
                        <span>{t('status')}</span>
                        {sortBy === 'status' && (sortOrder === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
                      </th>
                      <th className="col-actions">{t('actions')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {displayedUsers.map((user) => {
                      const id = getUserId(user);
                      const normalizedUserStatus = normalizeStatus(user?.status ?? user?.isLocked);
                      const isLockedUser = normalizedUserStatus === 'locked';
                      return (
                        <tr key={id || `${user.fullName || 'user'}-${user.email || ''}`}>
                          <td className="col-checkbox">
                            <input
                              type="checkbox"
                              checked={selectedUsers.has(id)}
                              onChange={() => handleSelectUser(user)}
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
                            {user.registerDate ? new Date(user.registerDate).toLocaleDateString() : '-'}
                          </td>
                          <td className="col-status">
                            <StatusBadge status={user.status} />
                          </td>
                          <td className="col-actions">
                            <div className="action-buttons">
                              <button className="action-btn" type="button" title={t('viewDetail')} aria-label={t('viewDetail')} onClick={() => handleViewDetails(user)}>
                                <Eye size={18} />
                              </button>
                              <button className="action-btn" type="button" title={t('assignRole')} aria-label={t('assignRole')} onClick={() => handleOpenRoleModal(user)}>
                                <UserCog size={18} />
                              </button>
                              <button
                                className="action-btn"
                                type="button"
                                title={isLockedUser ? t('unlockUser') : t('lockUser')}
                                aria-label={isLockedUser ? t('unlockUser') : t('lockUser')}
                                onClick={() => handleToggleLock(user)}
                              >
                                {isLockedUser ? <Unlock size={18} /> : <Lock size={18} />}
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>

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
                    <div className="pagination-desktop">
                      <button className="pagination-btn" type="button" disabled={currentPage === 1} onClick={() => setCurrentPage(1)} title={t('firstPage')}>
                        <ChevronsLeft size={18} />
                      </button>

                      <button className="pagination-btn" type="button" disabled={currentPage === 1} onClick={() => setCurrentPage((page) => Math.max(1, page - 1))} title={t('previous')}>
                        <ChevronLeft size={18} />
                      </button>

                      {pageButtons.map((button) => (
                        button === 'start-ellipsis' || button === 'end-ellipsis' ? (
                          <span key={button} className="pagination-ellipsis">…</span>
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

                      <button className="pagination-btn" type="button" disabled={currentPage === totalPages} onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))} title={t('next')}>
                        <ChevronRight size={18} />
                      </button>

                      <button className="pagination-btn" type="button" disabled={currentPage === totalPages} onClick={() => setCurrentPage(totalPages)} title={t('lastPage')}>
                        <ChevronsRight size={18} />
                      </button>
                    </div>

                    <div className="pagination-mobile">
                      <button className="pagination-btn" type="button" disabled={currentPage === 1} onClick={() => setCurrentPage((page) => page - 1)} title={t('previous')}>
                        <ChevronLeft size={18} />
                      </button>

                      <span className="current-page">{currentPage}</span>

                      <button className="pagination-btn" type="button" disabled={currentPage === totalPages} onClick={() => setCurrentPage((page) => page + 1)} title={t('next')}>
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
      <DetailModal />
      <EditRoleModal />
      <ActionConfirmModal />
    </div>
  );
}

export default UserManagementPage;