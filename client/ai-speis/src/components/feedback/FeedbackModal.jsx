import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Flag, Loader2, X } from 'lucide-react';

export const FEEDBACK_REASONS = Object.freeze([
  { value: 'INCORRECT_SCORE', labelKey: 'feedback.reasonOptions.incorrectScore' },
  { value: 'INACCURATE_FEEDBACK', labelKey: 'feedback.reasonOptions.inaccurateFeedback' },
  { value: 'MISSING_CONTEXT', labelKey: 'feedback.reasonOptions.missingContext' },
  { value: 'HALLUCINATION', labelKey: 'feedback.reasonOptions.hallucination' },
  { value: 'BIAS_OR_UNFAIRNESS', labelKey: 'feedback.reasonOptions.biasOrUnfairness' },
  { value: 'UNCLEAR_EXPLANATION', labelKey: 'feedback.reasonOptions.unclearExplanation' },
  { value: 'OFFENSIVE_OR_INAPPROPRIATE', labelKey: 'feedback.reasonOptions.offensiveOrInappropriate' },
  { value: 'OTHER', labelKey: 'feedback.reasonOptions.other' },
]);

const defaultForm = (roundSessionId = '') => ({
  reason: '',
  explanation: '',
  roundSessionId: roundSessionId ? String(roundSessionId) : '',
});

function FeedbackModal({
  isOpen,
  onClose,
  onSubmit,
  isSubmitting,
  roundOptions = [],
  interviewSessionId,
  evaluationType = 'Behavioral',
  evaluationLabel,
  t,
}) {
  const [form, setForm] = useState(() => defaultForm(interviewSessionId));
  const [errors, setErrors] = useState({});
  const onCloseRef = useRef(onClose);

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  const selectableRounds = useMemo(() => roundOptions
    .map((round) => ({
      interviewSessionId: round?.interviewSessionId,
      evaluationType: round?.evaluationType,
      label: round?.label,
    }))
    .filter((round) => round.interviewSessionId && round.evaluationType && round.label), [roundOptions]);

  useEffect(() => {
    if (!isOpen) {
      setForm(defaultForm(interviewSessionId));
      setErrors({});
      return undefined;
    }

    setForm(defaultForm(interviewSessionId));
    const onEscape = (event) => {
      if (event.key === 'Escape' && !isSubmitting) onCloseRef.current();
    };
    document.addEventListener('keydown', onEscape);
    return () => document.removeEventListener('keydown', onEscape);
  }, [interviewSessionId, isOpen, isSubmitting]);

  if (!isOpen) return null;

  const validate = () => {
    const nextErrors = {};
    if (!form.roundSessionId) nextErrors.roundSessionId = t('feedback.validation.roundRequired');
    if (!form.reason) nextErrors.reason = t('feedback.validation.reasonRequired');
    if (form.explanation.trim().length < 10) nextErrors.explanation = t('feedback.validation.explanationLength');
    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!validate()) return;
    const selectedRound = selectableRounds.find((round) => String(round.interviewSessionId) === form.roundSessionId);
    await onSubmit({
      interviewSessionId: Number(form.roundSessionId),
      evaluationType: selectedRound?.evaluationType || evaluationType,
      reason: form.reason,
      explanation: form.explanation.trim(),
    });
  };

  const canSubmit = Boolean(form.roundSessionId && form.reason && form.explanation.trim().length >= 10);

  return (
    <div
      className="fixed inset-0 z-[2000] grid place-items-center bg-black/45 p-4 backdrop-blur-sm"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !isSubmitting) onClose();
      }}
    >
      <div
        className="max-h-[calc(100vh-32px)] w-full max-w-2xl overflow-auto rounded-lg border border-border bg-surface p-6 shadow-xl"
        role="dialog"
        aria-modal="true"
        aria-labelledby="feedback-modal-title"
        aria-describedby="feedback-modal-description"
      >
        <header className="mb-5 flex items-start justify-between gap-4">
          <div>
            <h2 id="feedback-modal-title" className="m-0 text-xl font-bold text-text-primary">{t('feedback.title')}</h2>
            <p id="feedback-modal-description" className="mb-0 mt-1.5 text-sm leading-6 text-text-secondary">{t('feedback.subtitle')}</p>
          </div>
          <button type="button" className="grid h-9 w-9 shrink-0 place-items-center rounded-md text-text-secondary hover:bg-surface-muted" onClick={onClose} disabled={isSubmitting} aria-label={t('feedback.cancel')}>
            <X size={18} />
          </button>
        </header>

        {evaluationLabel ? (
          <div className="mb-4 border-l-4 border-primary bg-primary-xlight px-4 py-3 text-sm text-text-primary">
            <span className="block text-xs font-bold uppercase text-primary-dark">{t('feedback.selectedEvaluation')}</span>
            <span className="mt-1 block line-clamp-2">{evaluationLabel}</span>
          </div>
        ) : null}

        <form className="grid gap-4" onSubmit={handleSubmit} noValidate>
          {selectableRounds.length > 0 ? (
            <div>
              <label className="mb-1.5 block text-sm font-semibold text-text-primary" htmlFor="feedback-round-select">{t('feedback.roundSelectorLabel')}</label>
              <select
                id="feedback-round-select"
                className="min-h-11 w-full rounded-md border border-border-strong bg-surface px-3 text-sm text-text-primary focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary-light"
                value={form.roundSessionId}
                onChange={(event) => setForm((previous) => ({ ...previous, roundSessionId: event.target.value }))}
                disabled={isSubmitting}
                aria-invalid={errors.roundSessionId ? 'true' : 'false'}
              >
                <option value="">{t('feedback.roundSelectorPlaceholder')}</option>
                {selectableRounds.map((round) => <option key={round.interviewSessionId} value={round.interviewSessionId}>{round.label}</option>)}
              </select>
              {errors.roundSessionId ? <p className="mb-0 mt-1 text-xs text-error" role="alert">{errors.roundSessionId}</p> : null}
            </div>
          ) : null}

          <div>
            <label className="mb-1.5 block text-sm font-semibold text-text-primary" htmlFor="feedback-reason-select">{t('feedback.reasonLabel')}</label>
            <select
              id="feedback-reason-select"
              className="min-h-11 w-full rounded-md border border-border-strong bg-surface px-3 text-sm text-text-primary focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary-light"
              value={form.reason}
              onChange={(event) => setForm((previous) => ({ ...previous, reason: event.target.value }))}
              disabled={isSubmitting}
              aria-invalid={errors.reason ? 'true' : 'false'}
            >
              <option value="">{t('feedback.reasonPlaceholder')}</option>
              {FEEDBACK_REASONS.map((reason) => <option key={reason.value} value={reason.value}>{t(reason.labelKey)}</option>)}
            </select>
            <p className="mb-0 mt-1 text-xs text-text-secondary">{t('feedback.reasonHelper')}</p>
            {errors.reason ? <p className="mb-0 mt-1 text-xs text-error" role="alert">{errors.reason}</p> : null}
          </div>

          <div>
            <div className="mb-1.5 flex items-center justify-between gap-3">
              <label className="text-sm font-semibold text-text-primary" htmlFor="feedback-explanation">{t('feedback.explanationLabel')}</label>
              <span className="text-xs text-text-muted">{form.explanation.length}/1000</span>
            </div>
            <textarea
              id="feedback-explanation"
              className="min-h-32 w-full resize-y rounded-md border border-border-strong bg-surface px-3 py-2.5 text-sm leading-6 text-text-primary focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-primary-light"
              value={form.explanation}
              onChange={(event) => setForm((previous) => ({ ...previous, explanation: event.target.value.slice(0, 1000) }))}
              placeholder={t('feedback.explanationPlaceholder')}
              disabled={isSubmitting}
              aria-invalid={errors.explanation ? 'true' : 'false'}
            />
            {errors.explanation ? <p className="mb-0 mt-1 text-xs text-error" role="alert">{errors.explanation}</p> : null}
          </div>

          <footer className="mt-1 flex flex-col-reverse gap-2 border-t border-border pt-4 sm:flex-row sm:justify-end">
            <button type="button" className="min-h-10 rounded-md border border-border bg-surface px-4 text-sm font-semibold text-text-primary hover:bg-surface-muted" onClick={onClose} disabled={isSubmitting}>{t('feedback.cancel')}</button>
            <button type="submit" className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-primary px-4 text-sm font-semibold text-white hover:bg-primary-hover disabled:cursor-not-allowed disabled:opacity-50" disabled={!canSubmit || isSubmitting}>
              {isSubmitting ? <Loader2 size={16} className="animate-spin" /> : <Flag size={16} />}
              {isSubmitting ? t('feedback.submitting') : t('feedback.submit')}
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
}

export default FeedbackModal;
