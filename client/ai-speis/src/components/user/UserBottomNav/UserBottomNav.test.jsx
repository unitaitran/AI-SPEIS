import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import UserBottomNav from './UserBottomNav';
import { USER_ROUTES } from '../../../routes/routePaths';
import * as navigationModule from '../../../routes/navigation';

jest.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key, def) => def,
    i18n: { language: 'vi' },
  }),
}));

describe('UserBottomNav', () => {
  beforeEach(() => {
    jest.spyOn(navigationModule, 'navigate').mockImplementation(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  test('renders 5 concise bottom navigation items', () => {
    render(<UserBottomNav />);

    expect(screen.getByLabelText('Trang chủ')).toBeInTheDocument();
    expect(screen.getByLabelText('CV/JD')).toBeInTheDocument();
    expect(screen.getByLabelText('Câu hỏi')).toBeInTheDocument();
    expect(screen.getByLabelText('Lịch sử')).toBeInTheDocument();
    expect(screen.getByLabelText('Service')).toBeInTheDocument();
  });

  test('calls navigate when an item is clicked', () => {
    render(<UserBottomNav />);

    fireEvent.click(screen.getByLabelText('CV/JD'));
    expect(navigationModule.navigate).toHaveBeenCalledWith(USER_ROUTES.CV);

    fireEvent.click(screen.getByLabelText('Service'));
    expect(navigationModule.navigate).toHaveBeenCalledWith(USER_ROUTES.PACKAGES);
  });

  test('only activates the current page and does not falsely activate dashboard on other pages', () => {
    // Simulate window.location.pathname as /user/packages
    delete window.location;
    window.location = new URL('http://localhost/user/packages');

    render(<UserBottomNav />);

    const dashboardLink = screen.getByLabelText('Trang chủ');
    const serviceLink = screen.getByLabelText('Service');

    expect(dashboardLink).toHaveAttribute('data-active', 'false');
    expect(serviceLink).toHaveAttribute('data-active', 'true');
  });

  test('respects onBeforeNavigate callback returning false', () => {
    const onBeforeNavigate = jest.fn().mockReturnValue(false);
    render(<UserBottomNav onBeforeNavigate={onBeforeNavigate} />);

    fireEvent.click(screen.getByLabelText('Câu hỏi'));
    expect(onBeforeNavigate).toHaveBeenCalledWith(USER_ROUTES.QUESTIONS);
    expect(navigationModule.navigate).not.toHaveBeenCalled();
  });
});
