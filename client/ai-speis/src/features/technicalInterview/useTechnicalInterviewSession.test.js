import { act, renderHook, waitFor } from '@testing-library/react';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import useTechnicalInterviewSession, { TechnicalInterviewFlowStatus } from './useTechnicalInterviewSession';

jest.mock('../../services/technicalInterviewApi', () => ({
  __esModule: true,
  default: {
    initializeSession: jest.fn(),
    getSession: jest.fn(),
    getCurrentQuestion: jest.fn(),
    startSession: jest.fn(),
  },
}));

describe('useTechnicalInterviewSession', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    technicalInterviewApi.initializeSession.mockResolvedValue({ sessionId: 'session-1', status: 'CREATED' });
  });

  test('resumes an active session from backend session and current-question state', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      sessionId: 'session-1',
      sessionStatus: 'QUESTION_READY',
      lockedMainQuestions: [
        { mainQuestionIndex: 1, selectedQuestionId: 101 },
        { mainQuestionIndex: 2, selectedQuestionId: 102 },
        { mainQuestionIndex: 3, selectedQuestionId: 103 },
      ],
    });
    technicalInterviewApi.getCurrentQuestion.mockResolvedValue({
      attemptId: 'attempt-2',
      questionId: null,
      questionType: 'FOLLOW_UP',
      content: 'What changes at scale?',
      mainQuestionIndex: 2,
      totalMainQuestions: 3,
      subQuestionIndex: 1,
      requiredSubQuestionCount: 2,
      completedFollowUpCount: 0,
      sessionStatus: 'QUESTION_READY',
    });

    const { result } = renderHook(() => useTechnicalInterviewSession('session-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(technicalInterviewApi.initializeSession).toHaveBeenCalledWith('session-1');
    expect(technicalInterviewApi.getSession).toHaveBeenCalledWith('session-1');
    expect(technicalInterviewApi.getCurrentQuestion).toHaveBeenCalledWith('session-1');
    expect(result.current.currentQuestion).toMatchObject({
      attemptId: 'attempt-2',
      questionId: null,
      mainQuestionIndex: 2,
      completedSubQuestionCount: 0,
    });
    expect(result.current.session.lockedMainQuestions.map((item) => item.selectedQuestionId))
      .toEqual([101, 102, 103]);
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

  test('accepts a question-ready session from the Technical backend contract', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      sessionId: 17,
      status: 'QUESTION_READY',
    });
    technicalInterviewApi.getCurrentQuestion.mockResolvedValue({
      attemptId: 'attempt-17',
      questionType: 'MAIN',
      content: 'Explain the browser event loop.',
      mainQuestionIndex: 1,
      totalMainQuestions: 3,
    });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.error).toBeNull();
    expect(result.current.currentQuestion?.attemptId).toBe('attempt-17');
  });

  test('keeps a found session when only the current-question endpoint is unavailable', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      sessionId: 17,
      status: 'QUESTION_READY',
    });
    technicalInterviewApi.getCurrentQuestion.mockRejectedValue({ status: 404, code: 'SESSION_NOT_FOUND' });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.session?.sessionId).toBe(17);
    expect(result.current.error).toBeNull();
    expect(result.current.questionError).toMatchObject({ status: 404 });
  });

  test('uses the question returned by start and nextQuestion returned by answer submission', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({ sessionId: 17, status: 'CREATED' });
    technicalInterviewApi.startSession.mockResolvedValue({
      attemptId: 'attempt-main',
      questionType: 'MAIN',
      content: 'Explain dependency inversion.',
      sessionStatus: 'QUESTION_READY',
    });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.startSession();
    });
    expect(result.current.currentQuestion?.attemptId).toBe('attempt-main');
    expect(result.current.session?.status).toBe('QUESTION_READY');

    act(() => {
      result.current.applyAnswerResponse({
        sessionStatus: 'QUESTION_READY',
        nextQuestion: {
          attemptId: 'attempt-follow-up',
          questionType: 'FOLLOW_UP',
          content: 'Give a concrete example.',
          sessionStatus: 'QUESTION_READY',
        },
      });
    });
    expect(result.current.currentQuestion?.attemptId).toBe('attempt-follow-up');
  });

  test('reports question generation separately and prevents duplicate start requests', async () => {
    let resolveStart;
    technicalInterviewApi.getSession.mockResolvedValue({ sessionId: 17, status: 'CREATED' });
    technicalInterviewApi.startSession.mockImplementation(() => new Promise((resolve) => {
      resolveStart = resolve;
    }));

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let firstRequest;
    await act(async () => {
      firstRequest = result.current.startSession();
      await result.current.startSession();
    });
    expect(technicalInterviewApi.startSession).toHaveBeenCalledTimes(1);
    expect(technicalInterviewApi.startSession).toHaveBeenCalledWith(17, {
      signal: expect.any(AbortSignal),
    });
    expect(result.current.flowStatus).toBe(TechnicalInterviewFlowStatus.GENERATING_QUESTION);

    await act(async () => {
      resolveStart({
        attemptId: 'attempt-main',
        questionType: 'MAIN',
        content: 'Explain immutability.',
        sessionStatus: 'QUESTION_READY',
      });
      await firstRequest;
    });
    expect(result.current.flowStatus).toBe(TechnicalInterviewFlowStatus.QUESTION_READY);
  });

  test('resumes an evaluating session without requesting a ready question and keeps polling state', async () => {
    technicalInterviewApi.getSession.mockResolvedValue({
      sessionId: 17,
      status: 'EVALUATING',
      processingStatus: 'EVALUATING',
    });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.isProcessing).toBe(true);
    expect(result.current.currentQuestion).toBeNull();
    expect(technicalInterviewApi.getCurrentQuestion).not.toHaveBeenCalled();
  });

  test('reconciles a timed-out submit as accepted when backend is processing it', async () => {
    technicalInterviewApi.getSession
      .mockResolvedValueOnce({ sessionId: 17, status: 'QUESTION_READY' })
      .mockResolvedValueOnce({ sessionId: 17, status: 'EVALUATING' });
    technicalInterviewApi.getCurrentQuestion.mockResolvedValueOnce({
      attemptId: 'attempt-main',
      questionType: 'MAIN',
      content: 'Explain closures.',
      mainQuestionIndex: 1,
      totalMainQuestions: 3,
    });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let recovery;
    await act(async () => {
      result.current.markProcessing('attempt-main');
      recovery = await result.current.reconcileAfterSubmission('attempt-main');
    });

    expect(recovery).toEqual({ state: 'PROCESSING' });
    expect(technicalInterviewApi.getCurrentQuestion).toHaveBeenCalledTimes(1);
    expect(result.current.currentQuestion).toBeNull();
  });

  test('detects that a timed-out answer was accepted when the next attempt is already ready', async () => {
    technicalInterviewApi.getSession
      .mockResolvedValueOnce({ sessionId: 17, status: 'QUESTION_READY' })
      .mockResolvedValueOnce({ sessionId: 17, status: 'QUESTION_READY' });
    technicalInterviewApi.getCurrentQuestion
      .mockResolvedValueOnce({
        attemptId: 'attempt-main',
        questionType: 'MAIN',
        content: 'Explain closures.',
        mainQuestionIndex: 1,
        totalMainQuestions: 3,
      })
      .mockResolvedValueOnce({
        attemptId: 'attempt-follow-up',
        questionId: null,
        questionType: 'FOLLOW_UP',
        content: 'How do closures affect memory?',
        mainQuestionIndex: 1,
        totalMainQuestions: 3,
        subQuestionIndex: 1,
        requiredSubQuestionCount: 1,
      });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let recovery;
    await act(async () => {
      recovery = await result.current.reconcileAfterSubmission('attempt-main');
    });

    expect(recovery).toMatchObject({
      state: 'ACCEPTED_NEXT_QUESTION',
      currentQuestion: { attemptId: 'attempt-follow-up', questionId: null },
    });
  });
});
