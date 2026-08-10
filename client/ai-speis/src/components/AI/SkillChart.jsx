import React from 'react';
import {
  Radar,
  RadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  ResponsiveContainer,
} from 'recharts';

export const SkillChart = ({
  data = [
    { subject: 'Chuyên môn', score: 85, fullMark: 100 },
    { subject: 'Cấu trúc STAR', score: 70, fullMark: 100 },
    { subject: 'Giao tiếp', score: 90, fullMark: 100 },
    { subject: 'Khớp CV', score: 75, fullMark: 100 },
    { subject: 'Giải quyết VĐ', score: 80, fullMark: 100 },
  ],
  height = 300,
  className = '',
}) => {
  return (
    <div className={`w-full h-full flex flex-col items-center justify-center ${className}`}>
      <ResponsiveContainer width="100%" height={height}>
        <RadarChart cx="50%" cy="50%" outerRadius="75%" data={data}>
          <PolarGrid stroke="#CBD5E1" strokeDasharray="3 3" />
          <PolarAngleAxis
            dataKey="subject"
            tick={{ fill: '#475569', fontSize: 12, fontWeight: 600 }}
          />
          <PolarRadiusAxis angle={30} domain={[0, 100]} tick={{ fill: '#94A3B8', fontSize: 10 }} />
          <Radar
            name="Skill Evaluation"
            dataKey="score"
            stroke="#2563EB"
            fill="#2563EB"
            fillOpacity={0.25}
          />
        </RadarChart>
      </ResponsiveContainer>
    </div>
  );
};

export default SkillChart;
