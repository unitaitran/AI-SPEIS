import {
  AlertTriangle,
  Bell,
  Bot,
  CheckCircle2,
  CircleAlert,
  CreditCard,
  FileText,
  FileWarning,
  Info,
  MessageSquareText,
  RefreshCw,
  ServerCrash,
  ShieldAlert,
  UserRoundCog,
} from 'lucide-react';
import { AUTHENTICATED_ADMIN_ROUTES, USER_ROUTES } from '../../routes/routePaths';

const user = 'USER';
const admin = 'ADMIN';

/** Central configuration for all supported backend notification types. */
export const notificationTypeConfig = {
  INTERVIEW_SESSION_READY: { category: 'INTERVIEW', icon: Bell, severity: 'INFO', recipientRole: user, actionLabel: 'Start interview', destination: () => USER_ROUTES.INTERVIEW_SETUP, onlyActive: true },
  INTERVIEW_SESSION_INTERRUPTED: { category: 'INTERVIEW', icon: RefreshCw, severity: 'WARNING', recipientRole: user, actionLabel: 'Resume interview', destination: () => USER_ROUTES.INTERVIEW_SETUP, onlyActive: true },
  INTERVIEW_SESSION_EXPIRED: { category: 'INTERVIEW', icon: AlertTriangle, severity: 'WARNING', recipientRole: user, actionLabel: 'View interview status', destination: () => USER_ROUTES.INTERVIEW_HISTORY, useActionUrl: false },
  INTERVIEW_ROUND_COMPLETED: { category: 'INTERVIEW', icon: CheckCircle2, severity: 'SUCCESS', recipientRole: user, actionLabel: 'View progress', destination: () => USER_ROUTES.INTERVIEW_HISTORY },
  ALL_INTERVIEW_ROUNDS_COMPLETED: { category: 'INTERVIEW', icon: CheckCircle2, severity: 'SUCCESS', recipientRole: user, actionLabel: 'View interview summary', destination: () => USER_ROUTES.INTERVIEW_HISTORY },
  INTERVIEW_FEEDBACK_READY: { category: 'FEEDBACK', icon: MessageSquareText, severity: 'SUCCESS', recipientRole: user, actionLabel: 'View feedback', destination: () => USER_ROUTES.INTERVIEW_HISTORY },
  PROFILE_INFORMATION_REQUIRED: { category: 'PROFILE', icon: UserRoundCog, severity: 'WARNING', recipientRole: user, actionLabel: 'Update profile', destination: () => USER_ROUTES.PROFILE },
  CV_PROCESSING_FAILED: { category: 'PROFILE', icon: FileWarning, severity: 'ERROR', recipientRole: user, actionLabel: 'Upload CV again', destination: () => USER_ROUTES.CV },
  JD_INFORMATION_REQUIRED: { category: 'PROFILE', icon: FileText, severity: 'WARNING', recipientRole: user, actionLabel: 'Add job description', destination: () => USER_ROUTES.CV },
  JD_PROCESSING_FAILED: { category: 'PROFILE', icon: FileWarning, severity: 'ERROR', recipientRole: user, actionLabel: 'Update job description', destination: () => USER_ROUTES.CV },
  SUBSCRIPTION_ACTIVATED: { category: 'SUBSCRIPTION', icon: CheckCircle2, severity: 'SUCCESS', recipientRole: user, actionLabel: 'View subscription', destination: () => USER_ROUTES.PACKAGES },
  SUBSCRIPTION_EXPIRING_SOON: { category: 'SUBSCRIPTION', icon: AlertTriangle, severity: 'WARNING', recipientRole: user, actionLabel: 'Renew subscription', destination: () => USER_ROUTES.PACKAGES },
  SUBSCRIPTION_EXPIRED: { category: 'SUBSCRIPTION', icon: CreditCard, severity: 'WARNING', recipientRole: user, actionLabel: 'View plans', destination: () => USER_ROUTES.PACKAGES, useActionUrl: false },
  SUBSCRIPTION_PAYMENT_FAILED: { category: 'SUBSCRIPTION', icon: CircleAlert, severity: 'ERROR', recipientRole: user, actionLabel: 'Review payment', destination: () => USER_ROUTES.PACKAGES },
  SUBSCRIPTION_CANCELLED: { category: 'SUBSCRIPTION', icon: Info, severity: 'INFO', recipientRole: user, actionLabel: 'View subscription', destination: () => USER_ROUTES.PACKAGES },
  SUBSCRIPTION_PLAN_CHANGED: { category: 'SUBSCRIPTION', icon: CreditCard, severity: 'INFO', recipientRole: user, actionLabel: 'View subscription', destination: () => USER_ROUTES.PACKAGES },
  SUBSCRIPTION_USAGE_LIMIT_REACHED: { category: 'SUBSCRIPTION', icon: AlertTriangle, severity: 'WARNING', recipientRole: user, actionLabel: 'Upgrade plan', destination: () => USER_ROUTES.PACKAGES },
  AI_EVALUATION_REQUIRES_REVIEW: { category: 'AI_EVALUATION', icon: Bot, severity: 'WARNING', recipientRole: admin, actionLabel: 'Review evaluation', destination: () => AUTHENTICATED_ADMIN_ROUTES.AI_USAGE },
  AI_EVALUATION_FAILED: { category: 'AI_EVALUATION', icon: CircleAlert, severity: 'ERROR', recipientRole: admin, actionLabel: 'View evaluation issue', destination: () => AUTHENTICATED_ADMIN_ROUTES.AI_USAGE },
  FINAL_FEEDBACK_FAILED: { category: 'AI_EVALUATION', icon: RefreshCw, severity: 'ERROR', recipientRole: admin, actionLabel: 'Retry feedback', destination: () => AUTHENTICATED_ADMIN_ROUTES.AI_USAGE },
  SYSTEM_SERVICE_UNAVAILABLE: { category: 'SYSTEM', icon: ServerCrash, severity: 'CRITICAL', recipientRole: admin, actionLabel: 'View system status', destination: () => AUTHENTICATED_ADMIN_ROUTES.DASHBOARD },
  SUBSCRIPTION_PAYMENT_REQUIRES_REVIEW: { category: 'SUBSCRIPTION', icon: CreditCard, severity: 'WARNING', recipientRole: admin, actionLabel: 'Review payment', destination: () => AUTHENTICATED_ADMIN_ROUTES.PAYMENTS },
  SUBSCRIPTION_ACTIVATION_FAILED: { category: 'SUBSCRIPTION', icon: CircleAlert, severity: 'ERROR', recipientRole: admin, actionLabel: 'Review subscription', destination: () => AUTHENTICATED_ADMIN_ROUTES.SUBSCRIPTION },
  SUBSCRIPTION_DATA_INCONSISTENT: { category: 'SUBSCRIPTION', icon: ShieldAlert, severity: 'CRITICAL', recipientRole: admin, actionLabel: 'Review subscription', destination: () => AUTHENTICATED_ADMIN_ROUTES.SUBSCRIPTION },
};

export const categoriesByRole = {
  USER: ['INTERVIEW', 'FEEDBACK', 'PROFILE', 'SUBSCRIPTION'],
  ADMIN: ['AI_EVALUATION', 'SYSTEM', 'SUBSCRIPTION'],
};

const allowedPathPrefixes = [
  USER_ROUTES.DASHBOARD,
  USER_ROUTES.PROFILE,
  USER_ROUTES.PACKAGES,
  USER_ROUTES.CV,
  USER_ROUTES.INTERVIEW_HISTORY,
  USER_ROUTES.INTERVIEW_SETUP,
  USER_ROUTES.INTERVIEW_ROOM,
  USER_ROUTES.INTERVIEW_RESULT,
  USER_ROUTES.CAMPAIGN_RESULT,
  AUTHENTICATED_ADMIN_ROUTES.DASHBOARD,
  AUTHENTICATED_ADMIN_ROUTES.SUBSCRIPTION,
  AUTHENTICATED_ADMIN_ROUTES.PAYMENTS,
  AUTHENTICATED_ADMIN_ROUTES.AI_USAGE,
];

function isSafeActionUrl(value, role) {
  if (!value || typeof value !== 'string') return false;
  try {
    const url = new URL(value, window.location.origin);
    if (url.origin !== window.location.origin) return false;
    const expectedPrefix = role === admin ? '/admin' : '/user';
    return url.pathname.startsWith(expectedPrefix)
      && allowedPathPrefixes.some((prefix) => url.pathname === prefix || url.pathname.startsWith(`${prefix}/`));
  } catch {
    return false;
  }
}

export function getNotificationConfig(notification) {
  return notificationTypeConfig[notification?.type] || {
    category: notification?.category || 'SYSTEM',
    icon: Bell,
    severity: notification?.severity || 'INFO',
    recipientRole: notification?.recipientRole,
    actionLabel: null,
    destination: null,
  };
}

export function getNotificationDestination(notification, role) {
  const config = getNotificationConfig(notification);
  if (config.recipientRole && config.recipientRole !== role) return null;
  if (config.onlyActive && notification.actionStatus !== 'ACTIVE') return null;
  if (notification.actionStatus === 'CANCELLED') return null;
  if (notification.actionStatus === 'EXPIRED' && notification.type !== 'INTERVIEW_SESSION_EXPIRED') return null;
  if (config.useActionUrl !== false && isSafeActionUrl(notification.actionUrl, role)) {
    const safeUrl = new URL(notification.actionUrl, window.location.origin);
    return `${safeUrl.pathname}${safeUrl.search}`;
  }
  return config.destination ? config.destination(notification) : null;
}

export function getActionLabel(notification, role) {
  const config = getNotificationConfig(notification);
  return getNotificationDestination(notification, role) ? config.actionLabel : null;
}

export const serviceDisplayNames = {
  AI_MODEL: 'AI model',
  EXTERNAL_AI_API: 'External AI API',
  STT: 'Speech-to-text',
  TTS: 'Text-to-speech',
  RAG: 'Knowledge retrieval',
  CODING_JUDGE: 'Coding judge',
  BACKGROUND_JOB: 'Background job',
  NOTIFICATION_SERVICE: 'Notification service',
  PAYMENT_SERVICE: 'Payment service',
};
