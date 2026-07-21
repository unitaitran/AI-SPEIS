import React from 'react';
import { Loader2 } from 'lucide-react';

function TechnicalEvaluationState({ transcript, compact = false, t }) {
  return (
    <section
      className={`technical-evaluation technical-card${compact ? ' technical-evaluation--compact' : ''}`}
      aria-live="polite"
      aria-busy="true"
    >
      <div className="technical-evaluation__icon">
        <Loader2 size={30} className="animate-spin" aria-hidden="true" />
      </div>
      <h2>{t('room.evaluatingTitle')}</h2>
      <p>{t('room.evaluatingDescription')}</p>
      {transcript && (
        <div className="technical-processing-transcript">
          <h3>{t('room.processingTranscript')}</h3>
          <p>{transcript}</p>
        </div>
      )}
    </section>
  );
}

export default TechnicalEvaluationState;

