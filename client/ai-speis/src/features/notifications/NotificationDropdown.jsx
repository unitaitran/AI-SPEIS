import React from 'react';
import { BellOff, CheckCheck, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { getStoredSession } from '../../routes/auth';
import { navigate } from '../../routes/navigation';
import { AUTHENTICATED_ADMIN_ROUTES, USER_ROUTES } from '../../routes/routePaths';
import { getNotificationDestination } from './notificationConfig';
import { NotificationItem } from './NotificationItem';
import { getNotificationUiCopy } from './notificationCopy';
import { useNotifications } from './NotificationProvider';

export function NotificationDropdown({ onClose }) {
  const { i18n } = useTranslation();
  const text = getNotificationUiCopy(i18n.language);
  const { recentNotifications, unreadCount, markAsRead, markAllAsRead, isMarkingAllRead, isLoadingRecent, archive } = useNotifications();
  const role = String(getStoredSession()?.user?.role || '').toUpperCase();
  const openItem = async (notification) => {
    await markAsRead(notification.id, notification.readStatus);
    const destination = getNotificationDestination(notification, role);
    if (destination) navigate(destination);
    onClose();
  };
  const viewAll = () => {
    navigate(role === 'ADMIN' ? AUTHENTICATED_ADMIN_ROUTES.NOTIFICATIONS : USER_ROUTES.NOTIFICATIONS);
    onClose();
  };

  return (
    <section className="notification-dropdown" role="dialog" aria-label={text.dialog}>
      <header className="notification-dropdown__header">
        <div><h2>{text.notifications}</h2>{unreadCount > 0 && <span>{unreadCount} {String(i18n.language).startsWith('vi') ? 'chưa đọc' : 'unread'}</span>}</div>
        <button type="button" className="notification-text-button" onClick={markAllAsRead} disabled={unreadCount === 0 || isMarkingAllRead}>
          {isMarkingAllRead ? <Loader2 size={15} className="notification-spin" aria-hidden="true" /> : <CheckCheck size={16} aria-hidden="true" />} {text.markAllRead}
        </button>
      </header>
      <div className="notification-dropdown__body">
        {isLoadingRecent ? (
          <div className="notification-dropdown__loading" aria-label={`${text.loading} ${text.notifications.toLowerCase()}`}><span /><span /><span /></div>
        ) : recentNotifications.length === 0 ? (
          <div className="notification-dropdown__empty"><BellOff size={24} aria-hidden="true" /><p>{role === 'ADMIN' ? text.noAdmin : text.noUser}</p></div>
        ) : recentNotifications.map((notification) => (
          <NotificationItem key={notification.id} notification={notification} role={role} compact onOpen={openItem} onArchive={archive} />
        ))}
      </div>
      <footer className="notification-dropdown__footer"><button type="button" onClick={viewAll}>{text.viewAll}</button></footer>
    </section>
  );
}
