export const clampScore = (value) => Math.min(10, Math.max(0, Number(value) || 0));

export const scorePercentage = (value) => clampScore(value) * 10;

const PERFORMANCE_BAND_LABELS = {
  EXCELLENT: { vi: 'Xuất sắc', en: 'Excellent' },
  VERY_GOOD: { vi: 'Rất tốt', en: 'Very good' },
  GOOD: { vi: 'Khá', en: 'Good' },
  MINIMUM_REQUIREMENT_MET: { vi: 'Đạt yêu cầu', en: 'Requirement met' },
  FAIR: { vi: 'Đạt yêu cầu', en: 'Requirement met' },
  WEAK: { vi: 'Yếu', en: 'Weak' },
  VERY_WEAK: { vi: 'Rất yếu', en: 'Very weak' },
  POOR: { vi: 'Rất yếu', en: 'Very weak' },
};

const ROUND_LABELS = {
  Behavior: { vi: 'Hành vi', en: 'Behavioral' },
  Technical: { vi: 'Kỹ thuật', en: 'Technical' },
  Code: { vi: 'Lập trình', en: 'Coding' },
};

const METRIC_LABELS = {
  PROFESSIONAL_KNOWLEDGE: { vi: 'Kiến thức chuyên môn', en: 'Professional Knowledge' },
  COMMUNICATION_SKILLS: { vi: 'Kỹ năng giao tiếp', en: 'Communication Skills' },
  CV_UNDERSTANDING: { vi: 'Hiểu biết về CV', en: 'CV Understanding' },
  PROBLEM_SOLVING: { vi: 'Giải quyết vấn đề', en: 'Problem Solving' },
};

const SOURCE_LABELS = {
  'Technical Accuracy': { vi: 'Độ chính xác kỹ thuật', en: 'Technical Accuracy' },
  'Technical Depth': { vi: 'Độ sâu kỹ thuật', en: 'Technical Depth' },
  'Technical Application': { vi: 'Ứng dụng kỹ thuật', en: 'Technical Application' },
  'Technical Communication': { vi: 'Giao tiếp kỹ thuật', en: 'Technical Communication' },
  'Technical Reasoning': { vi: 'Lập luận kỹ thuật', en: 'Technical Reasoning' },
  'Behavioral Communication': { vi: 'Giao tiếp hành vi', en: 'Behavioral Communication' },
  'Behavioral Action & Ownership': { vi: 'Hành động và tinh thần trách nhiệm', en: 'Behavioral Action & Ownership' },
  Coding: { vi: 'Lập trình', en: 'Coding' },
};

export const getPerformanceBandLabel = (band, language = 'vi') => {
  return PERFORMANCE_BAND_LABELS[String(band || '').toUpperCase()]?.[language] || band || '—';
};

export const getRoundLabel = (roundType, language = 'vi') => (
  ROUND_LABELS[roundType]?.[language] || roundType
);

export const getMetricLabel = (metric, language = 'vi') => (
  METRIC_LABELS[metric?.code]?.[language] || metric?.name || metric?.code || '—'
);

export const getSourceLabel = (source, language = 'vi') => (
  SOURCE_LABELS[source]?.[language] || source
);

