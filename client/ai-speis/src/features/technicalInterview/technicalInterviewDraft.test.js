import {
  clearStaleTechnicalInterviewDrafts,
  clearTechnicalInterviewDraft,
  readTechnicalInterviewDraft,
  readTechnicalInterviewSessionDraft,
  saveTechnicalInterviewDraft,
} from './technicalInterviewDraft';

describe('technical interview draft storage', () => {
  afterEach(() => localStorage.clear());

  test('scopes a transcript draft to sessionId and attemptId, then clears it after success', () => {
    saveTechnicalInterviewDraft('session-1', 'attempt-1', 'first draft');
    saveTechnicalInterviewDraft('session-1', 'attempt-2', 'second draft');

    expect(readTechnicalInterviewDraft('session-1', 'attempt-1')).toBe('first draft');
    expect(readTechnicalInterviewDraft('session-1', 'attempt-2')).toBe('second draft');

    clearTechnicalInterviewDraft('session-1', 'attempt-1');
    expect(readTechnicalInterviewDraft('session-1', 'attempt-1')).toBe('');
    expect(readTechnicalInterviewDraft('session-1', 'attempt-2')).toBe('second draft');
  });

  test('recovers the pending transcript while a reloaded session is processing', () => {
    saveTechnicalInterviewDraft('session-1', 'attempt-processing', 'read-only pending transcript');

    expect(readTechnicalInterviewSessionDraft('session-1')).toEqual({
      attemptId: 'attempt-processing',
      transcript: 'read-only pending transcript',
    });
  });

  test('clears drafts from confirmed older attempts when the backend returns a new attempt', () => {
    saveTechnicalInterviewDraft('session-1', 'attempt-old', 'old draft');
    saveTechnicalInterviewDraft('session-1', 'attempt-current', 'current draft');

    clearStaleTechnicalInterviewDrafts('session-1', 'attempt-current');

    expect(readTechnicalInterviewDraft('session-1', 'attempt-old')).toBe('');
    expect(readTechnicalInterviewDraft('session-1', 'attempt-current')).toBe('current draft');
  });
});

