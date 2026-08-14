import React, { useEffect, useMemo, useState } from 'react';
import { Flag, Loader2 } from 'lucide-react';
import ScopeSelector from './ScopeSelector';
import CategoryChipGroup from './CategoryChipGroup';
import CommentBox from './CommentBox';
import ConsentCheckbox from './ConsentCheckbox';

const DEFAULT_FORM = {
  scope: '',
  categories: [],
  questionId: '',
  comment: '',
  consent: false,
};

function FeedbackModal({
  isOpen,
  onClose,
  onSubmit,
  isSubmitting,
  questions,
  interviewSessionId,
  evaluationId,
  t,
}) {
  const [form, setForm] = useState(DEFAULT_FORM);
  const [errors, setErrors] = useState({});

  useEffect(() => {
    if (!isOpen) {
      setForm(DEFAULT_FORM);
      setErrors({});
      return;
    }

    const onEscape = (event) => {
      if (event.key === 'Escape' && !isSubmitting) onClose();
    };

    document.addEventListener('keydown', onEscape);
    return () => document.removeEventListener('keydown', onEscape);
  }, [isOpen, isSubmitting, onClose]);

  const questionOptions = useMemo(() => {
    const source = Array.isArray(questions) ? questions : [];
    return source.map((question, index) => ({
      id: question?.id,
      label: question?.label || t('feedback.questionItem', { index: index + 1 }),
    })).filter((question) => question.id !== undefined && question.id !== null && question.id !== '');
  }, [questions, t]);

  const validate = () => {
    const nextErrors = {};

    if (!form.scope) nextErrors.scope = t('feedback.validation.scopeRequired');
    if (!form.categories.length) nextErrors.categories = t('feedback.validation.categoryRequired');
    if (form.scope === 'SPECIFIC_QUESTION' && !form.questionId) nextErrors.questionId = t('feedback.validation.questionRequired');
    if (!form.comment.trim()) nextErrors.comment = t('feedback.validation.commentRequired');
    if (!form.consent) nextErrors.consent = t('feedback.validation.consentRequired');

    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const isValid = Boolean(
    form.scope
    && form.categories.length > 0
    && form.comment.trim()
    && form.consent
    && (form.scope !== 'SPECIFIC_QUESTION' || form.questionId),
  );

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!validate()) return;

    await onSubmit({
      interviewSessionId,
      evaluationId,
      scope: form.scope,
      categories: form.categories,
      questionId: form.scope === 'SPECIFIC_QUESTION' ? form.questionId : null,
      comment: form.comment.trim(),
    });
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-[2000] grid place-items-center bg-black/45 p-4 backdrop-blur-sm"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !isSubmitting) onClose();
      }}
    >
      <div
        className="max-h-[calc(100vh-32px)] w-full max-w-3xl overflow-auto rounded-[var(--border-radius-lg)] border border-[var(--border)] bg-[var(--surface-2)] p-6 shadow-[var(--box-shadow-lg)]"
        role="dialog"
        aria-modal="true"
        aria-labelledby="feedback-modal-title"
        aria-describedby="feedback-modal-description"
      >
        <header className="mb-6">
          <h2 id="feedback-modal-title" className="m-0 text-2xl font-semibold leading-8 text-[var(--text-primary)]">{t('feedback.title')}</h2>
          <p id="feedback-modal-description" className="mt-2 mb-0 text-sm leading-6 text-[var(--text-secondary)]">
            {t('feedback.subtitle')}
          </p>
        </header>

        <form className="grid gap-4" onSubmit={handleSubmit} noValidate>
          <fieldset disabled={isSubmitting} className="feedback-fieldset">
            <ScopeSelector
              value={form.scope}
              onChange={(scope) => {
                setForm((prev) => ({
                  ...prev,
                  scope,
                  questionId: scope === 'SPECIFIC_QUESTION' ? prev.questionId : '',
                }));
              }}
              disabled={isSubmitting}
              error={errors.scope}
              t={t}
            />

            <CategoryChipGroup
              selectedCategories={form.categories}
              onChange={(categories) => setForm((prev) => ({ ...prev, categories }))}
              disabled={isSubmitting}
              error={errors.categories}
              t={t}
            />

            {form.scope === 'SPECIFIC_QUESTION' ? (
              <div>
                <label className="mb-1 block text-sm font-semibold text-[var(--text-primary)]" htmlFor="feedback-question-select">{t('feedback.questionSelectorLabel')}</label>
                <select
                  id="feedback-question-select"
                  className="min-h-[42px] w-full rounded-[var(--border-radius-md)] border border-[var(--border-strong)] bg-[var(--surface-2)] px-4 text-sm text-[var(--text-primary)] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[var(--primary-light)]"
                  value={form.questionId}
                  onChange={(event) => setForm((prev) => ({ ...prev, questionId: event.target.value }))}
                  aria-invalid={errors.questionId ? 'true' : 'false'}
                >
                  <option value="">{t('feedback.questionSelectorPlaceholder')}</option>
                  {questionOptions.map((question) => (
                    <option key={question.id} value={question.id}>{question.label}</option>
                  ))}
                </select>
                {errors.questionId ? <p className="mt-1 mb-0 text-xs text-[var(--error)]" role="alert">{errors.questionId}</p> : null}
              </div>
            ) : null}

            <CommentBox
              value={form.comment}
              onChange={(comment) => setForm((prev) => ({ ...prev, comment }))}
              disabled={isSubmitting}
              error={errors.comment}
              maxLength={1000}
              t={t}
            />

            <ConsentCheckbox
              checked={form.consent}
              onChange={(consent) => setForm((prev) => ({ ...prev, consent }))}
              disabled={isSubmitting}
              error={errors.consent}
              t={t}
            />
          </fieldset>

          <footer className="mt-1 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <button
              type="button"
              className="min-h-[42px] rounded-[var(--border-radius-md)] border border-[var(--border)] bg-[var(--surface-2)] px-4 text-sm font-semibold text-[var(--text-primary)] transition-colors hover:bg-[var(--surface-1)] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[var(--primary-light)] disabled:cursor-not-allowed disabled:bg-[var(--surface-1)] disabled:text-[var(--text-disabled)]"
              onClick={onClose}
              disabled={isSubmitting}
            >
              {t('feedback.cancel')}
            </button>
            <button
              type="submit"
              className="min-h-[42px] rounded-[var(--border-radius-md)] border border-[var(--primary-dark)] bg-[var(--primary-dark)] px-4 text-sm font-semibold text-[var(--surface-2)] transition-colors hover:bg-[var(--primary)] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[var(--primary-light)] disabled:cursor-not-allowed disabled:border-[var(--border-strong)] disabled:bg-[var(--border-strong)]"
              disabled={!isValid || isSubmitting}
            >
              {isSubmitting ? <Loader2 size={16} className="inline-block animate-spin" /> : <Flag size={16} className="inline-block" />}
              {isSubmitting ? t('feedback.submitting') : t('feedback.submit')}
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
}

export default FeedbackModal;
