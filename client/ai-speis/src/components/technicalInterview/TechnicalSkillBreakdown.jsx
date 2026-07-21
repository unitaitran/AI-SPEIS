import React from 'react';
import { getScorePercentage } from '../../features/technicalInterview/technicalInterviewResult';
import TechnicalResultFeedbackList from './TechnicalResultFeedbackList';

function TechnicalSkillBreakdown({ skills, t }) {
  return (
    <section className="technical-result-section technical-card" aria-labelledby="technical-skills-title">
      <div className="technical-section__header">
        <div>
          <p className="technical-section__eyebrow">{t('result.skillsEyebrow')}</p>
          <h2 id="technical-skills-title">{t('result.skillResults')}</h2>
        </div>
      </div>
      {Array.isArray(skills) && skills.length > 0 ? (
        <div className="technical-skill-grid">
          {skills.map((skill, index) => {
            const percentage = getScorePercentage(skill.score, skill.maxScore);
            return (
              <article className="technical-skill-card" key={skill.skillCode || skill.skill || skill.name || index}>
                <div className="technical-skill-card__header">
                  <h3>{skill.skill || skill.name}</h3>
                  <span className="technical-score-inline">
                    {skill.score ?? t('result.notAvailable')}
                    {skill.maxScore != null && <small> / {skill.maxScore}</small>}
                  </span>
                </div>
                {percentage != null && (
                  <div
                    className="technical-score-bar"
                    role="progressbar"
                    aria-label={t('result.dimensionScoreAlternative', {
                      name: skill.skill || skill.name,
                      score: skill.score,
                      maxScore: skill.maxScore,
                    })}
                    aria-valuemin={0}
                    aria-valuemax={skill.maxScore}
                    aria-valuenow={skill.score}
                  >
                    <div className="technical-score-bar__fill" style={{ width: `${percentage}%` }} />
                  </div>
                )}
                {skill.level && <p className="technical-skill-card__level"><strong>{skill.level}</strong></p>}
                <div className="technical-feedback-columns">
                  <TechnicalResultFeedbackList title={t('result.strengths')} items={skill.strengths} />
                  <TechnicalResultFeedbackList title={t('result.gaps')} items={skill.gaps || skill.missingEvidence} />
                  <TechnicalResultFeedbackList title={t('result.suggestions')} items={skill.suggestions || skill.improvementSuggestions} />
                </div>
              </article>
            );
          })}
        </div>
      ) : (
        <p className="technical-empty-copy">{t('result.noSkills')}</p>
      )}
    </section>
  );
}

export default TechnicalSkillBreakdown;

