import React from 'react';
import EvaluationScore from './EvaluationScore';
import SkillChart from './SkillChart';
import Card from '../UI/Card';
import Badge from '../UI/Badge';
import { Sparkles, CheckCircle2, AlertTriangle, Lightbulb } from 'lucide-react';

export const InterviewReport = ({
  overallScore = 8.5,
  candidateName = 'Ứng viên',
  roleTitle = 'Software Engineer',
  dateString = new Date().toLocaleDateString('vi-VN'),
  keyStrengths = [],
  areasToImprove = [],
  recommendations = [],
  skillData,
  className = '',
}) => {
  return (
    <div className={`flex flex-col gap-6 w-full ${className}`}>
      {/* Header Banner */}
      <Card variant="ai" className="flex flex-col md:flex-row items-center justify-between gap-6 p-6">
        <div className="flex flex-col gap-1 text-center md:text-left">
          <Badge variant="ai" size="sm" icon={Sparkles} className="w-fit mx-auto md:mx-0">
            Báo cáo đánh giá AI
          </Badge>
          <h2 className="text-xl md:text-2xl font-extrabold text-text-primary">
            Kết quả phỏng vấn: {roleTitle}
          </h2>
          <p className="text-xs text-text-secondary">
            Ứng viên: <span className="font-semibold text-text-primary">{candidateName}</span> • Ngày thực hiện: {dateString}
          </p>
        </div>

        <EvaluationScore score={overallScore} size="lg" />
      </Card>

      {/* Grid: Skill Radar Chart & Strengths / Weaknesses */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Radar Skill Chart */}
        <Card variant="default" className="flex flex-col gap-3 p-5">
          <h3 className="text-sm font-bold text-text-primary">Biểu đồ đánh giá năng lực phỏng vấn</h3>
          <div className="h-[280px]">
            <SkillChart data={skillData} height={280} />
          </div>
        </Card>

        {/* Strengths & Weaknesses */}
        <div className="flex flex-col gap-4">
          <Card variant="default" className="p-5 border-l-4 border-l-success">
            <h4 className="text-sm font-bold text-text-primary flex items-center gap-2 mb-2">
              <CheckCircle2 size={18} className="text-success" />
              Điểm mạnh chính (Key Strengths)
            </h4>
            <ul className="list-disc list-inside text-xs text-text-secondary flex flex-col gap-1.5 pl-1">
              {keyStrengths.length > 0 ? (
                keyStrengths.map((str, idx) => <li key={idx}>{str}</li>)
              ) : (
                <li>Giao tiếp rõ ràng, trả lời đúng trọng tâm câu hỏi chuyên môn.</li>
              )}
            </ul>
          </Card>

          <Card variant="default" className="p-5 border-l-4 border-l-warning">
            <h4 className="text-sm font-bold text-text-primary flex items-center gap-2 mb-2">
              <AlertTriangle size={18} className="text-warning" />
              Điểm cần cải thiện (Areas to Improve)
            </h4>
            <ul className="list-disc list-inside text-xs text-text-secondary flex flex-col gap-1.5 pl-1">
              {areasToImprove.length > 0 ? (
                areasToImprove.map((area, idx) => <li key={idx}>{area}</li>)
              ) : (
                <li>Cần bổ sung ví dụ thực tế theo mô hình STAR trong câu hỏi hành vi.</li>
              )}
            </ul>
          </Card>
        </div>
      </div>

      {/* AI Recommendations */}
      {recommendations && recommendations.length > 0 && (
        <Card variant="ai" className="p-5">
          <h4 className="text-sm font-bold text-secondary flex items-center gap-2 mb-3">
            <Lightbulb size={18} className="text-secondary" />
            Lộ trình & Gợi ý cải thiện từ AI
          </h4>
          <div className="flex flex-col gap-2">
            {recommendations.map((rec, idx) => (
              <div key={idx} className="p-3 bg-surface rounded-md border border-secondary/20 text-xs text-text-primary flex items-start gap-2.5">
                <span className="w-5 h-5 rounded-full bg-secondary-light text-secondary font-bold flex items-center justify-center shrink-0 text-[10px]">
                  {idx + 1}
                </span>
                <span className="leading-relaxed">{rec}</span>
              </div>
            ))}
          </div>
        </Card>
      )}
    </div>
  );
};

export default InterviewReport;
