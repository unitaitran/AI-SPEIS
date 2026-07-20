import React from 'react';
import { AlertCircle, ArrowLeft, RefreshCw } from 'lucide-react';

function TechnicalInterviewErrorState({ title, message, onRetry, onBack, retryLabel, backLabel }) {
  return (
    <section className="technical-error-state technical-card" role="alert">
      <div className="technical-error-state__icon">
        <AlertCircle size={30} aria-hidden="true" />
      </div>
      <h2>{title}</h2>
      <p>{message}</p>
      <div className="technical-error-state__actions">
        {onRetry && (
          <button type="button" className="technical-secondary-button" onClick={onRetry}>
            <RefreshCw size={18} aria-hidden="true" />{retryLabel}
          </button>
        )}
        {onBack && (
          <button type="button" className="technical-secondary-button" onClick={onBack}>
            <ArrowLeft size={18} aria-hidden="true" />{backLabel}
          </button>
        )}
      </div>
    </section>
  );
}

export default TechnicalInterviewErrorState;
