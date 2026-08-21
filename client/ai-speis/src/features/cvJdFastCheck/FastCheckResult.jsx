import React from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, CircleAlert, Lightbulb, Check } from 'lucide-react';
import './FastCheckPanel.css';

function FastCheckResult({ result }) {
  const { t } = useTranslation('cvjd');

  const getMascotConfig = (score) => {
    if (score >= 80) {
      return {
        src: '/happy.png',
        alt: 'AI-SPEIS Happy Mascot',
      };
    }
    if (score >= 50) {
      return {
        src: '/ideaing_mascot.jpg',
        alt: 'AI-SPEIS Ideaing Mascot',
      };
    }
    return {
      src: '/fighting_mascot.jpg',
      alt: 'AI-SPEIS Fighting Mascot',
    };
  };

  const mascot = getMascotConfig(result.score || 0);

  return (
    <div className="fast-check__result" aria-live="polite">
      {/* Header */}
      <div className="fast-check__result-heading">
        <div>
          <p className="fast-check__eyebrow">{t('analysisResult', 'KẾT QUẢ PHÂN TÍCH')}</p>
          <h3>{t('matchSuitability', 'Mức độ phù hợp CV với JD')}</h3>
        </div>
        <span className="fast-check__result-badge">
          <CheckCircle2 size={14} /> {t('completed', 'Hoàn tất')}
        </span>
      </div>

      {/* Overall Score Card with Mascot */}
      <div className="fast-check__score-card">
        <div
          className="fast-check__score-ring"
          style={{ '--fast-check-score': `${result.score * 3.6}deg` }}
          role="img"
          aria-label={`Match Score ${result.score}/100`}
        >
          <div>
            <strong>{result.score}</strong>
            <span>/100</span>
          </div>
        </div>

        <div className="fast-check__score-copy">
          <span className="fast-check__score-label">{t('overallMatchScore', 'OVERALL MATCH SCORE')}</span>
          {result.suitabilityLevel && <h4>{result.suitabilityLevel}</h4>}
          <p>{t('scoreExplanation', 'Điểm do AI đối chiếu và đánh giá dựa trên dữ liệu trích xuất từ CV và JD của bạn.')}</p>
          <div className="fast-check__score-track" aria-hidden="true">
            <span style={{ width: `${result.score}%` }} />
          </div>
        </div>

        {/* Mascot Avatar */}
        <div className="fast-check__mascot-box">
          <div className="fast-check__mascot-img-wrap">
            <img src={mascot.src} alt={mascot.alt} className="fast-check__mascot-img" />
          </div>
        </div>
      </div>

      {/* Analysis Grid (Strengths & Missing Skills) */}
      <div className="fast-check__analysis-grid">
        <article className="fast-check__analysis-card fast-check__analysis-card--success">
          <div className="fast-check__analysis-title">
            <div className="fast-check__title-icon fast-check__title-icon--success">
              <CheckCircle2 size={18} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h4>{t('matchingSkillsTitle', 'Điểm phù hợp')}</h4>
                {result.strengths?.length > 0 && (
                  <span className="fast-check__count-badge fast-check__count-badge--success">
                    {result.strengths.length}
                  </span>
                )}
              </div>
              <p>{t('matchingSkillsSubtitle', 'Kỹ năng tìm thấy ở cả CV và JD')}</p>
            </div>
          </div>
          {result.strengths?.length ? (
            <ul className="fast-check__skill-list">
              {result.strengths.map((skill) => (
                <li key={skill} className="fast-check__skill-item fast-check__skill-item--success">
                  <Check size={12} className="fast-check__check-icon" />
                  <span>{skill}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="fast-check__empty-result">{t('noMatchingSkills', 'Chưa ghi nhận kỹ năng phù hợp cụ thể.')}</p>
          )}
        </article>

        <article className="fast-check__analysis-card fast-check__analysis-card--warning">
          <div className="fast-check__analysis-title">
            <div className="fast-check__title-icon fast-check__title-icon--warning">
              <CircleAlert size={18} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h4>{t('missingSkillsTitle', 'Kỹ năng chưa được tìm thấy')}</h4>
                {result.missingSkills?.length > 0 && (
                  <span className="fast-check__count-badge fast-check__count-badge--warning">
                    {result.missingSkills.length}
                  </span>
                )}
              </div>
              <p>{t('missingSkillsSubtitle', 'Các yêu cầu JD chưa được nhận diện trong CV')}</p>
            </div>
          </div>
          {result.missingSkills?.length ? (
            <ul className="fast-check__skill-list">
              {result.missingSkills.map((skill) => (
                <li key={skill} className="fast-check__skill-item fast-check__skill-item--warning">
                  <span>{skill}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="fast-check__empty-result">{t('noMissingSkills', 'Tuyệt vời! Không ghi nhận kỹ năng còn thiếu.')}</p>
          )}
          <p className="fast-check__disclaimer">
            {t('missingSkillsDisclaimer', '* “Chưa được tìm thấy” không đồng nghĩa bạn không có kỹ năng này; CV có thể chưa ghi rõ.')}
          </p>
        </article>
      </div>

      {/* Additional Advice Section */}
      {(result.advice || result.additionalAnalysis?.length > 0) && (
        <article className="fast-check__advice">
          <div className="fast-check__advice-icon">
            <Lightbulb size={20} />
          </div>
          <div className="fast-check__advice-content">
            <h4>{t('detailedAdviceTitle', 'Phân tích chi tiết & Lời khuyên')}</h4>
            {result.advice && <p className="fast-check__advice-text">{result.advice}</p>}
            {result.additionalAnalysis?.map((item) => (
              <p key={item.label} className="fast-check__advice-meta">
                <strong>{item.label}:</strong> {item.value}
              </p>
            ))}
          </div>
        </article>
      )}
    </div>
  );
}

export default FastCheckResult;

