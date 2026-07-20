import { clearTechnicalInterviewDraft, readTechnicalInterviewDraft, saveTechnicalInterviewDraft } from './technicalInterviewDraft';

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
});

