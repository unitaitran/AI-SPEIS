import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { NotificationBell } from './NotificationBell';
import { NotificationCenter } from './NotificationCenter';
import { formatRelativeTime, NotificationItem } from './NotificationItem';
import UserTopbar from '../../components/user/UserTopbar/UserTopbar';
import AdminTopbar from '../../components/admin/AdminTopbar/AdminTopbar';
import { NotificationProvider } from './NotificationProvider';
import { categoriesByRole, getActionLabel, getNotificationDestination, notificationTypeConfig } from './notificationConfig';
import { notificationService } from './notificationService';
import { getNotificationContent } from './notificationCopy';

jest.mock('./notificationService', () => ({
  notificationService: {
    getNotifications: jest.fn(),
    getUnreadCount: jest.fn(),
    markAsRead: jest.fn(),
    markAllAsRead: jest.fn(),
    archive: jest.fn(),
  },
}));

jest.mock('./notificationRealtime', () => ({
  createNotificationRealtimeConnection: () => ({
    state: 'Disconnected',
    start: () => Promise.resolve(),
    stop: () => Promise.resolve(),
    onclose: () => {},
  }),
}));

const userNotification = {
  id: 'n-1', recipientRole: 'USER', type: 'CV_PROCESSING_FAILED', category: 'PROFILE', severity: 'ERROR',
  title: 'CV processing failed', message: 'Please upload the document again.', entityType: 'CV', entityId: 'cv-1',
  actionUrl: '/user/cv-management', readStatus: 'UNREAD', actionStatus: 'ACTIVE', createdAt: new Date().toISOString(), expiresAt: null,
};

const adminNotification = { ...userNotification, id: 'n-2', recipientRole: 'ADMIN', type: 'AI_EVALUATION_FAILED', category: 'AI_EVALUATION' };

function renderBell() {
  localStorage.setItem('token', 'token');
  localStorage.setItem('user', JSON.stringify({ userId: 1, role: 'user' }));
  return render(<NotificationProvider><NotificationBell /></NotificationProvider>);
}

beforeEach(() => {
  localStorage.clear();
  jest.clearAllMocks();
  notificationService.getUnreadCount.mockResolvedValue({ count: 1 });
  notificationService.getNotifications.mockResolvedValue({ items: [userNotification, adminNotification], pageNumber: 1, pageSize: 8, totalItems: 2 });
  notificationService.markAsRead.mockResolvedValue(null);
  notificationService.markAllAsRead.mockResolvedValue(null);
  notificationService.archive.mockResolvedValue(null);
});

describe('notification type configuration', () => {
  test('exports bell-enabled topbars for both authenticated shells', () => {
    expect(UserTopbar).toBeDefined();
    expect(AdminTopbar).toBeDefined();
  });

  test('covers all 31 supported notification types', () => {
    expect(Object.keys(notificationTypeConfig)).toHaveLength(31);
  });

  test('does not expose admin categories to users', () => {
    expect(categoriesByRole.USER).toEqual(['INTERVIEW', 'FEEDBACK', 'PROFILE', 'SUBSCRIPTION']);
    expect(categoriesByRole.USER).not.toContain('SYSTEM');
    expect(categoriesByRole.ADMIN).toEqual(['AI_EVALUATION', 'SYSTEM', 'SUBSCRIPTION']);
  });

  test('does not resume an interrupted interview when it is expired', () => {
    expect(getNotificationDestination({ ...userNotification, type: 'INTERVIEW_SESSION_INTERRUPTED', actionStatus: 'EXPIRED' }, 'USER')).toBeNull();
  });

  test('sends a CV processing failure to CV management', () => {
    expect(getNotificationDestination(userNotification, 'USER')).toBe('/user/cv-management');
  });

  test('keeps an expired interview action on the status page', () => {
    const notification = { ...userNotification, type: 'INTERVIEW_SESSION_EXPIRED', actionStatus: 'EXPIRED', actionUrl: '/user/interview/room/active-session' };
    expect(getNotificationDestination(notification, 'USER')).toBe('/user/interview-history');
    expect(getActionLabel(notification, 'USER')).toBe('View interview status');
  });

  test('uses a safe fallback for unknown types and never crashes', () => {
    expect(getNotificationDestination({ ...userNotification, type: 'NEW_BACKEND_TYPE', actionUrl: '/user/profile' }, 'USER')).toBe('/user/profile');
    expect(getNotificationDestination({ ...userNotification, type: 'NEW_BACKEND_TYPE', actionUrl: 'https://example.com/unsafe' }, 'USER')).toBeNull();
  });

  test('maps an admin evaluation issue to the existing AI usage page', () => {
    expect(getNotificationDestination(adminNotification, 'ADMIN')).toBe('/admin/ai-usage');
  });

  test('maps a user feedback review notification to the admin feedback queue', () => {
    expect(getNotificationDestination({
      ...adminNotification,
      type: 'AI_EVALUATION_REQUIRES_REVIEW',
      actionUrl: null,
    }, 'ADMIN')).toBe('/admin/ai-feedback');
  });

  test('does not render a raw CV parser error', () => {
    render(<NotificationItem notification={{ ...userNotification, message: 'ParserException: raw CV content' }} role="USER" onOpen={jest.fn()} onArchive={jest.fn()} />);
    expect(screen.queryByText(/ParserException/)).not.toBeInTheDocument();
    expect(screen.getByText(/could not process your CV/i)).toBeInTheDocument();
  });

  test('treats legacy timezone-less notification timestamps as UTC', () => {
    const now = Date.UTC(2026, 7, 6, 10, 10, 0);
    expect(formatRelativeTime('2026-08-06T10:00:00', 'en', now)).toBe('10 minutes ago');
  });

  test('renders notification copy in the current application language', () => {
    const content = getNotificationContent({ type: 'INTERVIEW_ROUND_COMPLETED' }, { roundType: 'TECHNICAL' }, 'vi');
    expect(content.title).toMatch(/Kỹ thuật/);
    expect(content.message).toMatch(/hoàn thành/);
    expect(content.action).toBe('Xem tiến trình');
  });
});

describe('notification bell', () => {
  test('shows unread count, opens dropdown, and excludes another role', async () => {
    renderBell();
    await waitFor(() => expect(screen.getByRole('button', { name: /1 unread/i })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: /1 unread/i }));
    await waitFor(() => expect(screen.getByRole('dialog', { name: 'Notifications' })).toBeInTheDocument());
    expect(await screen.findByText('CV processing failed')).toBeInTheDocument();
    expect(screen.getAllByText('CV processing failed')).toHaveLength(1);
  });

  test('marks all notifications as read from the dropdown', async () => {
    renderBell();
    await waitFor(() => screen.getByRole('button', { name: /1 unread/i }));
    fireEvent.click(screen.getByRole('button', { name: /1 unread/i }));
    await screen.findByRole('dialog', { name: 'Notifications' });
    fireEvent.click(screen.getByRole('button', { name: /mark all as read/i }));
    await waitFor(() => expect(notificationService.markAllAsRead).toHaveBeenCalledTimes(1));
    expect(screen.getByRole('button', { name: 'Notifications' })).toBeInTheDocument();
  });
});

describe('notification center', () => {
  test('offers only user categories and renders a retry-safe list', async () => {
    localStorage.setItem('token', 'token');
    localStorage.setItem('user', JSON.stringify({ userId: 1, role: 'user' }));
    render(<NotificationProvider><NotificationCenter role="USER" /></NotificationProvider>);
    expect(await screen.findByRole('heading', { name: 'Notifications' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'INTERVIEW' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'SYSTEM' })).not.toBeInTheDocument();
  });
});
