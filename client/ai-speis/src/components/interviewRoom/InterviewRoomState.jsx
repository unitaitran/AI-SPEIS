import React from 'react';
import { AlertCircle, ArrowLeft, Loader2, LogOut, RefreshCw, Sparkles } from 'lucide-react';

function InterviewRoomState({
  backLabel,
  description,
  hint,
  isGenerating = false,
  onBack,
  onEnd,
  onRetry,
  endLabel,
  retryLabel,
  title,
  variant = 'loading',
}) {
  if (variant === 'error') {
    return (
      <section className="technical-error-state technical-card" role="alert">
        <div className="technical-error-state__icon"><AlertCircle size={30} aria-hidden="true" /></div>
        <h2>{title}</h2>
        <p>{description}</p>
        <div className="technical-error-state__actions">
          {onRetry ? (
            <button type="button" className="technical-secondary-button" onClick={onRetry}>
              <RefreshCw size={18} aria-hidden="true" />{retryLabel}
            </button>
          ) : null}
          {onBack ? (
            <button type="button" className="technical-secondary-button" onClick={onBack}>
              <ArrowLeft size={18} aria-hidden="true" />{backLabel}
            </button>
          ) : null}
          {onEnd ? (
            <button type="button" className="technical-secondary-button" onClick={onEnd}>
              <LogOut size={18} aria-hidden="true" />{endLabel}
            </button>
          ) : null}
        </div>
      </section>
    );
  }

  return (
    <section className="technical-initialization technical-card" aria-live="polite" aria-busy="true">
      <div className="technical-initialization__icon" aria-hidden="true">
        {isGenerating ? <Sparkles size={30} /> : <Loader2 size={30} className="animate-spin" />}
      </div>
      <div>
        <p className="technical-section__eyebrow">AI-SPEIS</p>
        <h2>{title}</h2>
        <p>{description}</p>
        {hint ? <span>{hint}</span> : null}
      </div>
      <div className="technical-initialization__skeleton" aria-hidden="true"><i /><i /><i /></div>
    </section>
  );
}

export default InterviewRoomState;
