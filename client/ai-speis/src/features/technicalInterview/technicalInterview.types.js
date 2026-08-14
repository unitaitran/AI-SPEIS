export const TechnicalQuestionType = Object.freeze({
  MAIN: 'MAIN',
  CLARIFICATION: 'CLARIFICATION',
  FOLLOW_UP: 'FOLLOW_UP',
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
