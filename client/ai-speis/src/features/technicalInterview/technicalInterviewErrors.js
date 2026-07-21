import { TechnicalInterviewErrorCode } from './technicalInterview.types';

const ERROR_CODE_ALIASES = Object.freeze({
  INVALID_USER_IDENTITY: TechnicalInterviewErrorCode.SESSION_ACCESS_DENIED,
  NO_CURRENT_QUESTION: TechnicalInterviewErrorCode.QUESTION_NOT_READY,
  CONCURRENT_SUBMISSION: TechnicalInterviewErrorCode.ANSWER_PROCESSING,
  DUPLICATE_SUBMISSION: TechnicalInterviewErrorCode.ANSWER_ALREADY_SUBMITTED,
  INVALID_SUBMISSION_STATE: TechnicalInterviewErrorCode.INVALID_SESSION_STATE,
  SESSION_CONCURRENCY_CONFLICT: TechnicalInterviewErrorCode.SESSION_VERSION_CONFLICT,
  AI_EVALUATION_FAILED: TechnicalInterviewErrorCode.EVALUATION_FAILED,
  EVALUATION_VALIDATION_FAILED: TechnicalInterviewErrorCode.EVALUATION_FAILED,
  NO_ACTIVE_NEXT_QUESTION: TechnicalInterviewErrorCode.QUESTION_GENERATION_FAILED,
  NEXT_QUESTION_BECAME_INACTIVE: TechnicalInterviewErrorCode.QUESTION_GENERATION_FAILED,
});

export class TechnicalInterviewError extends Error {
  constructor(message, { code, status, details } = {}) {
    super(message);
    this.name = 'TechnicalInterviewError';
    this.code = typeof code === 'string' && code.trim()
      ? code.trim()
      : TechnicalInterviewErrorCode.UNKNOWN_ERROR;
    this.status = status;
    this.details = details;
  }
}

export const getTechnicalInterviewErrorCode = (error) => {
  if (typeof error?.code === 'string' && error.code.trim()) {
    const code = error.code.trim();
    return ERROR_CODE_ALIASES[code] || code;
  }
  if (error instanceof TypeError) return TechnicalInterviewErrorCode.NETWORK_ERROR;
  return TechnicalInterviewErrorCode.UNKNOWN_ERROR;
};

export const getTechnicalInterviewErrorKey = (error) => (
  `errors.${getTechnicalInterviewErrorCode(error)}`
);
