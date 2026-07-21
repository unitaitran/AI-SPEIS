import { act, renderHook } from '@testing-library/react';
import useTechnicalInterviewTranscript, {
  readTechnicalInterviewTranscript,
  TechnicalTranscriptItemStatus,
  TechnicalTranscriptRole,
  upsertTechnicalTranscriptItem,
} from './useTechnicalInterviewTranscript';

describe('technical interview transcript ledger', () => {
  afterEach(() => localStorage.clear());

  test('replaces a partial/draft item with its final item instead of duplicating it', () => {
    const draft = {
      id: 'attempt-1:answer',
      attemptId: 'attempt-1',
      role: TechnicalTranscriptRole.CANDIDATE,
      content: 'Partial answer',
      status: TechnicalTranscriptItemStatus.DRAFT,
    };
    const final = {
      ...draft,
      content: 'Final answer',
      status: TechnicalTranscriptItemStatus.FINAL,
    };

    const items = upsertTechnicalTranscriptItem(
      upsertTechnicalTranscriptItem([], draft),
      final,
    );

    expect(items).toHaveLength(1);
    expect(items[0]).toMatchObject({ content: 'Final answer', status: 'FINAL' });
  });

  test('persists chronological interviewer and candidate items for the session', () => {
    const { result } = renderHook(() => useTechnicalInterviewTranscript('session-1'));

    act(() => {
      result.current.syncQuestion({
        attemptId: 'attempt-1',
        content: 'Explain event delegation.',
        questionType: 'MAIN',
        mainQuestionIndex: 1,
      });
      result.current.syncCandidate(
        'attempt-1',
        'It uses event bubbling.',
        TechnicalTranscriptItemStatus.DRAFT,
      );
      result.current.markCandidateFinal('attempt-1', 'It uses event bubbling.');
    });

    const stored = readTechnicalInterviewTranscript('session-1');
    expect(stored).toHaveLength(2);
    expect(stored.map((item) => item.role)).toEqual(['INTERVIEWER', 'CANDIDATE']);
    expect(stored[1].status).toBe('FINAL');
  });
});
