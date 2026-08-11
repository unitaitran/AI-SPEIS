import { act, renderHook, waitFor } from '@testing-library/react';
import interviewSessionService from '../../services/InterviewSessionService';
import technicalV2InterviewApi, { TechnicalV2InterviewError } from '../../services/technicalV2InterviewApi';
import useTechnicalInterviewSession from './useTechnicalInterviewSession';
import { TechnicalV2ErrorCode, TechnicalV2FlowPhase } from './technicalV2Interview.types';

jest.mock('../../services/InterviewSessionService', () => ({
  __esModule: true,
  default: {
    getSession: jest.fn(),
    getActiveCampaign: jest.fn(),
    getCampaign: jest.fn(),
  },
}));

jest.mock('../../services/technicalV2InterviewApi', () => ({
  __esModule: true,
  TechnicalV2InterviewError: class TechnicalV2InterviewError extends Error {
    constructor(message, options = {}) {
      super(message);
      this.name = 'TechnicalV2InterviewError';
      Object.assign(this, options);
    }
  },
  default: {
    initialize: jest.fn(),
    start: jest.fn(),
    getState: jest.fn(),
    getCurrentQuestion: jest.fn(),
    submitAnswer: jest.fn(),
    complete: jest.fn(),
    generateFeedback: jest.fn(),
    getResult: jest.fn(),
  },
}));

const session = {
  interviewSessionId: 17,
  interviewCampaignId: 7,
  interviewRoundType: 'Technical',
  status: 'Active',
};

const campaign = {
  interviewCampaignId: 7,
  status: 'Active',
  sessions: [session],
};

const question = {
  sessionQuestionId: 101,
  questionId: 201,
  questionType: 'Main',
  questionOrder: 1,
  totalMainQuestions: 2,
  content: 'Explain dependency inversion.',
  timeLimitSeconds: 120,
};

const state = (overrides = {}) => ({
  sessionId: 17,
  runtimeVersion: 'V2',
  targetMainQuestionCount: 2,
  completedMainQuestionCount: 0,
  sessionStatus: 'Active',
  questionSetStatus: 'Ready',
  evaluationStatus: 'NOT_STARTED',
  isComplete: false,
  currentQuestion: question,
  transcript: [],
  ...overrides,
});

describe('useTechnicalInterviewSession V2', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    interviewSessionService.getSession.mockResolvedValue(session);
    interviewSessionService.getActiveCampaign.mockResolvedValue(campaign);
    technicalV2InterviewApi.getState.mockResolvedValue(state());
  });

  test('loads the server-authoritative current question', async () => {
    const { result } = renderHook(() => useTechnicalInterviewSession(17));

    await waitFor(() => expect(result.current.phase).toBe(TechnicalV2FlowPhase.READY_TO_ANSWER));

    expect(technicalV2InterviewApi.getState).toHaveBeenCalledWith(17, { signal: expect.any(AbortSignal) });
    expect(technicalV2InterviewApi.start).not.toHaveBeenCalled();
    expect(result.current.currentQuestion).toEqual(question);
    expect(result.current.transcriptMessages[0].content).toBe(question.content);
  });

  test('initializes and starts an uninitialized V2 session', async () => {
    technicalV2InterviewApi.getState
      .mockRejectedValueOnce(new TechnicalV2InterviewError('not initialized', { code: TechnicalV2ErrorCode.NOT_INITIALIZED }))
      .mockResolvedValueOnce(state({ currentQuestion: null }));
    technicalV2InterviewApi.initialize.mockResolvedValue(state({ currentQuestion: null }));
    technicalV2InterviewApi.start.mockResolvedValue(question);

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.phase).toBe(TechnicalV2FlowPhase.READY_TO_ANSWER));

    expect(technicalV2InterviewApi.initialize).toHaveBeenCalledWith(17, undefined, { signal: expect.any(AbortSignal) });
    expect(technicalV2InterviewApi.start).toHaveBeenCalledWith(17, { signal: expect.any(AbortSignal) });
    expect(result.current.currentQuestion).toEqual(question);
  });

  test('opens a completed round through the V2 result endpoint', async () => {
    technicalV2InterviewApi.getState.mockResolvedValue(state({ isComplete: true, currentQuestion: null }));
    technicalV2InterviewApi.getResult.mockResolvedValue({ sessionId: 17, overallScore: 8.25, mainQuestions: [] });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.phase).toBe(TechnicalV2FlowPhase.COMPLETED));

    expect(technicalV2InterviewApi.getResult).toHaveBeenCalledWith(17, { signal: expect.any(AbortSignal) });
    expect(result.current.completionResult.overallScore).toBe(8.25);
  });

  test('reconciles a timed-out answer when the server has advanced', async () => {
    const nextQuestion = { ...question, sessionQuestionId: 102, questionId: 202, questionOrder: 2, content: 'Give a concrete example.' };
    technicalV2InterviewApi.submitAnswer.mockRejectedValue(new TechnicalV2InterviewError('timeout', { code: TechnicalV2ErrorCode.REQUEST_TIMEOUT }));
    technicalV2InterviewApi.getState
      .mockResolvedValueOnce(state())
      .mockResolvedValueOnce(state({ completedMainQuestionCount: 1, currentQuestion: nextQuestion }));

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.phase).toBe(TechnicalV2FlowPhase.READY_TO_ANSWER));

    let response;
    await act(async () => {
      response = await result.current.submitAnswer({ transcript: 'A valid answer' });
    });

    expect(response).toMatchObject({ accepted: true, reconciled: true });
    expect(result.current.currentQuestion.sessionQuestionId).toBe(102);
    expect(technicalV2InterviewApi.submitAnswer).toHaveBeenCalledWith(17, 101, { transcript: 'A valid answer' }, expect.objectContaining({ idempotencyKey: expect.any(String) }));
  });

  test('reuses the same idempotency key for a retry of the same question', async () => {
    technicalV2InterviewApi.submitAnswer.mockRejectedValue(new TechnicalV2InterviewError('network', { code: TechnicalV2ErrorCode.NETWORK_ERROR }));
    technicalV2InterviewApi.getState.mockResolvedValue(state());

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.phase).toBe(TechnicalV2FlowPhase.READY_TO_ANSWER));

    await act(async () => { await expect(result.current.submitAnswer({ transcript: 'Retryable answer' })).rejects.toBeTruthy(); });
    await act(async () => { await expect(result.current.submitAnswer({ transcript: 'Retryable answer' })).rejects.toBeTruthy(); });

    const firstKey = technicalV2InterviewApi.submitAnswer.mock.calls[0][3].idempotencyKey;
    const secondKey = technicalV2InterviewApi.submitAnswer.mock.calls[1][3].idempotencyKey;
    expect(firstKey).toBeTruthy();
    expect(secondKey).toBe(firstKey);
  });

  test('blocks a second active session with an explicit conflict phase', async () => {
    interviewSessionService.getActiveCampaign.mockResolvedValue({
      ...campaign,
      sessions: [session, { ...session, interviewSessionId: 18, status: 'Active' }],
    });

    const { result } = renderHook(() => useTechnicalInterviewSession(17));
    await waitFor(() => expect(result.current.phase).toBe(TechnicalV2FlowPhase.SESSION_CONFLICT));
    expect(result.current.conflict.sessionId).toBe(18);
    expect(technicalV2InterviewApi.getState).not.toHaveBeenCalled();
  });
});
