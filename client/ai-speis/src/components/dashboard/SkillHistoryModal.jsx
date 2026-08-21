import React, { useState, useEffect, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import {
  X,
  TrendingUp,
  TrendingDown,
  Minus,
  Calendar,
  Sparkles,
  Layers,
  MessageSquare,
  FileCheck2,
  Lightbulb,
} from 'lucide-react';
import './SkillHistoryModal.css';

const DEFAULT_SKILL_CONFIGS = {
  PROFESSIONAL_KNOWLEDGE: {
    code: 'PROFESSIONAL_KNOWLEDGE',
    label: 'Kiến thức chuyên môn',
    labelEn: 'Professional Knowledge',
    icon: Layers,
    color: '#0284c7',
    bgLight: '#f0f9ff',
    border: '#bae6fd',
    gradientFrom: '#38bdf8',
    gradientTo: '#0284c7',
    textColor: '#0369a1',
  },
  COMMUNICATION_SKILLS: {
    code: 'COMMUNICATION_SKILLS',
    label: 'Kỹ năng giao tiếp',
    labelEn: 'Communication Skills',
    icon: MessageSquare,
    color: '#9333ea',
    bgLight: '#faf5ff',
    border: '#e9d5ff',
    gradientFrom: '#c084fc',
    gradientTo: '#9333ea',
    textColor: '#7e22ce',
  },
  CV_UNDERSTANDING: {
    code: 'CV_UNDERSTANDING',
    label: 'Hiểu biết về CV',
    labelEn: 'CV Understanding',
    icon: FileCheck2,
    color: '#d97706',
    bgLight: '#fffbeb',
    border: '#fde68a',
    gradientFrom: '#fbbf24',
    gradientTo: '#d97706',
    textColor: '#b45309',
  },
  PROBLEM_SOLVING: {
    code: 'PROBLEM_SOLVING',
    label: 'Giải quyết vấn đề',
    labelEn: 'Problem Solving',
    icon: Lightbulb,
    color: '#059669',
    bgLight: '#ecfdf5',
    border: '#a7f3d0',
    gradientFrom: '#34d399',
    gradientTo: '#059669',
    textColor: '#047857',
  },
};

function formatDate(dateStr) {
  if (!dateStr) return '';
  try {
    const d = new Date(dateStr);
    if (Number.isNaN(d.getTime())) return dateStr;
    const timeStr = d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    const dateFormatted = d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    return `${timeStr} ${dateFormatted}`;
  } catch {
    return dateStr;
  }
}

function SkillHistoryModal({ skill, allSkills = [], onClose }) {
  const { i18n } = useTranslation();
  const isEnglish = (i18n.language || '').toLowerCase().startsWith('en');
  const [activeCode, setActiveCode] = useState(skill?.code || 'PROFESSIONAL_KNOWLEDGE');
  const [hoveredPoint, setHoveredPoint] = useState(null);

  useEffect(() => {
    if (skill?.code) {
      setActiveCode(skill.code);
    }
  }, [skill?.code]);

  // Merge full list of 4 skills
  const skillsList = useMemo(() => {
    const defaultCodes = ['PROFESSIONAL_KNOWLEDGE', 'COMMUNICATION_SKILLS', 'CV_UNDERSTANDING', 'PROBLEM_SOLVING'];
    return defaultCodes.map((code) => {
      const foundInProps = (allSkills || []).find((s) => s.code === code);
      const conf = DEFAULT_SKILL_CONFIGS[code];
      const currentScore = foundInProps?.score ?? (skill?.code === code ? skill.score : 0);
      const history = foundInProps?.history ?? (skill?.code === code ? skill.history : []);
      return {
        ...conf,
        ...foundInProps,
        score: Number(currentScore) || 0,
        history: history || [],
      };
    });
  }, [allSkills, skill]);

  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape') {
        onClose?.();
      }
    };
    if (skill) {
      window.addEventListener('keydown', handleKeyDown);
    }
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [skill, onClose]);

  if (!skill) return null;

  const activeSkill = skillsList.find((s) => s.code === activeCode) || skillsList[0];
  const conf = DEFAULT_SKILL_CONFIGS[activeSkill.code] || DEFAULT_SKILL_CONFIGS.PROFESSIONAL_KNOWLEDGE;

  const rawHistory = Array.isArray(activeSkill.history) && activeSkill.history.length > 0
    ? activeSkill.history
    : [
      { title: 'Phỏng vấn gần nhất', score: activeSkill.score || 0, date: new Date().toISOString() },
    ];

  const history = rawHistory.map((item, idx) => {
    const rawVal = item.score ?? item.Score ?? item.value ?? item.Value;
    const scoreNum = Number(rawVal);
    const fallbackScore = Number(activeSkill.score) || 0;
    const validScore = (!Number.isNaN(scoreNum) && scoreNum > 0)
      ? scoreNum
      : (fallbackScore > 0 ? fallbackScore : 0);

    return {
      title: item.title || item.Title || `Phỏng vấn #${idx + 1}`,
      score: validScore,
      date: item.date || item.Date || new Date().toISOString(),
    };
  });

  const skillTitle = isEnglish ? (activeSkill.labelEn || activeSkill.name) : (activeSkill.label || activeSkill.name);
  const latestScore = Number(history[history.length - 1]?.score ?? activeSkill.score ?? 0);
  const firstScore = Number(history[0]?.score ?? latestScore);
  const scoreDiff = latestScore - firstScore;

  // Chart coordinate calculations
  const chartHeight = 180;
  const chartWidth = 560;
  const paddingX = 40;
  const paddingTop = 20;
  const paddingBottom = 30;
  const usableWidth = chartWidth - paddingX * 2;
  const usableHeight = chartHeight - paddingTop - paddingBottom;

  const points = history.map((pt, idx) => {
    const scoreVal = Math.min(10, Math.max(0, Number(pt.score) || 0));
    const x = history.length === 1
      ? paddingX + usableWidth / 2
      : paddingX + (idx / (history.length - 1)) * usableWidth;
    const y = paddingTop + (1 - scoreVal / 10) * usableHeight;
    return { ...pt, x, y, scoreVal, index: idx };
  });

  // Generate SVG path string
  let pathD = '';
  if (points.length === 1) {
    pathD = `M ${points[0].x - 45} ${points[0].y} L ${points[0].x + 45} ${points[0].y}`;
  } else {
    pathD = points.reduce((acc, pt, idx) => (
      idx === 0 ? `M ${pt.x} ${pt.y}` : `${acc} L ${pt.x} ${pt.y}`
    ), '');
  }

  // Area fill below line
  let areaD = '';
  if (points.length === 1) {
    areaD = `M ${points[0].x - 45} ${points[0].y} L ${points[0].x + 45} ${points[0].y} L ${points[0].x + 45} ${chartHeight - paddingBottom} L ${points[0].x - 45} ${chartHeight - paddingBottom} Z`;
  } else {
    const lastPt = points[points.length - 1];
    const firstPt = points[0];
    areaD = `${pathD} L ${lastPt.x} ${chartHeight - paddingBottom} L ${firstPt.x} ${chartHeight - paddingBottom} Z`;
  }

  const ActiveIcon = conf.icon;

  if (typeof document === 'undefined') return null;

  return createPortal(
    <div
      className="skill-modal-backdrop"
      role="presentation"
      onClick={onClose}
    >
      <section
        className="skill-modal-container"
        role="dialog"
        aria-modal="true"
        aria-labelledby="skill-modal-title"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="skill-modal-header">
          <div className="skill-modal-header__left">
            <div
              className="skill-modal-header__icon"
              style={{ background: conf.bgLight, color: conf.color }}
            >
              <ActiveIcon size={24} />
            </div>
            <div>
              <h2 id="skill-modal-title" className="skill-modal-header__title">
                {skillTitle}
              </h2>
              <span className="skill-modal-header__subtitle">
                {isEnglish ? 'Skill Progress & Fluctuation Trend' : 'Tiến trình & Biểu đồ biến động qua các buổi phỏng vấn'}
              </span>
            </div>
          </div>
          <button
            type="button"
            className="skill-modal-header__close"
            onClick={onClose}
            aria-label="Close"
          >
            <X size={20} />
          </button>
        </div>

        {/* Body Content */}
        <div className="skill-modal-body">
          {/* Top 4 Skill Tabs */}
          <div className="skill-modal-tabs">
            {skillsList.map((item) => {
              const isActive = item.code === activeCode;
              const itemConf = DEFAULT_SKILL_CONFIGS[item.code] || conf;
              const TabIcon = itemConf.icon;
              const itemScore = Number(item.score) || 0;

              return (
                <button
                  key={item.code}
                  type="button"
                  className={`skill-modal-tab ${isActive ? 'skill-modal-tab--active' : ''}`}
                  style={{
                    borderColor: isActive ? itemConf.color : '#e2e8f0',
                  }}
                  onClick={() => setActiveCode(item.code)}
                >
                  <div className="skill-modal-tab__top">
                    <div
                      className="skill-modal-tab__icon"
                      style={{
                        background: isActive ? itemConf.color : itemConf.bgLight,
                        color: isActive ? '#ffffff' : itemConf.color,
                      }}
                    >
                      <TabIcon size={16} />
                    </div>
                    <span
                      className="skill-modal-tab__badge"
                      style={{
                        background: isActive ? itemConf.bgLight : '#f1f5f9',
                        color: isActive ? itemConf.textColor : '#64748b',
                        border: `1px solid ${isActive ? itemConf.border : '#e2e8f0'}`,
                      }}
                    >
                      {itemScore.toFixed(1)}/10
                    </span>
                  </div>
                  <span
                    className="skill-modal-tab__title"
                    style={{ color: isActive ? itemConf.textColor : '#1e293b' }}
                  >
                    {isEnglish ? item.labelEn : item.label}
                  </span>
                </button>
              );
            })}
          </div>

          {/* 3 Summary Cards */}
          <div className="skill-modal-metrics">
            {/* Metric 1: Latest Score */}
            <div
              className="skill-modal-metric-card"
              style={{ background: conf.bgLight, border: `1px solid ${conf.border}` }}
            >
              <span className="skill-modal-metric-card__label" style={{ color: conf.textColor }}>
                {isEnglish ? 'Latest Score' : 'Lần gần nhất'}
              </span>
              <div className="skill-modal-metric-card__value" style={{ color: conf.color }}>
                {latestScore.toFixed(1)} <small style={{ fontSize: '0.875rem', fontWeight: '500' }}>/ 10</small>
              </div>
            </div>

            {/* Metric 2: Initial Score */}
            <div
              className="skill-modal-metric-card"
              style={{ background: '#f8fafc', border: '1px solid #e2e8f0' }}
            >
              <span className="skill-modal-metric-card__label" style={{ color: '#64748b' }}>
                {isEnglish ? 'Initial Score' : 'Lần đầu tiên'}
              </span>
              <div className="skill-modal-metric-card__value" style={{ color: '#475569' }}>
                {firstScore.toFixed(1)} <small style={{ fontSize: '0.875rem', fontWeight: '500' }}>/ 10</small>
              </div>
            </div>

            {/* Metric 3: Score Trend */}
            <div
              className="skill-modal-metric-card"
              style={{
                background: scoreDiff > 0 ? '#f0fdf4' : scoreDiff < 0 ? '#fef2f2' : '#f8fafc',
                border: `1px solid ${scoreDiff > 0 ? '#bbf7d0' : scoreDiff < 0 ? '#fecaca' : '#e2e8f0'}`,
              }}
            >
              <span
                className="skill-modal-metric-card__label"
                style={{ color: scoreDiff > 0 ? '#15803d' : scoreDiff < 0 ? '#b91c1c' : '#64748b' }}
              >
                {isEnglish ? 'Total Fluctuation' : 'Độ lên xuống'}
              </span>
              <div
                className="skill-modal-metric-card__value"
                style={{ color: scoreDiff > 0 ? '#16a34a' : scoreDiff < 0 ? '#dc2626' : '#64748b' }}
              >
                {scoreDiff > 0 ? <TrendingUp size={22} /> : scoreDiff < 0 ? <TrendingDown size={22} /> : <Minus size={22} />}
                {scoreDiff > 0 ? `+${scoreDiff.toFixed(1)}` : scoreDiff.toFixed(1)}
              </div>
            </div>
          </div>

          {/* SVG Line Chart */}
          <div className="skill-modal-chart-card">
            <div className="skill-modal-chart-header">
              <span className="skill-modal-chart-title">
                <Sparkles size={16} style={{ color: conf.color }} />
                {isEnglish ? `${skillTitle} Evolution Curve` : `Biểu đồ biến động ${skillTitle.toLowerCase()}`}
              </span>
              <span className="skill-modal-chart-count">
                {history.length} {isEnglish ? 'Evaluated Sessions' : 'Lượt đánh giá'}
              </span>
            </div>

            <svg
              viewBox={`0 0 ${chartWidth} ${chartHeight}`}
              style={{ width: '100%', height: 'auto', overflow: 'visible' }}
            >
              <defs>
                <linearGradient id={`skillAreaGrad_${activeCode}`} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={conf.gradientFrom} stopOpacity="0.35" />
                  <stop offset="100%" stopColor={conf.gradientTo} stopOpacity="0.0" />
                </linearGradient>
                <filter id={`shadowFilter_${activeCode}`} x="-10%" y="-10%" width="120%" height="120%">
                  <feDropShadow dx="0" dy="4" stdDeviation="4" floodColor={conf.color} floodOpacity="0.25" />
                </filter>
              </defs>

              {/* Y-Axis Grid Lines */}
              {[10, 5, 0].map((level) => {
                const yPos = paddingTop + (1 - level / 10) * usableHeight;
                return (
                  <g key={level}>
                    <line
                      x1={paddingX}
                      y1={yPos}
                      x2={chartWidth - paddingX}
                      y2={yPos}
                      stroke="#e2e8f0"
                      strokeDasharray={level === 0 ? 'none' : '4 4'}
                      strokeWidth={level === 0 ? 1.5 : 1}
                    />
                    <text
                      x={paddingX - 10}
                      y={yPos + 4}
                      textAnchor="end"
                      fontSize="10"
                      fontWeight="600"
                      fill="#94a3b8"
                    >
                      {level}
                    </text>
                  </g>
                );
              })}

              {/* Area Under Line */}
              <path d={areaD} fill={`url(#skillAreaGrad_${activeCode})`} />

              {/* Connecting Line */}
              <path
                d={pathD}
                fill="none"
                stroke={conf.color}
                strokeWidth="3.5"
                strokeLinecap="round"
                strokeLinejoin="round"
                filter={`url(#shadowFilter_${activeCode})`}
              />

              {/* Interactive Data Node Points */}
              {points.map((pt) => {
                const isHovered = hoveredPoint?.index === pt.index;
                return (
                  <g key={pt.index}>
                    <circle
                      cx={pt.x}
                      cy={pt.y}
                      r={isHovered ? 8 : 6}
                      fill="#ffffff"
                      stroke={conf.color}
                      strokeWidth={isHovered ? 3.5 : 2.5}
                      style={{ cursor: 'pointer', transition: 'all 0.2s' }}
                      onMouseEnter={() => setHoveredPoint(pt)}
                      onMouseLeave={() => setHoveredPoint(null)}
                    />
                    {/* Score Value Label on top of Node */}
                    <text
                      x={pt.x}
                      y={pt.y - 12}
                      textAnchor="middle"
                      fontSize="11"
                      fontWeight="800"
                      fill={conf.color}
                    >
                      {pt.scoreVal.toFixed(1)}
                    </text>
                  </g>
                );
              })}
            </svg>

            {/* Hover Tooltip Popup */}
            {hoveredPoint ? (
              <div className="skill-modal-tooltip">
                <div style={{ fontWeight: '700', color: conf.gradientFrom }}>{hoveredPoint.title || 'Buổi phỏng vấn'}</div>
                <div>{formatDate(hoveredPoint.date)}</div>
                <div style={{ marginTop: '2px', fontWeight: '800', color: '#f59e0b' }}>
                  Điểm: {hoveredPoint.scoreVal.toFixed(2)} / 10
                </div>
              </div>
            ) : null}
          </div>

          {/* Detailed Timeline Breakdown */}
          <div className="skill-modal-timeline">
            <h3 className="skill-modal-timeline__title">
              {isEnglish ? 'Detailed Session Timeline' : 'Chi tiết từng đợt phỏng vấn'}
            </h3>
            <div className="skill-modal-timeline__list">
              {history.slice().reverse().map((item, idx, arr) => {
                const itemScore = Number(item.score) || 0;
                const prevItemScore = idx < arr.length - 1 ? Number(arr[idx + 1].score) || 0 : null;
                const diff = prevItemScore != null ? itemScore - prevItemScore : null;

                return (
                  <div key={idx} className="skill-modal-timeline__item">
                    <div className="skill-modal-timeline__item-left">
                      <div
                        className="skill-modal-timeline__item-icon"
                        style={{ background: conf.bgLight, color: conf.color }}
                      >
                        <Calendar size={18} />
                      </div>
                      <div>
                        <div className="skill-modal-timeline__item-name">
                          {item.title || `Phỏng vấn #${arr.length - idx}`}
                        </div>
                        <div className="skill-modal-timeline__item-date">
                          {formatDate(item.date)}
                        </div>
                      </div>
                    </div>

                    <div className="skill-modal-timeline__item-right">
                      {diff != null ? (
                        <span
                          className="skill-modal-timeline__diff-badge"
                          style={{
                            background: diff > 0 ? '#dcfce7' : diff < 0 ? '#fee2e2' : '#f1f5f9',
                            color: diff > 0 ? '#15803d' : diff < 0 ? '#b91c1c' : '#64748b',
                          }}
                        >
                          {diff > 0 ? `+${diff.toFixed(1)}` : diff.toFixed(1)}
                        </span>
                      ) : null}
                      <span
                        className="skill-modal-timeline__score-badge"
                        style={{ color: conf.color, border: `1px solid ${conf.border}` }}
                      >
                        {itemScore.toFixed(1)} / 10
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </section>
    </div>,
    document.body
  );
}

export default SkillHistoryModal;
