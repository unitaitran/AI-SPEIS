import { getPerformanceBandLabel, scorePercentage } from './campaignResult';

describe('campaign result helpers', () => {
  test('converts a 0-10 score into a clamped percentage', () => {
    expect(scorePercentage(7.25)).toBe(72.5);
    expect(scorePercentage(12)).toBe(100);
    expect(scorePercentage(-1)).toBe(0);
  });

  test('localizes shared performance bands', () => {
    expect(getPerformanceBandLabel('GOOD', 'vi')).toBe('Khá');
    expect(getPerformanceBandLabel('VERY_GOOD', 'en')).toBe('Very good');
  });
});
