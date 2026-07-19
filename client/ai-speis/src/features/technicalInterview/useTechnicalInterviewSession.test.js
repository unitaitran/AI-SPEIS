import { renderHook, waitFor } from '@testing-library/react';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import useTechnicalInterviewSession from './useTechnicalInterviewSession';

jest.mock('../../services/technicalInterviewApi', () => ({
  __esModule: true,
  default: {
    getSession: jest.fn(),
    getCurrentQuestion: jest.fn(),
    startSession: jest.fn(),
  },
}));

describe('useTechnicalInterviewSession', () => {
  beforeEach(() => jest.clearAllMocks());

  test('resumes an active session from backend session and current-question state', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      sessionId: 'session-1',
      sessionStatus: 'QUESTION_READY',
    });
    technicalInterviewApi.getCurrentQuestion.mockResolvedValue({
      attemptId: 'attempt-2',
      questionId: null,
      questionType: 'FOLLOW_UP',
      content: 'What changes at scale?',
      mainQuestionIndex: 2,
      totalMainQuestions: 5,
      sessionStatus: 'QUESTION_READY',
    });

    const { result } = renderHook(() => useTechnicalInterviewSession('session-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(technicalInterviewApi.getSession).toHaveBeenCalledWith('session-1');
    expect(technicalInterviewApi.getCurrentQuestion).toHaveBeenCalledWith('session-1');
    expect(result.current.currentQuestion).toMatchObject({
      attemptId: 'attempt-2',
      questionId: null,
      mainQuestionIndex: 2,
    });
  });

  test('does not request another question after backend marks the session completed', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      sessionId: 'session-1',
      sessionStatus: 'COMPLETED',
    });
    const { result } = renderHook(() => useTechnicalInterviewSession('session-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(technicalInterviewApi.getCurrentQuestion).not.toHaveBeenCalled();
    expect(result.current.currentQuestion).toBeNull();
  });

  test('accepts an Active session from the existing backend contract', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      interviewSessionId: 17,
      interviewRoundType: 'Technical',
      status: 'Active',
    });
    technicalInterviewApi.getCurrentQuestion.mockResolvedValue({
      attemptId: 'attempt-17',
      questionType: 'MAIN',
      content: 'Explain the browser event loop.',
      mainQuestionIndex: 1,
      totalMainQuestions: 5,
    });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.error).toBeNull();
    expect(result.current.currentQuestion?.attemptId).toBe('attempt-17');
  });

  test('keeps a found session when only the current-question endpoint is unavailable', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      interviewSessionId: 17,
      interviewRoundType: 'Technical',
      status: 'Active',
    });
    technicalInterviewApi.getCurrentQuestion.mockRejectedValue({ status: 404, code: 'SESSION_NOT_FOUND' });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.session?.interviewSessionId).toBe(17);
    expect(result.current.error).toBeNull();
    expect(result.current.questionError).toMatchObject({ status: 404 });
  });
});
