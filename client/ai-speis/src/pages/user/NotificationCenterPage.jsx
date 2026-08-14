import React from 'react';
import UserLayout from '../../layouts/user/UserLayout';
import { NotificationCenter } from '../../features/notifications/NotificationCenter';

function NotificationCenterPage() {
  return <UserLayout><NotificationCenter role="USER" /></UserLayout>;
}

export default NotificationCenterPage;

