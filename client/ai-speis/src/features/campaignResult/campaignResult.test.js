import {
  getMetricLabel,
  getPerformanceBandLabel,
  getRoundLabel,
  getSourceLabel,
  scorePercentage,
} from './campaignResult';

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

  test('localizes backend campaign labels using the setup language', () => {
    expect(getRoundLabel('Technical', 'vi')).toBe('Kỹ thuật');
    expect(getMetricLabel({ code: 'PROBLEM_SOLVING', name: 'Problem Solving' }, 'vi'))
      .toBe('Giải quyết vấn đề');
    expect(getSourceLabel('Technical Depth', 'vi')).toBe('Độ sâu kỹ thuật');
    expect(getMetricLabel({ code: 'PROBLEM_SOLVING', name: 'Problem Solving' }, 'en'))
      .toBe('Problem Solving');
  });
});
