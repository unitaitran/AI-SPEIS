import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  X,
  TrendingUp,
  TrendingDown,
  Minus,
  Calendar,
  Sparkles,
  BarChart2,
} from 'lucide-react';

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

function SkillHistoryModal({ skill, onClose }) {
  const { i18n } = useTranslation();
  const isEnglish = i18n.language === 'en';
  const [hoveredPoint, setHoveredPoint] = useState(null);

  if (!skill) return null;

  const rawHistory = Array.isArray(skill.history) && skill.history.length > 0
    ? skill.history
    : [
      { title: 'Phỏng vấn gần nhất', score: skill.score || 0, date: new Date().toISOString() },
    ];

  const history = rawHistory.map((item) => {
    const rawVal = item.score ?? item.Score ?? item.value ?? item.Value;
    const scoreNum = Number(rawVal);
    const fallbackSkillScore = Number(skill.score) || 0;
    const validScore = (!Number.isNaN(scoreNum) && scoreNum > 0)
      ? scoreNum
      : (fallbackSkillScore > 0 ? fallbackSkillScore : 0);

    return {
      title: item.title || item.Title || 'Buổi phỏng vấn',
      score: validScore,
      date: item.date || item.Date || new Date().toISOString(),
    };
  });

  const skillTitle = isEnglish ? (skill.labelEn || skill.name) : (skill.label || skill.name);
  const latestScore = Number(history[history.length - 1]?.score ?? skill.score ?? 0);
  const firstScore = Number(history[0]?.score ?? latestScore);
  const scoreDiff = latestScore - firstScore;

  // Chart coordinate calculations
  const chartHeight = 180;
  const chartWidth = 520;
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
    pathD = `M ${points[0].x - 40} ${points[0].y} L ${points[0].x + 40} ${points[0].y}`;
  } else {
    pathD = points.reduce((acc, pt, idx) => (
      idx === 0 ? `M ${pt.x} ${pt.y}` : `${acc} L ${pt.x} ${pt.y}`
    ), '');
  }

  // Area fill below line
  let areaD = '';
  if (points.length === 1) {
    areaD = `M ${points[0].x - 40} ${points[0].y} L ${points[0].x + 40} ${points[0].y} L ${points[0].x + 40} ${chartHeight - paddingBottom} L ${points[0].x - 40} ${chartHeight - paddingBottom} Z`;
  } else {
    const lastPt = points[points.length - 1];
    const firstPt = points[0];
    areaD = `${pathD} L ${lastPt.x} ${chartHeight - paddingBottom} L ${firstPt.x} ${chartHeight - paddingBottom} Z`;
  }

  return (
    <div
      className="behavior-dialog-backdrop"
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        display: 'grid',
        placeItems: 'center',
        background: 'rgba(15, 23, 42, 0.65)',
        backdropFilter: 'blur(6px)',
        padding: '1rem',
      }}
      role="presentation"
      onClick={onClose}
    >
      <section
        className="behavior-dialog"
        style={{
          width: 'min(620px, 100%)',
          maxHeight: '90vh',
          overflowY: 'auto',
          borderRadius: '20px',
          background: '#ffffff',
          boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.3)',
          display: 'flex',
          flexDirection: 'column',
          padding: 0,
        }}
        role="dialog"
        aria-modal="true"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '1.5rem 1.75rem',
            borderBottom: '1px solid #e2e8f0',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%)',
            borderTopLeftRadius: '20px',
            borderTopRightRadius: '20px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <div
              style={{
                width: '42px',
                height: '42px',
                borderRadius: '12px',
                background: '#e0f2fe',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#0284c7',
              }}
            >
              <BarChart2 size={22} />
            </div>
            <div>
              <h2 style={{ margin: 0, fontSize: '1.25rem', fontWeight: '700', color: '#0f172a' }}>
                {skillTitle}
              </h2>
              <span style={{ fontSize: '0.8125rem', color: '#64748b' }}>
                {isEnglish ? 'Skill Progress & Fluctuation Trend' : 'Tiến trình & Biểu đồ độ lên xuống qua các buổi phỏng vấn'}
              </span>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            style={{
              border: 'none',
              background: '#f1f5f9',
              borderRadius: '50%',
              width: '36px',
              height: '36px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
              color: '#64748b',
              transition: 'all 0.2s',
            }}
            onMouseOver={(e) => { e.currentTarget.style.background = '#e2e8f0'; }}
            onMouseOut={(e) => { e.currentTarget.style.background = '#f1f5f9'; }}
          >
            <X size={20} />
          </button>
        </div>

        {/* Body Content */}
        <div style={{ padding: '1.5rem 1.75rem' }}>
          {/* Summary Cards Row */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(3, 1fr)',
              gap: '1rem',
              marginBottom: '1.5rem',
            }}
          >
            {/* Metric 1: Latest Score */}
            <div
              style={{
                padding: '1rem',
                borderRadius: '14px',
                background: '#f0f9ff',
                border: '1px solid #bae6fd',
              }}
            >
              <span style={{ fontSize: '0.75rem', fontWeight: '600', color: '#0369a1', textTransform: 'uppercase' }}>
                {isEnglish ? 'Latest Score' : 'Lần gần nhất'}
              </span>
              <div style={{ fontSize: '1.5rem', fontWeight: '800', color: '#0284c7', marginTop: '0.25rem' }}>
                {latestScore.toFixed(1)} <small style={{ fontSize: '0.875rem', fontWeight: '500' }}>/ 10</small>
              </div>
            </div>

            {/* Metric 2: Initial Score */}
            <div
              style={{
                padding: '1rem',
                borderRadius: '14px',
                background: '#f8fafc',
                border: '1px solid #e2e8f0',
              }}
            >
              <span style={{ fontSize: '0.75rem', fontWeight: '600', color: '#64748b', textTransform: 'uppercase' }}>
                {isEnglish ? 'Initial Score' : 'Lần đầu tiên'}
              </span>
              <div style={{ fontSize: '1.5rem', fontWeight: '800', color: '#475569', marginTop: '0.25rem' }}>
                {firstScore.toFixed(1)} <small style={{ fontSize: '0.875rem', fontWeight: '500' }}>/ 10</small>
              </div>
            </div>

            {/* Metric 3: Score Trend */}
            <div
              style={{
                padding: '1rem',
                borderRadius: '14px',
                background: scoreDiff > 0 ? '#f0fdf4' : scoreDiff < 0 ? '#fef2f2' : '#f8fafc',
                border: `1px solid ${scoreDiff > 0 ? '#bbf7d0' : scoreDiff < 0 ? '#fecaca' : '#e2e8f0'}`,
              }}
            >
              <span
                style={{
                  fontSize: '0.75rem',
                  fontWeight: '600',
                  color: scoreDiff > 0 ? '#15803d' : scoreDiff < 0 ? '#b91c1c' : '#64748b',
                  textTransform: 'uppercase',
                }}
              >
                {isEnglish ? 'Total Fluctuation' : 'Độ lên xuống'}
              </span>
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.35rem',
                  fontSize: '1.35rem',
                  fontWeight: '800',
                  color: scoreDiff > 0 ? '#16a34a' : scoreDiff < 0 ? '#dc2626' : '#64748b',
                  marginTop: '0.25rem',
                }}
              >
                {scoreDiff > 0 ? <TrendingUp size={20} /> : scoreDiff < 0 ? <TrendingDown size={20} /> : <Minus size={20} />}
                {scoreDiff > 0 ? `+${scoreDiff.toFixed(1)}` : scoreDiff.toFixed(1)}
              </div>
            </div>
          </div>

          {/* SVG Line Chart Section */}
          <div
            style={{
              background: '#ffffff',
              border: '1px solid #e2e8f0',
              borderRadius: '16px',
              padding: '1.25rem 1rem 0.75rem',
              marginBottom: '1.5rem',
              position: 'relative',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem', padding: '0 0.5rem' }}>
              <span style={{ fontSize: '0.875rem', fontWeight: '700', color: '#1e293b', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                <Sparkles size={16} style={{ color: '#0284c7' }} />
                {isEnglish ? 'Skill Evolution Curve' : 'Biểu đồ đường biến động năng lực'}
              </span>
              <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
                {history.length} {isEnglish ? 'Evaluated Sessions' : 'Lượt đánh giá'}
              </span>
            </div>

            <svg
              viewBox={`0 0 ${chartWidth} ${chartHeight}`}
              style={{ width: '100%', height: 'auto', overflow: 'visible' }}
            >
              <defs>
                <linearGradient id="skillAreaGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#38bdf8" stopOpacity="0.3" />
                  <stop offset="100%" stopColor="#0284c7" stopOpacity="0.0" />
                </linearGradient>
                <filter id="shadowFilter" x="-10%" y="-10%" width="120%" height="120%">
                  <feDropShadow dx="0" dy="4" stdDeviation="4" floodColor="#0284c7" floodOpacity="0.3" />
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
              <path d={areaD} fill="url(#skillAreaGradient)" />

              {/* Connecting Line */}
              <path
                d={pathD}
                fill="none"
                stroke="#0284c7"
                strokeWidth="3.5"
                strokeLinecap="round"
                strokeLinejoin="round"
                filter="url(#shadowFilter)"
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
                      stroke="#0284c7"
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
                      fill="#0284c7"
                    >
                      {pt.scoreVal.toFixed(1)}
                    </text>
                  </g>
                );
              })}
            </svg>

            {/* Hover Tooltip Popup */}
            {hoveredPoint ? (
              <div
                style={{
                  position: 'absolute',
                  top: '10px',
                  right: '15px',
                  background: '#0f172a',
                  color: '#ffffff',
                  padding: '0.5rem 0.85rem',
                  borderRadius: '10px',
                  fontSize: '0.75rem',
                  boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.3)',
                  zIndex: 30,
                  pointerEvents: 'none',
                }}
              >
                <div style={{ fontWeight: '700', color: '#38bdf8' }}>{hoveredPoint.title || 'Buổi phỏng vấn'}</div>
                <div>{formatDate(hoveredPoint.date)}</div>
                <div style={{ marginTop: '2px', fontWeight: '800', color: '#f59e0b' }}>
                  Điểm: {hoveredPoint.scoreVal.toFixed(2)} / 10
                </div>
              </div>
            ) : null}
          </div>

          {/* History Breakdown List */}
          <div>
            <h3 style={{ margin: '0 0 0.85rem', fontSize: '0.9375rem', fontWeight: '700', color: '#0f172a' }}>
              {isEnglish ? 'Detailed Session Timeline' : 'Chi tiết từng đợt phỏng vấn'}
            </h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.65rem' }}>
              {history.slice().reverse().map((item, idx, arr) => {
                const itemScore = Number(item.score) || 0;
                const prevItemScore = idx < arr.length - 1 ? Number(arr[idx + 1].score) || 0 : null;
                const diff = prevItemScore != null ? itemScore - prevItemScore : null;

                return (
                  <div
                    key={idx}
                    style={{
                      padding: '0.85rem 1rem',
                      borderRadius: '12px',
                      background: '#f8fafc',
                      border: '1px solid #e2e8f0',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                      <div
                        style={{
                          width: '36px',
                          height: '36px',
                          borderRadius: '50%',
                          background: '#e0f2fe',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          color: '#0284c7',
                        }}
                      >
                        <Calendar size={18} />
                      </div>
                      <div>
                        <div style={{ fontSize: '0.875rem', fontWeight: '700', color: '#1e293b' }}>
                          {item.title || `Phỏng vấn #${arr.length - idx}`}
                        </div>
                        <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                          {formatDate(item.date)}
                        </div>
                      </div>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.85rem' }}>
                      {diff != null ? (
                        <span
                          style={{
                            fontSize: '0.75rem',
                            fontWeight: '700',
                            padding: '0.2rem 0.5rem',
                            borderRadius: '6px',
                            background: diff > 0 ? '#dcfce7' : diff < 0 ? '#fee2e2' : '#f1f5f9',
                            color: diff > 0 ? '#15803d' : diff < 0 ? '#b91c1c' : '#64748b',
                          }}
                        >
                          {diff > 0 ? `+${diff.toFixed(1)}` : diff.toFixed(1)}
                        </span>
                      ) : null}
                      <span
                        style={{
                          fontSize: '1rem',
                          fontWeight: '800',
                          color: '#0284c7',
                          background: '#ffffff',
                          padding: '0.25rem 0.75rem',
                          borderRadius: '8px',
                          border: '1px solid #bae6fd',
                        }}
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
    </div>
  );
}

export default SkillHistoryModal;
