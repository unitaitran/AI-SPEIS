import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AdminDashboardPage from './Dashboard/AdminDashboardPage';
import UserManagementPage from './UserManagement/UserManagementPage';
import QuestionManagementPage from './QuestionManagement/QuestionManagementPage';

vi.mock('../../services/UserService', () => ({
  userService: {
    getUsers: vi.fn().mockResolvedValue({ items: [{ userId: 1, fullName: 'Ada', email: 'ada@example.com', role: 'user', package: 'free', quota: 5, registerDate: '2024-01-01', status: 'active' }], total: 1 }),
    getUserById: vi.fn().mockResolvedValue({ fullName: 'Ada', email: 'ada@example.com', role: 'user', package: 'free', quota: 5, createdAt: '2024-01-01', status: 'active' }),
    lockUser: vi.fn().mockResolvedValue({}),
    unlockUser: vi.fn().mockResolvedValue({}),
    assignRole: vi.fn().mockResolvedValue({}),
    assignPackage: vi.fn().mockRejectedValue(new Error('not implemented')),
  },
}));

vi.mock('../../services/QuestionService', () => ({
  questionService: {
    getAdminQuestions: vi.fn().mockResolvedValue({ items: [{ questionId: '1', questionContent: 'Explain hooks', roleTarget: 'Frontend Developer', major: 'React', difficulty: 'Medium' }], totalItems: 1, totalPages: 1 }),
    getAdminQuestionFilters: vi.fn().mockResolvedValue({ majors: [], roleTargets: [] }),
  },
}));

describe('admin pages', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('renders the admin dashboard shell', () => {
    render(<AdminDashboardPage />);

    expect(screen.getByRole('heading', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /export report/i })).toBeInTheDocument();
  });

  it('renders the user management page, filters, and modal interactions', async () => {
    const user = userEvent.setup();
    render(<UserManagementPage />);

    expect(await screen.findByText(/ada@example.com/i)).toBeInTheDocument();
    await user.type(screen.getByRole('textbox'), 'Ada');
    expect(screen.getByText(/ada@example.com/i)).toBeInTheDocument();
  });

  it('renders the question management page and supports filtering', async () => {
    const user = userEvent.setup();
    render(<QuestionManagementPage />);

    expect(await screen.findByText(/Explain hooks/i)).toBeInTheDocument();
    await user.type(screen.getByRole('textbox'), 'hooks');
    expect(screen.getByText(/Explain hooks/i)).toBeInTheDocument();
  });
});
