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
  ANSWER_PROCESSING: 'ANSWER_PROCESSING',
  AI_PROVIDER_TIMEOUT: 'AI_PROVIDER_TIMEOUT',
  AI_PROVIDER_UNAVAILABLE: 'AI_PROVIDER_UNAVAILABLE',
  EVALUATION_FAILED: 'EVALUATION_FAILED',
  QUESTION_GENERATION_FAILED: 'QUESTION_GENERATION_FAILED',
  QUESTION_GENERATION_TIMEOUT: 'QUESTION_GENERATION_TIMEOUT',
  TTS_GENERATION_FAILED: 'TTS_GENERATION_FAILED',
  TTS_GENERATION_TIMEOUT: 'TTS_GENERATION_TIMEOUT',
  ACTIVE_CAMPAIGN_EXISTS: 'ACTIVE_CAMPAIGN_EXISTS',
  ACTIVE_INTERVIEW_SESSION_EXISTS: 'ACTIVE_INTERVIEW_SESSION_EXISTS',
  SESSION_ALREADY_ENDED: 'SESSION_ALREADY_ENDED',
  CAMPAIGN_ALREADY_CLOSED: 'CAMPAIGN_ALREADY_CLOSED',
  SESSION_VERSION_CONFLICT: 'SESSION_VERSION_CONFLICT',
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
  ROUND_LIFECYCLE_TRANSITION_FAILED: 'ROUND_LIFECYCLE_TRANSITION_FAILED',
  NETWORK_ERROR: 'NETWORK_ERROR',
  REQUEST_TIMEOUT: 'REQUEST_TIMEOUT',
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
 * @property {string=} language
 * @property {number=} targetMainQuestionCount
 * @property {number=} completedMainQuestionCount
 * @property {string=} status
 * @property {boolean=} canCompleteEarly
 * @property {Array<Object>=} transcript Server-authoritative question and submitted-answer history.
 */

/**
 * @typedef {Object} TechnicalInterviewProgress
 * @property {number} mainQuestionIndex
 * @property {number} totalMainQuestions
 * @property {number|null=} subQuestionIndex
 * @property {number=} requiredSubQuestionCount
 * @property {number=} completedSubQuestionCount
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
 * @property {number|null=} subQuestionIndex
 * @property {number=} requiredSubQuestionCount
 * @property {number=} completedSubQuestionCount
 * @property {TechnicalInterviewProgress=} progress
 */

/**
 * @typedef {Object} TechnicalAnswerSubmission
 * @property {string} attemptId
 * @property {string} transcript
 * @property {string|null=} audioId
 */

/**
 * @typedef {Object} TechnicalAnswerResponse
 * @property {string} attemptId
 * @property {string} sessionStatus
 * @property {string=} resolvedAction
 * @property {TechnicalInterviewProgress=} progress
 * @property {TechnicalQuestion|null=} nextQuestion
 * @property {{evaluation?: string, questionGeneration?: string}=} processing
 */

/**
 * @typedef {Object} TechnicalInterviewResult
 * @property {number=} technicalScore
 * @property {number=} overallScore
 * @property {number=} maxScore
 * @property {string=} performanceBand
 * @property {string=} rubricVersion
 * @property {string=} summaryFeedback
 * @property {Array<TechnicalDimensionResult>=} dimensionResults
 * @property {Array<TechnicalSkillResult>=} skillResults
 * @property {Array<TechnicalMainQuestionResult>=} questionResults
 * @property {Array<string>=} recommendations
 */

/**
 * @typedef {Object} TechnicalDimensionResult
 * @property {string} rubricCode
 * @property {string} name
 * @property {number} score
 * @property {number=} maxScore
 * @property {number=} weight
 * @property {string=} level
 * @property {Array<string>=} evidence
 * @property {Array<string>=} missingEvidence
 */

/**
 * @typedef {Object} TechnicalSubQuestionResult
 * @property {string} attemptId
 * @property {'CLARIFICATION'|'FOLLOW_UP'} questionType
 * @property {number=} sequenceWithinMain
 * @property {string} question
 * @property {string=} answerTranscript
 * @property {number|null=} rawScore
 * @property {number|null=} followUpBonus
 */

/**
 * @typedef {Object} TechnicalMainQuestionResult
 * @property {string} attemptId
 * @property {number} mainQuestionIndex
 * @property {string} question
 * @property {number} initialMainScore
 * @property {number} finalMainScore
 * @property {number=} cumulativeFollowUpBonus
 * @property {Array<TechnicalDimensionResult>=} dimensions
 * @property {Array<TechnicalSubQuestionResult>=} adaptiveHistory
 * @property {Array<string>=} strengths
 * @property {Array<string>=} missingPoints
 * @property {Array<string>=} improvementSuggestions
 */

/** @typedef {Object} TechnicalSkillResult */

/**
 * @typedef {Object} TechnicalInterviewError
 * @property {string} code
 * @property {number=} status
 * @property {unknown=} details
 */
