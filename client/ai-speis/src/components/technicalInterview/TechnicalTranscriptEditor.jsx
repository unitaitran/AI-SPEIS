import React from 'react';

function TechnicalTranscriptEditor({ value, onChange, disabled, editable = true, t }) {
  const isReadOnly = disabled || !editable;
  return (
    <div className="technical-transcript">
      <label htmlFor="technical-transcript">{t('room.transcriptLabel')}</label>
      <textarea
        id="technical-transcript"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={t('room.transcriptPlaceholder')}
        readOnly={isReadOnly}
        aria-readonly={isReadOnly}
        aria-describedby="technical-transcript-helper"
      />
      <p id="technical-transcript-helper" className="technical-transcript__helper">
        {isReadOnly ? t('room.transcriptReadOnly') : t('room.transcriptHelper')}
      </p>
    </div>
  );
}

export default TechnicalTranscriptEditor;

