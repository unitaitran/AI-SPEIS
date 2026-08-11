import React from 'react';

export const FEEDBACK_CATEGORIES = [
  { value: 'Incorrect Score', labelKey: 'feedback.categoryOptions.incorrectScore' },
  { value: 'Incorrect Feedback', labelKey: 'feedback.categoryOptions.incorrectFeedback' },
  { value: 'Hallucination', labelKey: 'feedback.categoryOptions.hallucination' },
  { value: 'Bias', labelKey: 'feedback.categoryOptions.bias' },
  { value: 'Unclear Explanation', labelKey: 'feedback.categoryOptions.unclearExplanation' },
  { value: 'Grammar Issue', labelKey: 'feedback.categoryOptions.grammarIssue' },
  { value: 'Missing Feedback', labelKey: 'feedback.categoryOptions.missingFeedback' },
  { value: 'Offensive', labelKey: 'feedback.categoryOptions.offensive' },
  { value: 'Other', labelKey: 'feedback.categoryOptions.other' },
];

function CategoryChipGroup({ selectedCategories, onChange, disabled, error, t }) {
  const selected = Array.isArray(selectedCategories) ? selectedCategories : [];

  const toggleCategory = (categoryValue) => {
    if (selected.includes(categoryValue)) {
      onChange(selected.filter((item) => item !== categoryValue));
      return;
    }
    onChange([...selected, categoryValue]);
  };

  return (
    <fieldset className="feedback-fieldset" disabled={disabled} aria-invalid={error ? 'true' : 'false'}>
      <legend className="m-0 text-sm font-semibold text-[var(--text-primary)]">{t('feedback.categoryLabel')}</legend>
      <div className="mt-2 flex flex-wrap gap-2" role="group" aria-label={t('feedback.categoryGroupAria')}>
        {FEEDBACK_CATEGORIES.map((category) => {
          const isActive = selected.includes(category.value);
          return (
            <button
              key={category.value}
              type="button"
              className={`min-h-9 rounded-[var(--border-radius-pill)] border px-4 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-[var(--primary-light)] ${
                isActive
                  ? 'border-[var(--primary-dark)] bg-[var(--primary-xlight)] text-[var(--primary-dark)]'
                  : 'border-[var(--border)] bg-[var(--surface-2)] text-[var(--text-primary)] hover:border-[var(--primary-light)] hover:bg-[var(--surface-1)]'
              }`}
              onClick={() => toggleCategory(category.value)}
              aria-pressed={isActive}
            >
              {t(category.labelKey)}
            </button>
          );
        })}
      </div>
      {error ? <p className="mt-1 mb-0 text-xs text-[var(--error)]" role="alert">{error}</p> : null}
    </fieldset>
  );
}

export default CategoryChipGroup;
