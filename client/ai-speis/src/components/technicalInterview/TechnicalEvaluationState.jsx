import React from 'react';
import { Loader2 } from 'lucide-react';

function TechnicalEvaluationState({ t }) {
  return (
    <section className="technical-evaluation technical-card" aria-live="polite">
      <div className="technical-evaluation__icon">
        <Loader2 size={30} className="animate-spin" aria-hidden="true" />
      </div>
      <h2>{t('room.evaluatingTitle')}</h2>
      <p>{t('room.evaluatingDescription')}</p>
    </section>
  );
}

export default TechnicalEvaluationState;

