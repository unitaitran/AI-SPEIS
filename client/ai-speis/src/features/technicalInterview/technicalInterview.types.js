export const TechnicalSessionStatus = Object.freeze({
  CREATED: 'CREATED',
  SELECTING_QUESTION: 'SELECTING_QUESTION',
  QUESTION_READY: 'QUESTION_READY',
  ANSWERING: 'ANSWERING',
  EVALUATING: 'EVALUATING',
  COMPLETED: 'COMPLETED',
  FAILED: 'FAILED',
});

export const TechnicalQuestionType = Object.freeze({
  MAIN: 'MAIN',
  CLARIFICATION: 'CLARIFICATION',
  FOLLOW_UP: 'FOLLOW_UP',
});

export const TechnicalInterviewErrorCode = Object.freeze({
  SESSION_NOT_FOUND: 'SESSION_NOT_FOUND',
  SESSION_ACCESS_DENIED: 'SESSION_ACCESS_DENIED',
  INVALID_SESSION_STATE: 'INVALID_SESSION_STATE',
  QUESTION_NOT_READY: 'QUESTION_NOT_READY',
  ANSWER_ALREADY_SUBMITTED: 'ANSWER_ALREADY_SUBMITTED',
  AI_PROVIDER_TIMEOUT: 'AI_PROVIDER_TIMEOUT',
  AI_PROVIDER_UNAVAILABLE: 'AI_PROVIDER_UNAVAILABLE',
  RUBRIC_CONFIGURATION_ERROR: 'RUBRIC_CONFIGURATION_ERROR',
  EVALUATION_VALIDATION_FAILED: 'EVALUATION_VALIDATION_FAILED',
  TRANSCRIPT_REQUIRED: 'TRANSCRIPT_REQUIRED',
  SESSION_COMPLETED: 'SESSION_COMPLETED',
  TECHNICAL_SESSION_NOT_INITIALIZED: 'TECHNICAL_SESSION_NOT_INITIALIZED',
  NOT_TECHNICAL_SESSION: 'NOT_TECHNICAL_SESSION',
  UNSUPPORTED_JOB_ROLE: 'UNSUPPORTED_JOB_ROLE',
  NO_TECHNICAL_CANDIDATE: 'NO_TECHNICAL_CANDIDATE',
  INVALID_SELECTED_SKILLS: 'INVALID_SELECTED_SKILLS',
  SESSION_START_REJECTED: 'SESSION_START_REJECTED',
  INVALID_SESSION_STATUS: 'INVALID_SESSION_STATUS',
  NO_CURRENT_QUESTION: 'NO_CURRENT_QUESTION',
  INVALID_IDEMPOTENCY_KEY: 'INVALID_IDEMPOTENCY_KEY',
  INVALID_TRANSCRIPT: 'INVALID_TRANSCRIPT',
  ATTEMPT_NOT_IN_SESSION: 'ATTEMPT_NOT_IN_SESSION',
  INVALID_SUBMISSION_STATE: 'INVALID_SUBMISSION_STATE',
  IDEMPOTENCY_KEY_REUSED: 'IDEMPOTENCY_KEY_REUSED',
  CONCURRENT_SUBMISSION: 'CONCURRENT_SUBMISSION',
  DUPLICATE_SUBMISSION: 'DUPLICATE_SUBMISSION',
  SESSION_NOT_COMPLETED: 'SESSION_NOT_COMPLETED',
  MAIN_QUESTION_TARGET_NOT_REACHED: 'MAIN_QUESTION_TARGET_NOT_REACHED',
  QUESTION_STILL_PENDING: 'QUESTION_STILL_PENDING',
  SESSION_CONCURRENCY_CONFLICT: 'SESSION_CONCURRENCY_CONFLICT',
  NETWORK_ERROR: 'NETWORK_ERROR',
  UNKNOWN_ERROR: 'UNKNOWN_ERROR',
});

export const RecordingStatus = Object.freeze({
  IDLE: 'IDLE',
  REQUESTING_PERMISSION: 'REQUESTING_PERMISSION',
  RECORDING: 'RECORDING',
  PROCESSING: 'PROCESSING',
  READY: 'READY',
  ERROR: 'ERROR',
});

export const SttStatus = Object.freeze({
  IDLE: 'IDLE',
  PROCESSING: 'PROCESSING',
  COMPLETED: 'COMPLETED',
  FAILED: 'FAILED',
});

/**
 * Runtime code stays in JavaScript to match the existing CRA codebase. These
 * typedefs document the backend-owned contract without introducing UI scoring.
 *
 * @typedef {Object} TechnicalInterviewSession
 * @property {string|number} sessionId
 * @property {string} sessionStatus
 * @property {string=} jobRole
 * @property {string=} experienceLevel
 * @property {boolean=} canCompleteEarly
 */

/**
 * @typedef {Object} TechnicalQuestion
 * @property {string} attemptId
 * @property {string|null=} questionId
 * @property {'MAIN'|'CLARIFICATION'|'FOLLOW_UP'} questionType
 * @property {string} content
 * @property {string=} skill
 * @property {string=} difficulty
 * @property {number} mainQuestionIndex
 * @property {number} totalMainQuestions
 * @property {string} sessionStatus
 */

/**
 * @typedef {Object} TechnicalAnswerSubmission
 * @property {string} attemptId
 * @property {string} transcript
 * @property {string|null=} audioId
 */

/**
 * @typedef {Object} TechnicalInterviewResult
 * @property {number=} overallScore
 * @property {number=} maxScore
 * @property {string=} performanceBand
 * @property {boolean=} passed
 * @property {string=} rubricVersion
 * @property {string=} summaryFeedback
 * @property {Array<TechnicalDimensionResult>=} dimensionResults
 * @property {Array<TechnicalSkillResult>=} skillResults
 * @property {Array<TechnicalQuestionResult>=} questionResults
 * @property {Array<string>=} recommendations
 */

/** @typedef {Object} TechnicalRubricResult */
/** @typedef {Object} TechnicalDimensionResult */
/** @typedef {Object} TechnicalSkillResult */
/** @typedef {Object} TechnicalQuestionResult */
/** @typedef {Object} TechnicalAnswerResponse */
