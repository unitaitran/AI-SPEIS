import React from 'react';
import { BriefcaseBusiness, GraduationCap, LogOut } from 'lucide-react';

function TechnicalInterviewHeader({
  t,
  jobRole,
  experienceLevel,
  status,
  canCompleteEarly = false,
  isCompleting = false,
  onComplete,
}) {
  return (
    <header className="technical-room-header technical-card">
      <div>
        <p className="technical-room-header__eyebrow">{t('result.eyebrow')}</p>
        <h1>{t('room.title')}</h1>
        <div className="technical-room-header__meta">
          {jobRole && (
            <span><BriefcaseBusiness size={16} aria-hidden="true" />{jobRole}</span>
          )}
          {experienceLevel && (
            <span><GraduationCap size={16} aria-hidden="true" />{experienceLevel}</span>
          )}
        </div>
      </div>
      <div className="technical-room-header__actions">
        {status && (
          <span className="technical-status-badge">
            {t(`room.statuses.${status}`, { defaultValue: status })}
          </span>
        )}
        {canCompleteEarly && (
          <button
            type="button"
            className="technical-secondary-button"
            onClick={onComplete}
            disabled={isCompleting}
          >
            <LogOut size={18} aria-hidden="true" />
            {isCompleting ? t('room.ending') : t('room.endEarly')}
          </button>
        )}
      </div>
    </header>
  );
}

export default TechnicalInterviewHeader;

