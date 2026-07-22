export const clampScore = (value) => Math.min(10, Math.max(0, Number(value) || 0));

export const scorePercentage = (value) => clampScore(value) * 10;

export const getPerformanceBandLabel = (band, language = 'vi') => {
  const labels = {
    EXCELLENT: { vi: 'Xuất sắc', en: 'Excellent' },
    VERY_GOOD: { vi: 'Rất tốt', en: 'Very good' },
    GOOD: { vi: 'Khá', en: 'Good' },
    MINIMUM_REQUIREMENT_MET: { vi: 'Đạt yêu cầu', en: 'Requirement met' },
    FAIR: { vi: 'Đạt yêu cầu', en: 'Requirement met' },
    WEAK: { vi: 'Yếu', en: 'Weak' },
    VERY_WEAK: { vi: 'Rất yếu', en: 'Very weak' },
    POOR: { vi: 'Rất yếu', en: 'Very weak' },
  };
  return labels[String(band || '').toUpperCase()]?.[language] || band || '—';
};

export const getRoundLabel = (roundType, language = 'vi') => ({
  Behavior: language === 'vi' ? 'Behavioral' : 'Behavioral',
  Technical: 'Technical',
  Code: 'Coding',
}[roundType] || roundType);

