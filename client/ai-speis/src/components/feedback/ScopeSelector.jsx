import React from 'react';

export const FEEDBACK_SCOPES = [
  { value: 'OVERALL_EVALUATION', labelKey: 'feedback.scopeOptions.overallEvaluation' },
  { value: 'OVERALL_SCORE', labelKey: 'feedback.scopeOptions.overallScore' },
  { value: 'SPECIFIC_QUESTION', labelKey: 'feedback.scopeOptions.specificQuestion' },
  { value: 'SPECIFIC_FEEDBACK', labelKey: 'feedback.scopeOptions.specificFeedback' },
];

function ScopeSelector({ value, onChange, disabled, error, t }) {
  return (
    <fieldset className="feedback-fieldset" disabled={disabled} aria-invalid={error ? 'true' : 'false'}>
      <legend className="m-0 text-sm font-semibold text-[var(--text-primary)]">{t('feedback.scopeLabel')}</legend>
      <p className="mt-1 mb-0 text-xs text-[var(--text-secondary)]">{t('feedback.scopeHelper')}</p>
      <div className="mt-2 grid gap-2" role="radiogroup" aria-label={t('feedback.scopeHelper')}>
        {FEEDBACK_SCOPES.map((scope) => (
          <label
            key={scope.value}
            className={`flex cursor-pointer items-center gap-2 rounded-[var(--border-radius)] border px-4 py-2 transition-colors ${
              value === scope.value
                ? 'border-[var(--primary-dark)] bg-[var(--primary-xlight)]'
                : 'border-[var(--border)] bg-[var(--surface-1)]'
            }`}
          >
            <input
              type="radio"
              name="feedback-scope"
              value={scope.value}
              checked={value === scope.value}
              onChange={(event) => onChange(event.target.value)}
              className="m-0 h-4 w-4 accent-[var(--primary-dark)]"
            />
            <span className="text-sm text-[var(--text-primary)]">{t(scope.labelKey)}</span>
          </label>
        ))}
      </div>
      {error ? <p className="mt-1 mb-0 text-xs text-[var(--error)]" role="alert">{error}</p> : null}
    </fieldset>
  );
}

export default ScopeSelector;
