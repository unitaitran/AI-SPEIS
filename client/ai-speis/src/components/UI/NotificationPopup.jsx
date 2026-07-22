import React, { useCallback, useEffect, useRef, useState } from 'react';
import { AlertTriangle, CheckCircle2, CircleAlert, Info, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { NOTIFICATION_EVENT } from '../../utils/notification';
import './NotificationPopup.css';

const TYPE_CONFIG = {
  success: { icon: CheckCircle2, defaultTitleKey: 'notification.success_title' },
  info: { icon: Info, defaultTitleKey: 'notification.info_title' },
  warning: { icon: AlertTriangle, defaultTitleKey: 'notification.warning_title' },
  error: { icon: CircleAlert, defaultTitleKey: 'notification.error_title' },
};

function NotificationPopup() {
  const { t } = useTranslation('dashboard');
  const [notifications, setNotifications] = useState([]);
  const timersRef = useRef(new Map());

  const dismiss = useCallback((id) => {
    const timer = timersRef.current.get(id);
    if (timer) clearTimeout(timer);
    timersRef.current.delete(id);
    setNotifications((current) => current.filter((item) => item.id !== id));
  }, []);

  useEffect(() => {
    const timers = timersRef.current;
    const handleNotification = (event) => {
      const notification = event.detail;
      if (!notification?.id || !TYPE_CONFIG[notification.type]) return;

      setNotifications((current) => [...current, notification].slice(-4));
      if (notification.duration > 0) {
        const timer = setTimeout(() => dismiss(notification.id), notification.duration);
        timers.set(notification.id, timer);
      }
    };

    window.addEventListener(NOTIFICATION_EVENT, handleNotification);
    return () => {
      window.removeEventListener(NOTIFICATION_EVENT, handleNotification);
      timers.forEach((timer) => clearTimeout(timer));
      timers.clear();
    };
  }, [dismiss]);

  if (notifications.length === 0) return null;

  return (
    <div className="notification-popup-stack" aria-live="polite" aria-atomic="false">
      {notifications.map((notification) => {
        const config = TYPE_CONFIG[notification.type];
        const Icon = config.icon;
        const isAssertive = notification.type === 'error' || notification.type === 'warning';

        return (
          <article
            key={notification.id}
            className={`notification-popup notification-popup--${notification.type}`}
            role={isAssertive ? 'alert' : 'status'}
            style={{ '--notification-duration': `${notification.duration}ms` }}
          >
            <div className="notification-popup__icon" aria-hidden="true"><Icon size={20} /></div>
            <div className="notification-popup__content">
              <strong>{notification.title || t(config.defaultTitleKey)}</strong>
              <p>{notification.message}</p>
            </div>
            <button
              type="button"
              className="notification-popup__close"
              onClick={() => dismiss(notification.id)}
              aria-label={t('notification.close')}
            >
              <X size={17} />
            </button>
            {notification.duration > 0 && <span className="notification-popup__progress" aria-hidden="true" />}
          </article>
        );
      })}
    </div>
  );
}

export default NotificationPopup;

