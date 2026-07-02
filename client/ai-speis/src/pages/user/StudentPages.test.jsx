import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import DashboardPage from './DashboardPage';
import MyCVPage from './MyCVPage';
import QuestionsPage from './QuestionsPage';

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key, fallback) => fallback || key,
    i18n: { language: 'en', changeLanguage: vi.fn() },
  }),
}));

vi.mock('../../layouts/user/UserLayout', () => ({
  default: ({ children }) => <div>{children}</div>,
}));

vi.mock('../../services/CVService', () => ({
  default: {
    getMyCV: vi.fn(),
    getParsedData: vi.fn(),
    getParseStatus: vi.fn(),
    uploadCV: vi.fn(),
    triggerParse: vi.fn(),
    deleteCV: vi.fn(),
    confirmParsedData: vi.fn(),
  },
}));

const cvService = await import('../../services/CVService');

describe('student pages', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('token', 'fake-token');
    vi.clearAllMocks();
    cvService.default.getMyCV.mockResolvedValue(null);
  });

  it('renders the dashboard with the welcome copy', () => {
    render(<DashboardPage />);

    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.getByText(/chào buổi sáng/i)).toBeInTheDocument();
  });

  it('shows the empty CV state and supports upload selection', async () => {
    cvService.default.getMyCV.mockResolvedValue(null);
    const file = new File(['pdf'], 'resume.pdf', { type: 'application/pdf' });
    render(<MyCVPage />);

    await waitFor(() => expect(screen.getByText(/CV của tôi/i)).toBeInTheDocument());
    expect(screen.getByText(/Hãy tải lên CV của bạn/i)).toBeInTheDocument();
  });

  it('renders question filters and allows search', async () => {
    global.fetch = vi.fn()
      .mockResolvedValueOnce({ ok: true, json: async () => [{ questionId: 1, questionContent: 'Explain React Hooks', roleTarget: 'Frontend Developer', difficulty: 'Easy', suggestedAnswer: 'Use hooks carefully' }] })
      .mockResolvedValueOnce({ ok: true, json: async () => [] });

    render(<QuestionsPage />);

    expect(await screen.findByText(/Explain React Hooks/i)).toBeInTheDocument();

    const searchInput = screen.getByPlaceholderText(/tìm câu hỏi/i);
    await act(async () => {
      await userEvent.type(searchInput, 'Hooks');
    });
    expect(searchInput).toHaveValue('Hooks');
  });
});
