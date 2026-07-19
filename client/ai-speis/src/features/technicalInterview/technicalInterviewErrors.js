import { TechnicalInterviewErrorCode } from './technicalInterview.types';

const KNOWN_ERROR_CODES = new Set(Object.values(TechnicalInterviewErrorCode));

export class TechnicalInterviewError extends Error {
  constructor(message, { code, status, details } = {}) {
    super(message);
    this.name = 'TechnicalInterviewError';
    this.code = KNOWN_ERROR_CODES.has(code) ? code : TechnicalInterviewErrorCode.UNKNOWN_ERROR;
    this.status = status;
    this.details = details;
  }
}

export const getTechnicalInterviewErrorCode = (error) => {
  if (error?.code && KNOWN_ERROR_CODES.has(error.code)) return error.code;
  if (error instanceof TypeError) return TechnicalInterviewErrorCode.NETWORK_ERROR;
  return TechnicalInterviewErrorCode.UNKNOWN_ERROR;
};

export const getTechnicalInterviewErrorKey = (error) => (
  `errors.${getTechnicalInterviewErrorCode(error)}`
);

