// Admin menu items based on AI-SPEIS SDS

export const ADMIN_MENU_ITEMS = [
  {
    id: 'dashboard',
    label: 'Overview',
    icon: 'LayoutDashboard',
    path: '/admin/dashboard',
  },
  {
    id: 'users',
    label: 'Users',
    icon: 'Users',
    path: '/admin/users',
  },
  {
    id: 'questions',
    label: 'Questions',
    icon: 'FileText',
    path: '/admin/questions',
  },
  {
    id: 'subscription',
    label: 'Subscription Packages',
    icon: 'Package',
    path: '/admin/subscription',
  },
  {
    id: 'payments',
    label: 'Payments',
    icon: 'Receipt',
    path: '/admin/payments',
  },
  {
    id: 'ai-usage',
    label: 'AI Usage',
    icon: 'Bot',
    path: '/admin/ai-usage',
  },
  {
    id: 'ai-feedback',
    label: 'AI Feedback',
    icon: 'Flag',
    path: '/admin/ai-feedback',
  },
];

export const ADMIN_ROUTES = {
  DASHBOARD: '/admin/dashboard',
  USERS: '/admin/users',
  ROLES: '/admin/roles',
  QUESTIONS: '/admin/questions',
  SUBSCRIPTION: '/admin/subscription',
  PAYMENTS: '/admin/payments',
  REWARDS: '/admin/rewards',
  COMMUNITY: '/admin/community',
  ANALYTICS: '/admin/analytics',
  AI_USAGE: '/admin/ai-usage',
  AI_FEEDBACK: '/admin/ai-feedback',
  REVENUE: '/admin/revenue',
  REFUNDS: '/admin/refunds',
  NOTIFICATIONS: '/admin/notifications',
};
