const RESPONSE_ERROR = 'Kết quả phân tích từ máy chủ chưa đầy đủ. Vui lòng thử lại.';

/**
 * @typedef {Object} CvJdMatchViewModel
 * @property {number} score
 * @property {string} suitabilityLevel
 * @property {string[]} strengths
 * @property {string[]} missingSkills
 * @property {string} advice
 * @property {{ label: string, value: string }[]} additionalAnalysis
 */

const KNOWN_FIELDS = new Set([
  'success',
  'errorMessage',
  'matchScore',
  'suitabilityLevel',
  'matchingSkills',
  'missingSkills',
  'advice',
  'fastCheckResultId',
  'cvFileId',
  'jdFileId',
  'userId',
  'createdAt',
  'updatedAt',
  'isDeleted',
  'status',
  'matchPercentage',
  'id',
]);

const toUniqueStringList = (value) => {
  if (!Array.isArray(value)) throw new Error(RESPONSE_ERROR);

  return [...new Set(value
    .filter((item) => typeof item === 'string')
    .map((item) => item.trim())
    .filter(Boolean))];
};

const formatFieldLabel = (key) => key
  .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
  .replace(/[_-]+/g, ' ')
  .replace(/^./, (character) => character.toUpperCase());

const toDisplayValue = (value) => {
  if (typeof value === 'string') return value.trim();
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  if (Array.isArray(value) && value.every((item) => ['string', 'number', 'boolean'].includes(typeof item))) {
    return value.join(', ');
  }
  return '';
};

/**
 * Validate the backend DTO and adapt API field names to the UI model.
 * @param {unknown} response
 * @returns {CvJdMatchViewModel}
 */
export const mapCvJdMatchResponse = (response) => {
  if (!response || typeof response !== 'object' || Array.isArray(response)) {
    throw new Error(RESPONSE_ERROR);
  }

  if (response.success !== true) {
    throw new Error(
      typeof response.errorMessage === 'string' && response.errorMessage.trim()
        ? response.errorMessage
        : RESPONSE_ERROR,
    );
  }

  const score = response.matchScore;
  if (typeof score !== 'number' || !Number.isFinite(score) || score < 0 || score > 100) {
    throw new Error(RESPONSE_ERROR);
  }

  const additionalAnalysis = Object.entries(response)
    .filter(([key]) => !KNOWN_FIELDS.has(key))
    .map(([key, value]) => ({ label: formatFieldLabel(key), value: toDisplayValue(value) }))
    .filter((item) => item.value);

  return {
    score,
    suitabilityLevel: typeof response.suitabilityLevel === 'string'
      ? response.suitabilityLevel.trim()
      : '',
    strengths: toUniqueStringList(response.matchingSkills),
    missingSkills: toUniqueStringList(response.missingSkills),
    advice: typeof response.advice === 'string' ? response.advice.trim() : '',
    additionalAnalysis,
  };
};
