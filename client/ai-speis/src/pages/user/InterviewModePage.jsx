import React, { useState } from 'react';
import { ArrowLeft, ArrowRight, Check, Lightbulb, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import InterviewProgressStepper from '../../components/user/InterviewProgressStepper/InterviewProgressStepper';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import { getInterviewSetupDraft, saveInterviewSetupDraft } from '../../utils/interviewContext';
import '../../styles/user/InterviewModePage.css';

const VALID_MODES = new Set(['Practice', 'RealTest']);

function InterviewModePage() {
  const { t } = useTranslation('interview');
  const storedMode = getInterviewSetupDraft()?.mode;
  const [mode, setMode] = useState(VALID_MODES.has(storedMode) ? storedMode : '');
  const [error, setError] = useState('');

  const selectMode = (nextMode) => {
    setMode(nextMode);
    setError('');
  };

  const handleContinue = () => {
    if (!VALID_MODES.has(mode)) {
      setError(t('mode.required'));
      return;
    }

    const currentDraft = getInterviewSetupDraft() || {};
    saveInterviewSetupDraft({ ...currentDraft, mode });
    navigate(USER_ROUTES.INTERVIEW_SETUP);
  };

  return (
    <UserLayout>
      <div className="interview-mode-page animate-pageEntrance">
        <header className="interview-mode-header">
          <span>{t('mode.eyebrow')}</span>
          <h1>{t('mode.title')}</h1>
          <p>{t('mode.subtitle')}</p>
        </header>

        <InterviewProgressStepper activeStep={0} />

        <section className="interview-mode-panel" aria-labelledby="interview-mode-title">
          <div className="interview-mode-panel-heading">
            <h2 id="interview-mode-title">{t('mode.question')}</h2>
            <p>{t('mode.description')}</p>
          </div>

          <fieldset className="interview-mode-options">
            <legend className="interview-mode-sr-only">{t('mode.legend')}</legend>

            <label className={`interview-mode-card${mode === 'Practice' ? ' interview-mode-card--selected' : ''}`}>
              <input
                type="radio"
                name="interview-mode"
                value="Practice"
                checked={mode === 'Practice'}
                onChange={() => selectMode('Practice')}
              />
              <span className="interview-mode-card-icon" aria-hidden="true"><Lightbulb size={24} /></span>
              <span className="interview-mode-radio" aria-hidden="true">{mode === 'Practice' && <Check size={14} />}</span>
              <strong>{t('mode.practice')}</strong>
              <p>{t('mode.practiceDescription')}</p>
            </label>

            <label className={`interview-mode-card${mode === 'RealTest' ? ' interview-mode-card--selected' : ''}`}>
              <input
                type="radio"
                name="interview-mode"
                value="RealTest"
                checked={mode === 'RealTest'}
                onChange={() => selectMode('RealTest')}
              />
              <span className="interview-mode-card-icon" aria-hidden="true"><ShieldCheck size={24} /></span>
              <span className="interview-mode-radio" aria-hidden="true">{mode === 'RealTest' && <Check size={14} />}</span>
              <strong>{t('mode.realTest')}</strong>
              <p>{t('mode.realTestDescription')}</p>
            </label>
          </fieldset>

          {error && <div className="interview-mode-error" role="alert">{error}</div>}

          <div className="interview-mode-actions">
            <button type="button" className="interview-mode-secondary" onClick={() => navigate(USER_ROUTES.DASHBOARD)}>
              <ArrowLeft size={18} />
              {t('common.back')}
            </button>
            <button type="button" className="interview-mode-primary" onClick={handleContinue}>
              {t('common.continue')}
              <ArrowRight size={20} />
            </button>
          </div>
        </section>
      </div>
    </UserLayout>
  );
}

export default InterviewModePage;
