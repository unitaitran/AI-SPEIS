import React from 'react';

function CommentBox({ value, onChange, disabled, maxLength = 1000, error, t }) {
  const currentLength = value?.length || 0;

  return (
    <div>
      <label className="m-0 text-sm font-semibold text-[var(--text-primary)]" htmlFor="feedback-comment">{t('feedback.commentLabel')}</label>
      <textarea
        id="feedback-comment"
        name="comment"
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value.slice(0, maxLength))}
        placeholder={t('feedback.commentPlaceholder')}
        maxLength={maxLength}
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby="feedback-comment-count"
        className="mt-2 w-full resize-y rounded-[var(--border-radius-md)] border border-[var(--border-strong)] bg-[var(--surface-2)] p-4 text-sm leading-6 text-[var(--text-primary)] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[var(--primary-light)] disabled:bg-[var(--surface-1)] disabled:text-[var(--text-secondary)]"
      />
      <div className="mt-1 flex justify-end text-xs text-[var(--text-secondary)]" id="feedback-comment-count" aria-live="polite">
        <span>{currentLength}/{maxLength}</span>
      </div>
      {error ? <p className="mt-1 mb-0 text-xs text-[var(--error)]" role="alert">{error}</p> : null}
    </div>
  );
}

export default CommentBox;
