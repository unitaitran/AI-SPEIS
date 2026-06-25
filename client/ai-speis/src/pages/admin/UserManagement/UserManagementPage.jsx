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
} from 'lucide-react';
import { userService } from '../../../services/UserService';
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

  const totalPages = Math.max(1, Math.ceil(totalUsers / pageSize));
  const startIndex = totalUsers === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endIndex = totalUsers === 0 ? 0 : Math.min(currentPage * pageSize, totalUsers);

  // Fetch users on mount and when pagination or sort changes
  useEffect(() => {
    fetchUsers();
  }, [currentPage, pageSize, sortBy, sortOrder]);

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
        ...filters,
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

  // Handle filter button
  const handleFilter = () => {
    setCurrentPage(1);
    fetchUsers();
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
      setSelectedUsers(new Set(users.map((u) => u.id)));
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
                <option value="student">{t('student')}</option>
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
            </div>

            <div className="filter-actions">
              <button
                className="btn-secondary"
                type="button"
                onClick={handleReset}
              >
                {t('reset')}
              </button>
              <button
                className="btn-primary"
                type="button"
                onClick={handleFilter}
              >
                {t('filter')}
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
                    {displayedUsers.map((user) => (
                      <tr key={user.id}>
                        <td className="col-checkbox">
                          <input
                            type="checkbox"
                            checked={selectedUsers.has(user.id)}
                            onChange={() => handleSelectUser(user.id)}
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
                          {user.registerDate
                            ? new Date(user.registerDate).toLocaleDateString()
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
                            >
                              <Eye size={18} />
                            </button>
                            <button
                              className="action-btn"
                              type="button"
                              title={t('assignRole')}
                              aria-label={t('assignRole')}
                            >
                              <UserCog size={18} />
                            </button>
                            <button
                              className="action-btn"
                              type="button"
                              title={t('lockUser')}
                              aria-label={t('lockUser')}
                            >
                              <Lock size={18} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
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
    </div>
  );
}

export default UserManagementPage;

