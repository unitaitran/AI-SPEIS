import { act, renderHook } from '@testing-library/react';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import useSubmitTechnicalAnswer from './useSubmitTechnicalAnswer';

jest.mock('../../services/technicalInterviewApi', () => ({
  __esModule: true,
  default: { submitAnswer: jest.fn() },
}));

describe('useSubmitTechnicalAnswer', () => {
  beforeEach(() => {
    technicalInterviewApi.submitAnswer.mockReset();
  });

  test('blocks duplicate clicks while the first submission is in flight', async () => {
    let resolveRequest;
    technicalInterviewApi.submitAnswer.mockReturnValue(new Promise((resolve) => {
      resolveRequest = resolve;
    }));
    const { result } = renderHook(() => useSubmitTechnicalAnswer('session-1'));
    const submission = { attemptId: 'attempt-1', transcript: 'answer' };
    let firstRequest;
    let duplicateRequest;

    await act(async () => {
      firstRequest = result.current.submitAnswer(submission);
      duplicateRequest = result.current.submitAnswer(submission);
      await Promise.resolve();
    });

    expect(technicalInterviewApi.submitAnswer).toHaveBeenCalledTimes(1);
    await expect(duplicateRequest).resolves.toBeNull();

    await act(async () => {
      resolveRequest({ sessionStatus: 'QUESTION_READY' });
      await firstRequest;
    });
  });

  test('reuses the same idempotency key when a failed request is retried', async () => {
    technicalInterviewApi.submitAnswer
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce({ sessionStatus: 'QUESTION_READY' });
    const { result } = renderHook(() => useSubmitTechnicalAnswer('session-1'));
    const submission = { attemptId: 'attempt-1', transcript: 'answer' };

    await act(async () => {
      await expect(result.current.submitAnswer(submission)).rejects.toThrow('network');
    });
    await act(async () => {
      await result.current.submitAnswer(submission);
    });

    const firstKey = technicalInterviewApi.submitAnswer.mock.calls[0][2].idempotencyKey;
    const retryKey = technicalInterviewApi.submitAnswer.mock.calls[1][2].idempotencyKey;
    expect(retryKey).toBe(firstKey);
  });
});
