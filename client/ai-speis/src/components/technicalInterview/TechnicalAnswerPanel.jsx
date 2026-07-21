import React from 'react';
import { AlertCircle, Send } from 'lucide-react';
import Button from '../UI/Button';
import TechnicalRecorder from './TechnicalRecorder';
import TechnicalTranscriptEditor from './TechnicalTranscriptEditor';

function TechnicalAnswerPanel({
  recorder,
  transcriptEditable,
  disabled,
  isSubmitting,
  errorMessage,
  onSubmit,
  showTranscriptEditor = true,
  stageMode = false,
  t,
}) {
  return (
    <section className={`technical-answer-panel technical-card${stageMode ? ' technical-answer-panel--stage' : ''}`} aria-labelledby="technical-answer-title">
      <div className={`technical-answer-panel__header${stageMode ? ' technical-visually-hidden' : ''}`}>
        <div>
          <p className="technical-section__eyebrow">{t('room.yourResponse')}</p>
          <h2 id="technical-answer-title">{t('room.answerTitle')}</h2>
        </div>
      </div>
      <TechnicalRecorder recorder={recorder} disabled={disabled || isSubmitting} t={t} />
      {(recorder.permissionError || recorder.sttError) && (
        <p className="technical-inline-error" role="alert">
          <AlertCircle size={18} aria-hidden="true" />
          {recorder.permissionError ? t('room.microphoneError') : t('room.sttError')}
        </p>
      )}
      {showTranscriptEditor && (
        <TechnicalTranscriptEditor
          value={recorder.transcript}
          onChange={recorder.setTranscript}
          disabled={disabled || isSubmitting}
          editable={transcriptEditable}
          t={t}
        />
      )}
      {errorMessage && (
        <p className="technical-inline-error" role="alert">
          <AlertCircle size={18} aria-hidden="true" />
          {errorMessage}
        </p>
      )}
      <div className="technical-answer-panel__submit">
        <Button
          type="button"
          onClick={onSubmit}
          disabled={disabled || isSubmitting || !recorder.transcript.trim()}
        >
          <Send size={18} aria-hidden="true" />
          {isSubmitting ? t('room.submitting') : t('room.submitAnswer')}
        </Button>
      </div>
    </section>
  );
}

export default TechnicalAnswerPanel;

