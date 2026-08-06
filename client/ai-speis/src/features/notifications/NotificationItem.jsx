import React, { useEffect, useState } from 'react';
import { Archive, ChevronRight, Clock3 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import '../../i18n';
import { getNotificationConfig, getNotificationDestination } from './notificationConfig';
import { getLocalizedServiceName, getLocalizedStatus, getNotificationContent } from './notificationCopy';

function parseMetadata(value) {
  if (!value) return {};
  if (typeof value === 'object') return value;
  try { return JSON.parse(value); } catch { return {}; }
}

function parseUtcDate(value) {
  if (!value) return null;
  const source = String(value);
  const hasTimezone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(source);
  const date = new Date(hasTimezone ? source : `${source}Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatRelativeTime(value, language = 'en', now = Date.now()) {
  const date = parseUtcDate(value);
  if (!date) return '';
  const seconds = Math.floor((now - date.getTime()) / 1000);
  if (seconds < 10) return String(language).startsWith('vi') ? 'Vừa xong' : 'Just now';
  const locale = String(language).startsWith('vi') ? 'vi' : 'en';
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  if (seconds < 60) return formatter.format(-seconds, 'second');
  if (seconds < 3600) return formatter.format(-Math.floor(seconds / 60), 'minute');
  if (seconds < 86400) return formatter.format(-Math.floor(seconds / 3600), 'hour');
  if (seconds < 604800) return formatter.format(-Math.floor(seconds / 86400), 'day');
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}

function formatDate(value, language) {
  const date = parseUtcDate(value);
  if (!date) return null;
  return new Intl.DateTimeFormat(String(language).startsWith('vi') ? 'vi-VN' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}

export function NotificationItem({ notification, role, onOpen, onArchive, compact = false }) {
  const { i18n } = useTranslation();
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const interval = window.setInterval(() => setNow(Date.now()), 30000);
    return () => window.clearInterval(interval);
  }, []);
  const config = getNotificationConfig(notification);
  const Icon = config.icon;
  const metadata = parseMetadata(notification.metadata);
  const content = getNotificationContent(notification, metadata, i18n.language);
  const actionLabel = getNotificationDestination(notification, role) ? content.action : null;
  const service = getLocalizedServiceName(metadata.service || metadata.serviceType || metadata.serviceCode, i18n.language);
  const expiryDate = notification.type === 'SUBSCRIPTION_EXPIRING_SOON' ? formatDate(metadata.expiryDate || notification.expiresAt, i18n.language) : null;
  const unread = notification.readStatus === 'UNREAD';
  const status = getLocalizedStatus(notification.actionStatus, i18n.language);

  return (
    <article className={`notification-item notification-item--${String(notification.severity || config.severity).toLowerCase()} ${unread ? 'is-unread' : ''} ${compact ? 'notification-item--compact' : ''}`}>
      <span className="notification-item__unread" aria-label={unread ? (String(i18n.language).startsWith('vi') ? 'Thông báo chưa đọc' : 'Unread notification') : undefined} />
      <div className="notification-item__icon" aria-hidden="true"><Icon size={20} /></div>
      <div className="notification-item__content">
        <div className="notification-item__heading">
          <strong>{content.title}</strong>
          <time dateTime={notification.createdAt} title={formatDate(notification.createdAt, i18n.language)}>{formatRelativeTime(notification.createdAt, i18n.language, now)}</time>
        </div>
        <p>{content.message}</p>
        {(status || service || expiryDate) && (
          <div className="notification-item__meta">
            {status && <span className={`notification-status notification-status--${String(notification.actionStatus).toLowerCase()}`}>{status}</span>}
            {service && <span>{String(i18n.language).startsWith('vi') ? 'Dịch vụ' : 'Service'}: {service}</span>}
            {expiryDate && <span>{String(i18n.language).startsWith('vi') ? 'Hết hạn' : 'Expires'} {expiryDate}</span>}
          </div>
        )}
        {!compact && (
          <div className="notification-item__actions">
            {actionLabel && <button type="button" className="notification-link" onClick={() => onOpen(notification)}>{actionLabel}<ChevronRight size={16} aria-hidden="true" /></button>}
            <button type="button" className="notification-icon-action" onClick={() => onArchive(notification)} aria-label={String(i18n.language).startsWith('vi') ? `Lưu trữ ${content.title}` : `Archive ${content.title}`} title={String(i18n.language).startsWith('vi') ? 'Lưu trữ thông báo' : 'Archive notification'}><Archive size={16} /></button>
          </div>
        )}
      </div>
      {compact && (
        <button type="button" className="notification-item__open" onClick={() => onOpen(notification)} aria-label={String(i18n.language).startsWith('vi') ? `Mở ${content.title}` : `Open ${content.title}`}>
          {actionLabel ? <ChevronRight size={18} aria-hidden="true" /> : <Clock3 size={16} aria-hidden="true" />}
        </button>
      )}
    </article>
  );
}
