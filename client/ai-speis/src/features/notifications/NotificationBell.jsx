import React, { useEffect, useRef, useState } from 'react';
import { Bell } from 'lucide-react';
import { getStoredSession } from '../../routes/auth';
import { useNotifications } from './NotificationProvider';
import { NotificationDropdown } from './NotificationDropdown';
import './notifications.css';

function displayCount(count) {
  if (count > 99) return '99+';
  if (count > 9) return '9+';
  return String(count);
}

export function NotificationBell({ variant = 'user' }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const { unreadCount, loadRecent, refreshUnreadCount } = useNotifications();

  useEffect(() => {
    if (!open) return undefined;
    const closeOnOutsideClick = (event) => {
      if (rootRef.current && !rootRef.current.contains(event.target)) setOpen(false);
    };
    const closeOnEscape = (event) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('mousedown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [open]);

  const toggle = () => {
    if (!getStoredSession()) return;
    const nextOpen = !open;
    setOpen(nextOpen);
    if (nextOpen) {
      refreshUnreadCount();
      loadRecent();
    }
  };

  return (
    <div className={`notification-bell notification-bell--${variant}`} ref={rootRef}>
      <button type="button" className="notification-bell__button" onClick={toggle} aria-label={unreadCount ? `Notifications, ${unreadCount} unread` : 'Notifications'} aria-expanded={open} aria-haspopup="dialog">
        <Bell size={20} aria-hidden="true" />
        {unreadCount > 0 && <span className="notification-bell__badge" aria-hidden="true">{displayCount(unreadCount)}</span>}
        <span className="sr-only">{unreadCount ? `${unreadCount} unread notifications` : 'No unread notifications'}</span>
      </button>
      {open && <NotificationDropdown onClose={() => setOpen(false)} />}
    </div>
  );
}
