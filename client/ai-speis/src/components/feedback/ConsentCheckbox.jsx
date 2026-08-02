import React from 'react';

function ConsentCheckbox({ checked, onChange, disabled, error, t }) {
  return (
    <div>
      <label className="flex cursor-pointer items-start gap-2" htmlFor="feedback-consent-checkbox">
        <input
          id="feedback-consent-checkbox"
          type="checkbox"
          checked={checked}
          disabled={disabled}
          onChange={(event) => onChange(event.target.checked)}
          aria-invalid={error ? 'true' : 'false'}
          className="mt-1 h-4 w-4 accent-[var(--primary-dark)]"
        />
        <span className="text-xs leading-5 text-[var(--text-primary)]">{t('feedback.consentLabel')}</span>
      </label>
      {error ? <p className="mt-1 mb-0 text-xs text-[var(--error)]" role="alert">{error}</p> : null}
    </div>
  );
}

export default ConsentCheckbox;
