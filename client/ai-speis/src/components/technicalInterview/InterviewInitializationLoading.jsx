import React from 'react';
import { Loader2, Sparkles } from 'lucide-react';

function InterviewInitializationLoading({ phase = 'initializingSession', t }) {
  const isGenerating = phase === 'generatingQuestion' || phase === 'generatingNextQuestion';

  return (
    <section className="technical-initialization technical-card" aria-live="polite" aria-busy="true">
      <div className="technical-initialization__icon" aria-hidden="true">
        {isGenerating ? <Sparkles size={30} /> : <Loader2 size={30} className="animate-spin" />}
      </div>
      <div>
        <p className="technical-section__eyebrow">AI-SPEIS</p>
        <h2>{isGenerating ? t('room.generatingQuestionTitle') : t('room.initializingTitle')}</h2>
        <p>
          {isGenerating
            ? t('room.generatingQuestionDescription')
            : t('room.initializingDescription')}
        </p>
        <span>{t('room.initializingHint')}</span>
      </div>
      <div className="technical-initialization__skeleton" aria-hidden="true">
        <i />
        <i />
        <i />
      </div>
    </section>
  );
}

export default InterviewInitializationLoading;
