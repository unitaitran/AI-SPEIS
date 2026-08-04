import {
  getSubscriberCount,
  normalizeMonitoring,
} from './SubscriptionManagementPage';

test('returns the subscriber count belonging to each plan', () => {
  const monitoring = normalizeMonitoring({
    activePremiumUsers: 0,
    planSubscriberCounts: [
      { planId: 1, subscriberCount: 3 },
      { planId: 2, subscriberCount: 1 },
    ],
  });

  expect(getSubscriberCount({ planId: 1, isFree: true }, monitoring)).toBe(3);
  expect(getSubscriberCount({ planId: 2, isFree: false }, monitoring)).toBe(1);
  expect(getSubscriberCount({ planId: 3 }, monitoring)).toBe(0);
});
