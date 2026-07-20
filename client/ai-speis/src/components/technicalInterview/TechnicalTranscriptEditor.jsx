import React from 'react';

function TechnicalTranscriptEditor({ value, onChange, disabled, editable = true, t }) {
  return (
    <div className="technical-transcript">
      <label htmlFor="technical-transcript">{t('room.transcriptLabel')}</label>
      <textarea
        id="technical-transcript"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={t('room.transcriptPlaceholder')}
        disabled={disabled || !editable}
        aria-describedby="technical-transcript-helper"
      />
      <p id="technical-transcript-helper" className="technical-transcript__helper">
        {editable ? t('room.transcriptHelper') : t('room.transcriptReadOnly')}
      </p>
    </div>
  );
}

export default TechnicalTranscriptEditor;

