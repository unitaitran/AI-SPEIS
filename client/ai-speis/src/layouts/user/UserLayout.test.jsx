import React from 'react';
import { render, screen } from '@testing-library/react';
import UserLayout from './UserLayout';

jest.mock('../../components/user/UserSidebar/UserSidebar', () => ({ collapsed }) => (
  <aside data-testid="user-sidebar" data-collapsed={String(collapsed)} />
));
jest.mock('../../components/user/UserTopbar/UserTopbar', () => () => <header>Topbar</header>);
jest.mock('../../components/user/UserBottomNav/UserBottomNav', () => () => (
  <nav data-testid="user-bottom-nav">BottomNav</nav>
));
jest.mock('../../components/user/ProfileModal/ProfileModal', () => () => null);

describe('UserLayout', () => {
  test('renders the sidebar collapsed immediately and restores the default when the mode is removed', () => {
    const { rerender } = render(
      <UserLayout collapseSidebar immersive>
        <div>Interview room</div>
      </UserLayout>,
    );

    expect(screen.getByTestId('user-sidebar')).toHaveAttribute('data-collapsed', 'true');
    expect(screen.getByText('Interview room').parentElement).toHaveClass('h-full');
    expect(screen.queryByTestId('user-bottom-nav')).toBeNull();

    rerender(
      <UserLayout>
        <div>Dashboard</div>
      </UserLayout>,
    );

    expect(screen.getByTestId('user-sidebar')).toHaveAttribute('data-collapsed', 'false');
    expect(screen.getByText('Dashboard').parentElement).toHaveClass('max-w-[1200px]');
    expect(screen.getByTestId('user-bottom-nav')).toBeInTheDocument();
  });
});
