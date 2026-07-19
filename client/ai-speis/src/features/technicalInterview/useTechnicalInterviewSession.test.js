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
});

