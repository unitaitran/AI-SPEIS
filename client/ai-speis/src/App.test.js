import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ProfilePage from './pages/student/Profile/ProfilePage';

test('renders editable student profile fields', () => {
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
  userEvent.type(emailInput, 'student@example.com');
  userEvent.click(saveButton);

  expect(screen.getByText(/đã lưu thay đổi hồ sơ/i)).toBeInTheDocument();
  expect(saveButton).toBeDisabled();
});
