import React from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, CircleAlert, Lightbulb } from 'lucide-react';
import './FastCheckPanel.css';

function FastCheckResult({ result }) {
  const { t } = useTranslation('cvjd');
  return (
    <div className="fast-check__result" aria-live="polite">
      <div className="fast-check__result-heading">
        <div>
          <p className="fast-check__eyebrow">{t('fastCheckResult.eyebrow')}</p>
          <h3>{t('fastCheckResult.title')}</h3>
        </div>
        <span className="fast-check__result-badge"><CheckCircle2 size={14} /> {t('fastCheckResult.completed')}</span>
      </div>

      <div className="fast-check__score-card">
        <div
          className="fast-check__score-ring"
          style={{ '--fast-check-score': `${result.score * 3.6}deg` }}
          role="img"
          aria-label={t('fastCheckResult.scoreAria', { score: result.score })}
        >
          <div><strong>{result.score}</strong><span>/100</span></div>
        </div>
        <div className="fast-check__score-copy">
          <span>{t('fastCheckResult.overallScore')}</span>
          {result.suitabilityLevel && <h4>{result.suitabilityLevel}</h4>}
          <p>{t('fastCheckResult.scoreDescription')}</p>
          <div className="fast-check__score-track" aria-hidden="true">
            <span style={{ width: `${result.score}%` }} />
          </div>
        </div>
      </div>

      <div className="fast-check__analysis-grid">
        <article className="fast-check__analysis-card fast-check__analysis-card--success">
          <div className="fast-check__analysis-title">
            <CheckCircle2 size={20} />
            <div><h4>{t('fastCheckResult.strengthsTitle')}</h4><p>{t('fastCheckResult.strengthsDescription')}</p></div>
          </div>
          {result.strengths.length ? (
            <ul>{result.strengths.map((skill) => <li key={skill}>{skill}</li>)}</ul>
          ) : (
            <p className="fast-check__empty-result">{t('fastCheckResult.noStrengths')}</p>
          )}
        </article>

        <article className="fast-check__analysis-card fast-check__analysis-card--warning">
          <div className="fast-check__analysis-title">
            <CircleAlert size={20} />
            <div><h4>{t('fastCheckResult.missingSkillsTitle')}</h4><p>{t('fastCheckResult.missingSkillsDescription')}</p></div>
          </div>
          {result.missingSkills.length ? (
            <ul>{result.missingSkills.map((skill) => <li key={skill}>{skill}</li>)}</ul>
          ) : (
            <p className="fast-check__empty-result">{t('fastCheckResult.noMissingSkills')}</p>
          )}
          <p className="fast-check__disclaimer">{t('fastCheckResult.disclaimer')}</p>
        </article>
      </div>

      {(result.advice || result.additionalAnalysis.length > 0) && (
        <article className="fast-check__advice">
          <div className="fast-check__advice-icon"><Lightbulb size={20} /></div>
          <div>
            <h4>{t('fastCheckResult.additionalAnalysis')}</h4>
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
