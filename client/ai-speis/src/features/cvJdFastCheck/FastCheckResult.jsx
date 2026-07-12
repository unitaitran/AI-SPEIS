import React from 'react';
import { CheckCircle2, CircleAlert, Lightbulb } from 'lucide-react';

function FastCheckResult({ result }) {
  return (
    <div className="fast-check__result" aria-live="polite">
      <div className="fast-check__result-heading">
        <div>
          <p className="fast-check__eyebrow">KẾT QUẢ PHÂN TÍCH</p>
          <h3>Mức độ phù hợp CV với JD</h3>
        </div>
        <span className="fast-check__result-badge"><CheckCircle2 size={14} /> Hoàn tất</span>
      </div>

      <div className="fast-check__score-card">
        <div
          className="fast-check__score-ring"
          style={{ '--fast-check-score': `${result.score * 3.6}deg` }}
          role="img"
          aria-label={`Điểm phù hợp ${result.score} trên 100`}
        >
          <div><strong>{result.score}</strong><span>/100</span></div>
        </div>
        <div className="fast-check__score-copy">
          <span>OVERALL MATCH SCORE</span>
          {result.suitabilityLevel && <h4>{result.suitabilityLevel}</h4>}
          <p>Điểm do backend AI trả về sau khi đối chiếu dữ liệu đã trích xuất từ CV và JD.</p>
          <div className="fast-check__score-track" aria-hidden="true">
            <span style={{ width: `${result.score}%` }} />
          </div>
        </div>
      </div>

      <div className="fast-check__analysis-grid">
        <article className="fast-check__analysis-card fast-check__analysis-card--success">
          <div className="fast-check__analysis-title">
            <CheckCircle2 size={20} />
            <div><h4>Điểm phù hợp</h4><p>Kỹ năng backend tìm thấy ở cả CV và JD</p></div>
          </div>
          {result.strengths.length ? (
            <ul>{result.strengths.map((skill) => <li key={skill}>{skill}</li>)}</ul>
          ) : (
            <p className="fast-check__empty-result">Backend chưa trả về kỹ năng phù hợp cụ thể.</p>
          )}
        </article>

        <article className="fast-check__analysis-card fast-check__analysis-card--warning">
          <div className="fast-check__analysis-title">
            <CircleAlert size={20} />
            <div><h4>Kỹ năng chưa được tìm thấy</h4><p>Các yêu cầu JD chưa được nhận diện trong CV</p></div>
          </div>
          {result.missingSkills.length ? (
            <ul>{result.missingSkills.map((skill) => <li key={skill}>{skill}</li>)}</ul>
          ) : (
            <p className="fast-check__empty-result">Backend không ghi nhận kỹ năng còn thiếu.</p>
          )}
          <p className="fast-check__disclaimer">“Chưa được tìm thấy” không có nghĩa là bạn chắc chắn không có kỹ năng này; CV có thể chưa thể hiện rõ.</p>
        </article>
      </div>

      {(result.advice || result.additionalAnalysis.length > 0) && (
        <article className="fast-check__advice">
          <div className="fast-check__advice-icon"><Lightbulb size={20} /></div>
          <div>
            <h4>Phân tích bổ sung</h4>
            {result.advice && <p>{result.advice}</p>}
            {result.additionalAnalysis.map((item) => (
              <p key={item.label}><strong>{item.label}:</strong> {item.value}</p>
            ))}
          </div>
        </article>
      )}
    </div>
  );
}

export default FastCheckResult;
