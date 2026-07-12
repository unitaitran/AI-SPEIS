export const NOTIFICATION_EVENT = 'ai-speis:notification';

const DEFAULT_DURATION = {
  success: 3500,
  info: 4000,
  warning: 5000,
  error: 6000,
};

let sequence = 0;

const emitNotification = (type, message, options = {}) => {
  if (typeof window === 'undefined' || !message) return;

  sequence += 1;
  window.dispatchEvent(new CustomEvent(NOTIFICATION_EVENT, {
    detail: {
      id: `${Date.now()}-${sequence}`,
      type,
      message: String(message),
      title: options.title || '',
      duration: options.duration ?? DEFAULT_DURATION[type],
    },
  }));
};

const notify = {
  success: (message, options) => emitNotification('success', message, options),
  info: (message, options) => emitNotification('info', message, options),
  warning: (message, options) => emitNotification('warning', message, options),
  error: (message, options) => emitNotification('error', message, options),
};

export default notify;

