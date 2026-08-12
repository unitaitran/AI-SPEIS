import { TechnicalV2ErrorCode } from './technicalV2Interview.types';

const aliases = Object.freeze({
  INVALID_USER_IDENTITY: TechnicalV2ErrorCode.SESSION_ACCESS_DENIED,
});

export const getTechnicalV2ErrorCode = (error) => {
  if (typeof error?.code === 'string' && error.code.trim()) return aliases[error.code.trim()] || error.code.trim();
  if (error instanceof TypeError) return TechnicalV2ErrorCode.NETWORK_ERROR;
  return TechnicalV2ErrorCode.UNKNOWN_ERROR;
};

export const getTechnicalV2ErrorKey = (error) => `error.${getTechnicalV2ErrorCode(error)}`;
