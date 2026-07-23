/**
 * Shared Question Difficulty Enum & Mapping Helper
 * Matches DB schema: 0 = Easy, 1 = Medium, 2 = Hard, 3 = Expert
 */

export const QUESTION_DIFFICULTY_ENUM = Object.freeze({
  EASY: 0,
  MEDIUM: 1,
  HARD: 2,
  EXPERT: 3,
});

export const DIFFICULTY_KEYS = Object.freeze([
  'Easy',
  'Medium',
  'Hard',
  'Expert',
]);

/**
 * Normalizes difficulty input (number 0-3, string "0"-"3", or string "Easy"/"Medium"/"Hard"/"Expert")
 * to its standard numeric value (0, 1, 2, 3).
 */
export const normalizeDifficulty = (value) => {
  if (value === 0 || value === '0' || String(value).toLowerCase() === 'easy') return 0;
  if (value === 1 || value === '1' || String(value).toLowerCase() === 'medium') return 1;
  if (value === 2 || value === '2' || String(value).toLowerCase() === 'hard') return 2;
  if (value === 3 || value === '3' || String(value).toLowerCase() === 'expert') return 3;
  return null;
};

/**
 * Converts difficulty value to key string ('Easy', 'Medium', 'Hard', 'Expert').
 */
export const getDifficultyKey = (value) => {
  const norm = normalizeDifficulty(value);
  if (norm !== null) return DIFFICULTY_KEYS[norm];
  return String(value ?? '');
};

/**
 * Gets localized text label for display.
 */
export const getDifficultyLabel = (value, t = null) => {
  const norm = normalizeDifficulty(value);
  if (norm === 0) return t ? t('questions.difficulty_easy', 'Dễ') : 'Easy';
  if (norm === 1) return t ? t('questions.difficulty_medium', 'Trung bình') : 'Medium';
  if (norm === 2) return t ? t('questions.difficulty_hard', 'Khó') : 'Hard';
  if (norm === 3) return t ? t('questions.difficulty_expert', 'Chuyên gia') : 'Expert';
  return String(value ?? '');
};

/**
 * Gets CSS badge styling classes for difficulty badges.
 */
export const getDifficultyBadgeClass = (value) => {
  const norm = normalizeDifficulty(value);
  if (norm === 0) return 'bg-success-light/35 text-success border-success/20';
  if (norm === 1) return 'bg-warning-light/35 text-warning border-warning/20';
  if (norm === 2) return 'bg-error-light/35 text-error border-error/20';
  if (norm === 3) return 'bg-purple-500/20 text-purple-600 border-purple-500/30';
  return 'bg-surface-3 text-text-secondary border-border';
};
