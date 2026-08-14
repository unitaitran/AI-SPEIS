export const InterviewMode = Object.freeze({
  PRACTICE: 'Practice',
  REAL: 'RealTest',
});

export function normalizeInterviewMode(mode) {
  if (!mode) return InterviewMode.PRACTICE;
  const str = String(mode).trim();
  if (str === 'RealTest' || str === 'Real' || str.toLowerCase() === 'real') {
    return InterviewMode.REAL;
  }
  return InterviewMode.PRACTICE;
}
