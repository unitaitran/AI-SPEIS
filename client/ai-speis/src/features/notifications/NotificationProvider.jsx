import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { getStoredSession } from '../../routes/auth';
import { NAVIGATION_EVENT } from '../../routes/navigation';
import { notificationService } from './notificationService';
import { createNotificationRealtimeConnection } from './notificationRealtime';

export const NOTIFICATION_STATE_RESET_EVENT = 'ai-speis:notifications-reset';

const NotificationContext = createContext(null);
const initialFilters = { readStatus: 'ALL', category: 'ALL' };

function itemIsUnread(item) {
  return item?.readStatus === 'UNREAD';
}

function replaceItem(items, id, patch) {
  return items.map((item) => (String(item.id) === String(id) ? { ...item, ...patch } : item));
}

function removeItem(items, id) {
  return items.filter((item) => String(item.id) !== String(id));
}

function uniqueById(items) {
  return Array.from(new Map(items.map((item) => [String(item.id), item])).values());
}

function matchesFilters(item, filters) {
  return (!filters?.readStatus || filters.readStatus === 'ALL' || item.readStatus === filters.readStatus)
    && (!filters?.category || filters.category === 'ALL' || item.category === filters.category);
}

function clearState(setState) {
  setState({
    notifications: [],
    recentNotifications: [],
    unreadCount: 0,
    filters: initialFilters,
    currentPage: 1,
    hasMore: false,
    totalItems: 0,
    isLoading: false,
    isLoadingRecent: false,
    isLoadingMore: false,
    isMarkingAllRead: false,
    error: null,
  });
}

export function NotificationProvider({ children }) {
  const [state, setState] = useState({
    notifications: [],
    recentNotifications: [],
    unreadCount: 0,
    filters: initialFilters,
    currentPage: 1,
    hasMore: false,
    totalItems: 0,
    isLoading: false,
    isLoadingRecent: false,
    isLoadingMore: false,
    isMarkingAllRead: false,
    error: null,
  });
  const [sessionKey, setSessionKey] = useState(null);
  const sessionKeyRef = useRef(null);
  const requestRef = useRef(null);
  const recentRequestRef = useRef(null);
  const realtimeConnectionRef = useRef(null);

  const getSession = useCallback(() => getStoredSession(), []);
  const reset = useCallback(() => {
    requestRef.current?.abort();
    recentRequestRef.current?.abort();
    clearState(setState);
  }, []);

  const refreshUnreadCount = useCallback(async (signal) => {
    const session = getSession();
    if (!session) return 0;
    try {
      const payload = await notificationService.getUnreadCount(signal);
      if (getSession()?.token !== session.token) return 0;
      const count = Number(payload?.count ?? payload?.Count ?? 0);
      setState((current) => ({ ...current, unreadCount: Math.max(0, Number.isFinite(count) ? count : 0) }));
      return count;
    } catch (error) {
      if (error?.name !== 'AbortError' && error?.code === 'UNAUTHORIZED') reset();
      return 0;
    }
  }, [getSession, reset]);

  const loadRecent = useCallback(async () => {
    const session = getSession();
    if (!session) return [];
    recentRequestRef.current?.abort();
    const controller = new AbortController();
    recentRequestRef.current = controller;
    setState((current) => ({ ...current, isLoadingRecent: true }));
    try {
      const payload = await notificationService.getNotifications({}, 1, 8, controller.signal);
      if (recentRequestRef.current !== controller || getSession()?.token !== session.token) return [];
      const items = Array.isArray(payload?.items) ? payload.items : [];
      const role = String(session.user.role).toUpperCase();
      const scopedItems = items.filter((item) => String(item.recipientRole).toUpperCase() === role);
      setState((current) => ({ ...current, recentNotifications: uniqueById(scopedItems), isLoadingRecent: false }));
      return scopedItems;
    } catch (error) {
      if (error?.name === 'AbortError') return [];
      if (error?.code === 'UNAUTHORIZED') reset();
      else setState((current) => ({ ...current, isLoadingRecent: false }));
      return [];
    }
  }, [getSession, reset]);

  const loadNotifications = useCallback(async ({ page = 1, append = false } = {}) => {
    const session = getSession();
    if (!session) return;
    requestRef.current?.abort();
    const controller = new AbortController();
    requestRef.current = controller;
    setState((current) => ({
      ...current,
      isLoading: !append,
      isLoadingMore: append,
      error: null,
    }));
    try {
      const payload = await notificationService.getNotifications(state.filters, page, 20, controller.signal);
      if (requestRef.current !== controller || getSession()?.token !== session.token) return;
      const role = String(session.user.role).toUpperCase();
      const items = (Array.isArray(payload?.items) ? payload.items : [])
        .filter((item) => String(item.recipientRole).toUpperCase() === role);
      const pageNumber = Number(payload?.pageNumber ?? payload?.PageNumber ?? page);
      const pageSize = Number(payload?.pageSize ?? payload?.PageSize ?? 20);
      const totalItems = Number(payload?.totalItems ?? payload?.TotalItems ?? items.length);
      setState((current) => ({
        ...current,
        notifications: append ? uniqueById([...current.notifications, ...items]) : uniqueById(items),
        currentPage: pageNumber,
        totalItems,
        hasMore: pageNumber * pageSize < totalItems,
        isLoading: false,
        isLoadingMore: false,
      }));
    } catch (error) {
      if (error?.name === 'AbortError') return;
      if (error?.code === 'UNAUTHORIZED') {
        reset();
        return;
      }
      setState((current) => ({
        ...current,
        isLoading: false,
        isLoadingMore: false,
        error: 'We could not load notifications. Please try again.',
      }));
    }
  }, [getSession, reset, state.filters]);

  const setFilters = useCallback((nextFilters) => {
    setState((current) => ({
      ...current,
      filters: { ...initialFilters, ...nextFilters },
      currentPage: 1,
      hasMore: false,
    }));
  }, []);

  const markAsRead = useCallback(async (id, knownReadStatus) => {
    if (!getSession() || knownReadStatus !== 'UNREAD') return;
    const previousUnreadCount = state.unreadCount;
    setState((current) => ({
      ...current,
      notifications: replaceItem(current.notifications, id, { readStatus: 'READ' }),
      recentNotifications: replaceItem(current.recentNotifications, id, { readStatus: 'READ' }),
      unreadCount: Math.max(0, current.unreadCount - 1),
    }));
    try {
      await notificationService.markAsRead(id);
    } catch (error) {
      if (error?.code === 'UNAUTHORIZED') return reset();
      setState((current) => ({
        ...current,
        notifications: replaceItem(current.notifications, id, { readStatus: 'UNREAD' }),
        recentNotifications: replaceItem(current.recentNotifications, id, { readStatus: 'UNREAD' }),
        unreadCount: previousUnreadCount,
        error: 'We could not mark this notification as read. Please try again.',
      }));
    }
  }, [getSession, reset, state.unreadCount]);

  const markAllAsRead = useCallback(async () => {
    if (!getSession() || state.unreadCount === 0) return;
    const previous = state;
    setState((current) => ({
      ...current,
      notifications: current.notifications.map((item) => (itemIsUnread(item) ? { ...item, readStatus: 'READ' } : item)),
      recentNotifications: current.recentNotifications.map((item) => (itemIsUnread(item) ? { ...item, readStatus: 'READ' } : item)),
      unreadCount: 0,
      isMarkingAllRead: true,
    }));
    try {
      await notificationService.markAllAsRead();
    } catch (error) {
      if (error?.code === 'UNAUTHORIZED') return reset();
      setState((current) => ({ ...current, ...previous, isMarkingAllRead: false, error: 'We could not mark all notifications as read. Please try again.' }));
      return;
    }
    setState((current) => ({ ...current, isMarkingAllRead: false }));
  }, [getSession, reset, state]);

  const archive = useCallback(async (notification) => {
    if (!getSession() || !notification?.id) return;
    const previous = state;
    setState((current) => ({
      ...current,
      notifications: removeItem(current.notifications, notification.id),
      recentNotifications: removeItem(current.recentNotifications, notification.id),
      unreadCount: itemIsUnread(notification) ? Math.max(0, current.unreadCount - 1) : current.unreadCount,
      totalItems: Math.max(0, current.totalItems - 1),
    }));
    try {
      await notificationService.archive(notification.id);
    } catch (error) {
      if (error?.code === 'UNAUTHORIZED') return reset();
      setState((current) => ({ ...current, ...previous, error: 'We could not archive this notification. Please try again.' }));
    }
  }, [getSession, reset, state]);

  useEffect(() => {
    const syncSession = () => {
      const session = getSession();
      const nextKey = session ? `${session.user.userId ?? session.user.id}:${session.user.role}:${session.token}` : null;
      if (nextKey === sessionKeyRef.current) return;
      sessionKeyRef.current = nextKey;
      setSessionKey(nextKey);
      reset();
      if (session) refreshUnreadCount();
    };
    syncSession();
    window.addEventListener(NAVIGATION_EVENT, syncSession);
    window.addEventListener('storage', syncSession);
    const resetForLogout = () => {
      sessionKeyRef.current = null;
      setSessionKey(null);
      reset();
    };
    window.addEventListener(NOTIFICATION_STATE_RESET_EVENT, resetForLogout);
    return () => {
      requestRef.current?.abort();
      recentRequestRef.current?.abort();
      window.removeEventListener(NAVIGATION_EVENT, syncSession);
      window.removeEventListener('storage', syncSession);
      window.removeEventListener(NOTIFICATION_STATE_RESET_EVENT, resetForLogout);
    };
  }, [getSession, refreshUnreadCount, reset]);

  useEffect(() => {
    const session = getSession();
    if (!session) return undefined;
    const role = String(session.user.role).toUpperCase();
    const connection = createNotificationRealtimeConnection({
      onCreated: (payload) => {
        const notification = payload?.notification;
        if (!notification || String(notification.recipientRole).toUpperCase() !== role) return;
        const nextUnreadCount = Number(payload?.unreadCount);
        setState((current) => {
          const alreadyPresent = current.notifications.some((item) => String(item.id) === String(notification.id));
          const appliesToActiveFilters = matchesFilters(notification, current.filters);
          return {
            ...current,
            recentNotifications: uniqueById([notification, ...current.recentNotifications]).slice(0, 8),
            notifications: appliesToActiveFilters
              ? uniqueById([notification, ...current.notifications])
              : current.notifications,
            totalItems: appliesToActiveFilters && !alreadyPresent ? current.totalItems + 1 : current.totalItems,
            unreadCount: Number.isFinite(nextUnreadCount) ? Math.max(0, nextUnreadCount) : current.unreadCount,
          };
        });
      },
      onRead: (payload) => {
        const nextUnreadCount = Number(payload?.unreadCount);
        setState((current) => ({
          ...current,
          notifications: replaceItem(current.notifications, payload?.notificationId, { readStatus: 'READ', readAt: payload?.readAt }),
          recentNotifications: replaceItem(current.recentNotifications, payload?.notificationId, { readStatus: 'READ', readAt: payload?.readAt }),
          unreadCount: Number.isFinite(nextUnreadCount) ? Math.max(0, nextUnreadCount) : current.unreadCount,
        }));
      },
      onReadAll: (payload) => {
        setState((current) => ({
          ...current,
          notifications: current.notifications.map((item) => (itemIsUnread(item) ? { ...item, readStatus: 'READ' } : item)),
          recentNotifications: current.recentNotifications.map((item) => (itemIsUnread(item) ? { ...item, readStatus: 'READ' } : item)),
          unreadCount: Number.isFinite(Number(payload?.unreadCount)) ? Math.max(0, Number(payload.unreadCount)) : 0,
        }));
      },
      onArchived: (payload) => {
        const nextUnreadCount = Number(payload?.unreadCount);
        setState((current) => ({
          ...current,
          notifications: removeItem(current.notifications, payload?.notificationId),
          recentNotifications: removeItem(current.recentNotifications, payload?.notificationId),
          unreadCount: Number.isFinite(nextUnreadCount) ? Math.max(0, nextUnreadCount) : current.unreadCount,
          totalItems: Math.max(0, current.totalItems - 1),
        }));
      },
      onReconnected: () => {
        refreshUnreadCount();
        loadRecent();
      },
    });
    realtimeConnectionRef.current = connection;
    connection.start().catch(() => {
      // REST remains available if the realtime transport cannot connect.
    });

    return () => {
      if (realtimeConnectionRef.current === connection) realtimeConnectionRef.current = null;
      if (connection.state !== 'Disconnected') connection.stop().catch(() => {});
    };
  }, [getSession, loadRecent, refreshUnreadCount, sessionKey]);

  useEffect(() => {
    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible') refreshUnreadCount();
    };
    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => document.removeEventListener('visibilitychange', onVisibilityChange);
  }, [refreshUnreadCount]);

  const value = useMemo(() => ({
    ...state,
    refreshUnreadCount,
    loadRecent,
    loadNotifications,
    setFilters,
    markAsRead,
    markAllAsRead,
    archive,
    reset,
  }), [archive, loadNotifications, loadRecent, markAllAsRead, markAsRead, refreshUnreadCount, reset, setFilters, state]);

  return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>;
}

export function useNotifications() {
  const context = useContext(NotificationContext);
  if (!context) throw new Error('useNotifications must be used within NotificationProvider.');
  return context;
}
