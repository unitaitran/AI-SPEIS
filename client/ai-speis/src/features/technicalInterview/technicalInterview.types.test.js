import {
  canSubmitTechnicalAnswer,
  TechnicalSessionStatus,
} from './technicalInterview.types';

describe('canSubmitTechnicalAnswer', () => {
  test.each([
    TechnicalSessionStatus.QUESTION_READY,
    TechnicalSessionStatus.ANSWERING,
  ])('allows submission while the current question is answerable: %s', (status) => {
    expect(canSubmitTechnicalAnswer(status)).toBe(true);
  });

  test.each([
    TechnicalSessionStatus.CREATED,
    TechnicalSessionStatus.SELECTING_QUESTION,
    TechnicalSessionStatus.EVALUATING,
    TechnicalSessionStatus.COMPLETED,
    TechnicalSessionStatus.FAILED,
    undefined,
  ])('blocks submission outside an answerable state: %s', (status) => {
    expect(canSubmitTechnicalAnswer(status)).toBe(false);
  });
});
