import { TechnicalInterviewErrorCode } from './technicalInterview.types';

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
  if (typeof error?.code === 'string' && error.code.trim()) return error.code.trim();
  if (error instanceof TypeError) return TechnicalInterviewErrorCode.NETWORK_ERROR;
  return TechnicalInterviewErrorCode.UNKNOWN_ERROR;
};

export const getTechnicalInterviewErrorKey = (error) => (
  `errors.${getTechnicalInterviewErrorCode(error)}`
);
