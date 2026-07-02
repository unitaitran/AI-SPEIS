import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import LoginPage from './LoginPage';
import RegisterPage from './RegisterPage';
import ForgotPasswordPage from './ForgotPasswordPage';
import ResetPasswordPage from './ResetPasswordPage';

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key, fallback) => fallback || key,
    i18n: { language: 'en', changeLanguage: vi.fn() },
  }),
}));

vi.mock('../../components/Auth/AuthCard', () => ({
  default: ({ children, footerText, mascotText }) => (
    <div>
      <h2>{mascotText}</h2>
      <div>{children}</div>
      <p>{footerText}</p>
    </div>
  ),
}));

vi.mock('../../components/Auth/LoginForm', () => ({
  default: () => (
    <form>
      <input aria-label="Email" />
      <button type="submit">Login</button>
    </form>
  ),
}));

vi.mock('../../components/Auth/RegisterForm', () => ({
  default: () => (
    <form>
      <input aria-label="Full name" />
      <button type="submit">Register</button>
    </form>
  ),
}));

vi.mock('../../components/UI/Input', () => ({
  default: ({ label, id, ...props }) => (
    <label>
      {label}
      <input aria-label={label} id={id} {...props} />
    </label>
  ),
}));

vi.mock('../../components/UI/Button', () => ({
  default: ({ children, ...props }) => <button type="button" {...props}>{children}</button>,
}));

describe('authentication pages', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/');
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('renders the login page and shows success feedback from the query string', async () => {
    window.history.replaceState({}, '', '/#login?status=success&message=Welcome');

    render(<LoginPage />);

    expect(screen.getByText('Welcome')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /login/i })).toBeInTheDocument();
  });

  it('renders the register page with the auth card shell', () => {
    render(<RegisterPage />);

    expect(screen.getByRole('button', { name: /register/i })).toBeInTheDocument();
    expect(screen.getByText(/mascot/i)).toBeInTheDocument();
  });

  it('submits the forgot password form and shows a success message', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ message: 'Reset link sent' }),
    });

    const user = userEvent.setup();
    render(<ForgotPasswordPage />);

    await user.type(screen.getByLabelText(/email/i), 'user@example.com');
    await user.click(screen.getByRole('button', { name: /forgot/i }));

    await waitFor(() => expect(screen.getByText('Reset link sent')).toBeInTheDocument());
  });

  it('submits the reset password form and shows success feedback', async () => {
    window.history.replaceState({}, '', '/#reset-password?token=test-token');
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ message: 'Password updated' }),
    });

    const user = userEvent.setup();
    render(<ResetPasswordPage />);

    const newPassword = screen.getByLabelText(/new_password_label/i);
    const confirmPassword = screen.getByLabelText(/confirm_password_label/i);

    await user.type(newPassword, 'Password123!');
    await user.type(confirmPassword, 'Password123!');
    await user.click(screen.getByRole('button', { name: /reset/i }));

    await waitFor(() => expect(screen.getByText(/Đặt lại mật khẩu thành công/i)).toBeInTheDocument());
  });
});
