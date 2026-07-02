import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AppRoutes from './routes/AppRoutes';
import { getDefaultRouteForRole, getStoredSession } from './routes/auth';
import ProfilePage from './pages/user/Profile/ProfilePage';

beforeEach(() => {
  localStorage.clear();
  window.history.replaceState({}, '', '/');
});

test('maps authenticated roles to their own dashboard', () => {
  expect(getDefaultRouteForRole('user')).toBe('/user/dashboard');
  expect(getDefaultRouteForRole('ADMIN')).toBe('/admin/dashboard');
  expect(getDefaultRouteForRole('unknown')).toBe('/#login');
});

test('rejects incomplete or invalid stored sessions', () => {
  localStorage.setItem('token', 'token');
  expect(getStoredSession()).toBeNull();

  localStorage.setItem('user', JSON.stringify({ role: 'manager' }));
  expect(getStoredSession()).toBeNull();
});

test('redirects guests away from user profile', async () => {
  window.history.replaceState({}, '', '/user/profile');
  render(<AppRoutes />);

  await waitFor(() => {
    expect(window.location.pathname).toBe('/');
    //expect(window.location.hash).toBe('#login');
  });
});

test('redirects admins away from user profile', async () => {
  localStorage.setItem('token', 'admin-token');
  localStorage.setItem('user', JSON.stringify({ role: 'admin' }));
  window.history.replaceState({}, '', '/user/profile');
  render(<AppRoutes />);

  await waitFor(() => {
    expect(window.location.pathname).toBe('/admin/dashboard');
  });
});

test('allows users to open user profile', () => {
  localStorage.setItem('token', 'user-token');
  localStorage.setItem('user', JSON.stringify({ role: 'user' }));
  window.history.replaceState({}, '', '/user/profile');
  render(<AppRoutes />);

  expect(screen.getByRole('heading', { name: /hồ sơ cá nhân/i })).toBeInTheDocument();
  expect(window.location.pathname).toBe('/user/profile');
});

test('renders editable user profile fields', () => {
  render(<ProfilePage />);

  expect(screen.getByRole('heading', { name: /hồ sơ cá nhân/i })).toBeInTheDocument();
  expect(screen.getByDisplayValue('Nguyễn Minh Anh')).toBeInTheDocument();
  expect(screen.getByDisplayValue('Frontend Developer')).toBeInTheDocument();
  expect(screen.getByLabelText(/ngôn ngữ phỏng vấn ưu tiên/i)).toBeInTheDocument();
});

test('enables actions after editing and can cancel changes', () => {
  render(<ProfilePage />);

  const fullNameInput = screen.getByLabelText(/họ và tên/i);
  const saveButton = screen.getByRole('button', { name: /lưu thay đổi/i });
  const cancelButton = screen.getByRole('button', { name: /^hủy$/i });

  expect(saveButton).toBeDisabled();
  expect(cancelButton).toBeDisabled();

  userEvent.clear(fullNameInput);
  userEvent.type(fullNameInput, 'Trần Minh Anh');

  expect(saveButton).toBeEnabled();
  expect(cancelButton).toBeEnabled();

  userEvent.click(cancelButton);

  expect(fullNameInput).toHaveValue('Nguyễn Minh Anh');
  expect(saveButton).toBeDisabled();
});

test('validates email and saves valid profile changes', () => {
  render(<ProfilePage />);

  const emailInput = screen.getByLabelText(/^email$/i);
  const saveButton = screen.getByRole('button', { name: /lưu thay đổi/i });

  userEvent.clear(emailInput);
  userEvent.type(emailInput, 'invalid-email');
  userEvent.click(saveButton);

  expect(screen.getByText(/email chưa đúng định dạng/i)).toBeInTheDocument();

  userEvent.clear(emailInput);
  userEvent.type(emailInput, 'user@example.com');
  userEvent.click(saveButton);

  expect(screen.getByText(/đã lưu thay đổi hồ sơ/i)).toBeInTheDocument();
  expect(saveButton).toBeDisabled();
});
