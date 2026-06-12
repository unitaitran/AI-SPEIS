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
    id: 'roles',
    label: 'Roles',
    icon: 'Shield',
    path: '/admin/roles',
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
    id: 'rewards',
    label: 'Rewards',
    icon: 'Trophy',
    path: '/admin/rewards',
  },
  {
    id: 'community',
    label: 'Community',
    icon: 'MessageCircle',
    path: '/admin/community',
    hasBadge: true // Based on SDS: Yes — reported posts
  },
  {
    id: 'analytics',
    label: 'Analytics',
    icon: 'LineChart',
    path: '/admin/analytics',
  },
  {
    id: 'ai-usage',
    label: 'AI Usage',
    icon: 'Bot',
    path: '/admin/ai-usage',
  },
  {
    id: 'revenue',
    label: 'Revenue',
    icon: 'Wallet',
    path: '/admin/revenue',
  },
  {
    id: 'refunds',
    label: 'Refunds',
    icon: 'History',
    path: '/admin/refunds',
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
  REVENUE: '/admin/revenue',
  REFUNDS: '/admin/refunds',
};
