import React, { useEffect } from 'react';
import { BellOff, CheckCheck, Inbox, Loader2, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { navigate } from '../../routes/navigation';
import { categoriesByRole, getNotificationDestination } from './notificationConfig';
import { NotificationItem } from './NotificationItem';
import { getLocalizedCategory, getNotificationUiCopy } from './notificationCopy';
import { useNotifications } from './NotificationProvider';
import './notifications.css';

function NotificationSkeleton() {
  return <div className="notification-skeleton-list" aria-label="Loading notifications">{[1, 2, 3, 4].map((item) => <div className="notification-skeleton" key={item}><span /><div><b /><i /><i /></div></div>)}</div>;
}

export function NotificationCenter({ role }) {
  const { i18n } = useTranslation();
  const text = getNotificationUiCopy(i18n.language);
  const {
    notifications, unreadCount, filters, currentPage, hasMore, isLoading, isLoadingMore,
    isMarkingAllRead, error, setFilters, loadNotifications, markAsRead, markAllAsRead, archive,
  } = useNotifications();

  useEffect(() => {
    loadNotifications({ page: 1, append: false });
  }, [filters, loadNotifications]);

  const openItem = async (notification) => {
    await markAsRead(notification.id, notification.readStatus);
    const destination = getNotificationDestination(notification, role);
    if (destination) navigate(destination);
  };
  const categoryOptions = categoriesByRole[role] || [];
  const emptyMessage = filters.readStatus === 'UNREAD'
    ? text.caughtUp
    : role === 'ADMIN'
      ? text.noAdmin
      : text.noUser;

  return (
    <section className="notification-center">
      <header className="notification-center__header">
        <div>
          <p className="notification-center__eyebrow">{text.center}</p>
          <h1>{text.notifications}</h1>
          <p>{unreadCount > 0 ? `${unreadCount} ${text.unread}${unreadCount === 1 || String(i18n.language).startsWith('vi') ? '' : 's'}` : text.caughtUp}</p>
        </div>
        <button type="button" className="notification-primary-button" onClick={markAllAsRead} disabled={unreadCount === 0 || isMarkingAllRead}>
          {isMarkingAllRead ? <Loader2 size={17} className="notification-spin" aria-hidden="true" /> : <CheckCheck size={18} aria-hidden="true" />}
          {text.markAllRead}
        </button>
      </header>

      <div className="notification-filter-card" aria-label="Notification filters">
        <div className="notification-filter-group">
          <span>{text.show}</span>
          {['ALL', 'UNREAD'].map((status) => <button key={status} type="button" className={filters.readStatus === status ? 'is-active' : ''} onClick={() => setFilters({ ...filters, readStatus: status })}>{status === 'ALL' ? text.all : text.unreadFilter}</button>)}
        </div>
        <label className="notification-category-filter">
          <span>{text.category}</span>
          <select value={filters.category} onChange={(event) => setFilters({ ...filters, category: event.target.value })}>
            <option value="ALL">{text.allCategories}</option>
            {categoryOptions.map((category) => <option value={category} key={category}>{getLocalizedCategory(category, i18n.language)}</option>)}
          </select>
        </label>
      </div>

      <div className="notification-list-card" aria-live="polite">
        {isLoading ? <NotificationSkeleton /> : error ? (
          <div className="notification-state notification-state--error" role="alert"><RefreshCw size={28} aria-hidden="true" /><h2>{text.loadFailed}</h2><p>{error}</p><button type="button" onClick={() => loadNotifications({ page: 1, append: false })}>{text.retry}</button></div>
        ) : notifications.length === 0 ? (
          <div className="notification-state"><BellOff size={32} aria-hidden="true" /><h2>{emptyMessage}</h2><p>{filters.readStatus === 'UNREAD' ? text.newUpdates : text.reviewHint}</p></div>
        ) : (
          <div className="notification-list">{notifications.map((notification) => <NotificationItem key={notification.id} notification={notification} role={role} onOpen={openItem} onArchive={archive} />)}</div>
        )}
      </div>

      {!isLoading && !error && hasMore && <div className="notification-load-more"><button type="button" onClick={() => loadNotifications({ page: currentPage + 1, append: true })} disabled={isLoadingMore}>{isLoadingMore ? <><Loader2 size={17} className="notification-spin" aria-hidden="true" /> {text.loading}</> : <><Inbox size={17} aria-hidden="true" /> {text.loadMore}</>}</button></div>}
    </section>
  );
}
