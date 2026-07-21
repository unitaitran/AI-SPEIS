export const BehavioralFlowPhase = Object.freeze({
  CHECKING_SESSION: 'checking-session',
  INITIALIZING: 'initializing',
  LOADING_QUESTION: 'loading-question',
  READY_TO_ANSWER: 'ready-to-answer',
  SUBMITTING_ANSWER: 'submitting-answer',
  EVALUATING_ANSWER: 'evaluating-answer',
  COMPLETING: 'completing',
  COMPLETED: 'completed',
  SESSION_CONFLICT: 'session-conflict',
  RECOVERABLE_ERROR: 'recoverable-error',
  FATAL_ERROR: 'fatal-error',
});

export const BehavioralQuestionType = Object.freeze({
  MAIN: 'Main',
  CLARIFICATION: 'Clarification',
  FOLLOW_UP_1: 'FollowUp1',
  FOLLOW_UP_2: 'FollowUp2',
});

export const BehavioralSessionStatus = Object.freeze({
  ACTIVE: 'Active',
  READY_TO_COMPLETE: 'ReadyToComplete',
  COMPLETED: 'Completed',
  FAILED: 'Failed',
  CANCELLED: 'Cancelled',
});

export const BehavioralErrorCode = Object.freeze({
  SESSION_NOT_FOUND: 'SESSION_NOT_FOUND',
  SESSION_ACCESS_DENIED: 'SESSION_ACCESS_DENIED',
  WRONG_ROUND_TYPE: 'WRONG_ROUND_TYPE',
  NOT_INITIALIZED: 'NOT_INITIALIZED',
  NOT_STARTED: 'NOT_STARTED',
  ROUND_COMPLETED: 'ROUND_COMPLETED',
  SESSION_EXPIRED: 'SESSION_EXPIRED',
  CAMPAIGN_CLOSED: 'CAMPAIGN_CLOSED',
  ALL_QUESTIONS_ANSWERED: 'ALL_QUESTIONS_ANSWERED',
  QUESTION_NOT_FOUND: 'QUESTION_NOT_FOUND',
  QUESTION_NOT_ACTIVE: 'QUESTION_NOT_ACTIVE',
  ALREADY_ANSWERED: 'ALREADY_ANSWERED',
  RESULT_NOT_READY: 'RESULT_NOT_READY',
  NETWORK_ERROR: 'NETWORK_ERROR',
  REQUEST_TIMEOUT: 'REQUEST_TIMEOUT',
  TRANSCRIPT_REQUIRED: 'TRANSCRIPT_REQUIRED',
  UNKNOWN_ERROR: 'UNKNOWN_ERROR',
});

/**
 * Runtime types stay in JavaScript to match the existing CRA application.
 * The shapes below document only fields returned by the backend contract.
 *
 * @typedef {Object} BehavioralQuestion
 * @property {number} sessionQuestionId
 * @property {number|null} questionId
 * @property {'Main'|'Clarification'|'FollowUp1'|'FollowUp2'} questionType
 * @property {number|null} parentSessionQuestionId
 * @property {string} content
 * @property {string|null} skill
 * @property {string|null} difficulty
 * @property {number} timeLimitSeconds
 * @property {string|null} hint
 * @property {number} mainQuestionIndex
 * @property {number} totalMainQuestions
 * @property {string} sessionStatus
 */

/**
 * @typedef {Object} BehavioralTranscriptMessage
 * @property {string} id
 * @property {'interviewer'|'candidate'} speaker
 * @property {string} content
 * @property {string=} questionType
 * @property {'submitted'|'current'} status
 * @property {string} createdAt
 */
