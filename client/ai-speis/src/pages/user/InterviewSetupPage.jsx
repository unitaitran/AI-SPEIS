import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  BriefcaseBusiness,
  Check,
  FileText,
  Info,
  Loader2,
  Settings2,
} from 'lucide-react';
import InterviewProgressStepper from '../../components/user/InterviewProgressStepper/InterviewProgressStepper';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import cvService from '../../services/CVService';
import jdService from '../../services/JDService';
import interviewSessionService from '../../services/InterviewSessionService';
import {
  clearActiveInterviewContext,
  getActiveInterviewContext,
  getInterviewSetupDraft,
  notifyInterviewQuotaChanged,
  saveActiveInterviewContext,
  saveInterviewSetupDraft,
} from '../../utils/interviewContext';
import '../../styles/user/InterviewSetupPage.css';

const FILE_STATUS_BY_VALUE = {
  0: 'Pending',
  1: 'Processing',
  2: 'ConfirmationRequired',
  3: 'Confirmed',
  4: 'Failed',
  5: 'AnalysisFailed',
  6: 'Archived',
};

const CV_READY_STATUSES = new Set(['Confirmed']);
const JD_READY_STATUSES = new Set(['ConfirmationRequired', 'Confirmed']);
const DEFAULT_QUESTION_COUNTS = Object.freeze({
  Behavior: 5,
  Technical: 5,
  Code: 3,
});
const MAX_QUESTION_COUNTS = Object.freeze({
  Behavior: 7,
  Technical: 7,
  Code: 3,
});

const normalizeStatus = (status) => (
  typeof status === 'number' ? FILE_STATUS_BY_VALUE[status] || 'Unknown' : status
);

const unwrapJdData = (response) => response?.data || response;

const formatRoundName = (round, t) => t(`rounds.${round}`, round);

const formatInterviewType = (rounds, t) => {
  if (!rounds?.length) return t('common.unknown');
  return rounds.map((round) => formatRoundName(round, t)).join(' + ');
};

const createConfigurationKey = (setup) => JSON.stringify({
  cvFileId: setup.CVFileId,
  jdFileId: setup.JDFileId,
  language: setup.Language,
  mode: setup.Mode,
  includeCoding: setup.IncludeCoding,
  selectedRounds: [...(setup.SelectedRounds || [])].sort(),
  questionCounts: Object.fromEntries(
    Object.entries(setup.QuestionCounts || {}).sort(([left], [right]) => left.localeCompare(right)),
  ),
});

function InterviewSetupPage() {
  const { t } = useTranslation('interview');
  const initialDraftRef = useRef(getInterviewSetupDraft() || {});
  const hasInitializedRoundDraftRef = useRef(false);
  const [activeCv, setActiveCv] = useState(null);
  const [jdOptions, setJdOptions] = useState([]);
  const [selectedJdId, setSelectedJdId] = useState(() => String(initialDraftRef.current.selectedJdId || ''));
  const [language, setLanguage] = useState(() => initialDraftRef.current.language || 'en');
  const [mode] = useState(() => initialDraftRef.current.mode || '');
  const [includeCoding, setIncludeCoding] = useState(() => Boolean(initialDraftRef.current.includeCoding));
  const [practiceRounds, setPracticeRounds] = useState(() => initialDraftRef.current.practiceRounds || []);
  const [practiceQuestionCounts, setPracticeQuestionCounts] = useState(() => ({
    ...DEFAULT_QUESTION_COUNTS,
    ...(initialDraftRef.current.practiceQuestionCounts || {}),
  }));
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [submitError, setSubmitError] = useState('');
  const [jdAvailabilityMessage, setJdAvailabilityMessage] = useState('');
  const hasValidMode = mode === 'Practice' || mode === 'RealTest';

  const loadSetupData = useCallback(async () => {
    setIsLoading(true);
    setLoadError('');
    setSubmitError('');

    try {
      const [cvHistory, jdHistory] = await Promise.all([
        cvService.getMyCVHistory(1, 20),
        jdService.getMyJDHistory(1, 20),
      ]);

      const readyCv = (cvHistory?.items || []).find((cv) => (
        CV_READY_STATUSES.has(normalizeStatus(cv.status))
      ));
      const readyJds = (jdHistory?.items || []).filter((jd) => (
        JD_READY_STATUSES.has(normalizeStatus(jd.status))
      ));

      const uploadedJds = jdHistory?.items || [];
      if (readyJds.length === 0 && uploadedJds.length > 0) {
        const latestStatus = normalizeStatus(uploadedJds[0].status);
        if (latestStatus === 'Pending') {
          setJdAvailabilityMessage(t('setup.jdPending'));
        } else if (latestStatus === 'Processing') {
          setJdAvailabilityMessage(t('setup.jdProcessing'));
        } else {
          setJdAvailabilityMessage(t('setup.jdInvalid'));
        }
      } else {
        setJdAvailabilityMessage('');
      }

      setActiveCv(readyCv || null);

      const resolvedJds = await Promise.all(readyJds.map(async (jd) => {
        try {
          const [parsedResponse, availableTypes] = await Promise.all([
            jdService.getParsedData(jd.jdFileId),
            interviewSessionService.getAvailableTypes(jd.jdFileId),
          ]);

          return {
            file: jd,
            parsed: unwrapJdData(parsedResponse),
            availableTypes,
          };
        } catch {
          return null;
        }
      }));

      const availableJds = resolvedJds.filter(Boolean);

      if (readyJds.length > 0 && availableJds.length === 0) {
        throw new Error(t('setup.jdDataLoadFailed'));
      }

      setJdOptions(availableJds);
      setSelectedJdId((currentId) => {
        const stillAvailable = availableJds.some(({ file }) => String(file.jdFileId) === currentId);
        return stillAvailable ? currentId : String(availableJds[0]?.file.jdFileId || '');
      });
    } catch (error) {
      setActiveCv(null);
      setJdOptions([]);
      setSelectedJdId('');
      setJdAvailabilityMessage('');
      setLoadError(error.message || t('setup.loadFailed'));
    } finally {
      setIsLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!hasValidMode) {
      navigate(USER_ROUTES.INTERVIEW_MODE, { replace: true });
      return;
    }

    loadSetupData();
  }, [hasValidMode, loadSetupData]);

  const selectedJd = useMemo(() => (
    jdOptions.find(({ file }) => String(file.jdFileId) === selectedJdId) || null
  ), [jdOptions, selectedJdId]);

  const baseRounds = useMemo(
    () => selectedJd?.availableTypes?.availableRounds || [],
    [selectedJd],
  );
  const hasOptionalCoding = Boolean(selectedJd?.availableTypes?.hasOptionalCoding);
  const selectablePracticeRounds = useMemo(() => {
    if (!hasOptionalCoding || baseRounds.includes('Code')) return baseRounds;
    return [...baseRounds, 'Code'];
  }, [baseRounds, hasOptionalCoding]);
  const selectablePracticeRoundsKey = selectablePracticeRounds.join('|');

  useEffect(() => {
    if (!selectedJdId || !selectedJd) return;

    if (!hasInitializedRoundDraftRef.current) {
      const restoredRounds = (initialDraftRef.current.practiceRounds || [])
        .filter((round) => selectablePracticeRounds.includes(round));
      setPracticeRounds(restoredRounds.length > 0 ? restoredRounds : selectablePracticeRounds);
      setPracticeQuestionCounts((currentCounts) => Object.fromEntries(
        selectablePracticeRounds.map((round) => [
          round,
          Math.min(
            MAX_QUESTION_COUNTS[round],
            Math.max(1, Number(currentCounts[round]) || DEFAULT_QUESTION_COUNTS[round]),
          ),
        ]),
      ));
      setIncludeCoding(Boolean(initialDraftRef.current.includeCoding && hasOptionalCoding));
      hasInitializedRoundDraftRef.current = true;
      return;
    }

    setIncludeCoding(false);
    setPracticeRounds(selectablePracticeRounds);
    setPracticeQuestionCounts(Object.fromEntries(
      selectablePracticeRounds.map((round) => [round, DEFAULT_QUESTION_COUNTS[round]]),
    ));
  }, [hasOptionalCoding, selectedJd, selectedJdId, selectablePracticeRounds, selectablePracticeRoundsKey]);

  useEffect(() => {
    if (isLoading || !hasValidMode) return;

    const currentDraft = getInterviewSetupDraft() || {};
    saveInterviewSetupDraft({
      ...currentDraft,
      mode,
      cvFileId: activeCv?.cvFileId || null,
      selectedJdId,
      language,
      includeCoding,
      practiceRounds,
      practiceQuestionCounts,
    });
  }, [
    activeCv?.cvFileId,
    hasValidMode,
    includeCoding,
    isLoading,
    language,
    mode,
    practiceQuestionCounts,
    practiceRounds,
    selectedJdId,
  ]);

  const realTestRounds = useMemo(() => {
    if (!includeCoding || baseRounds.includes('Code')) return baseRounds;
    return [...baseRounds, 'Code'];
  }, [baseRounds, includeCoding]);

  const configuredRounds = mode === 'Practice' ? practiceRounds : realTestRounds;

  const jobTitle = selectedJd?.parsed?.jobTitle
    || selectedJd?.availableTypes?.roleTarget
    || selectedJd?.file?.fileName
    || t('common.unknown');
  const experienceLevel = selectedJd?.parsed?.experienceLevel || t('common.unknown');
  const difficulty = selectedJd?.availableTypes?.difficulty || t('common.unknown');
  const interviewType = formatInterviewType(configuredRounds, t);
  const languageLabel = language === 'en' ? t('common.english') : t('common.vietnamese');
  const modeLabel = mode === 'RealTest' ? t('setup.realTest') : t('setup.practice');
  const hasRequiredSources = Boolean(activeCv && selectedJd);
  const hasValidRoundSelection = mode !== 'Practice' || practiceRounds.length > 0;
  const hasValidQuestionCounts = mode !== 'Practice' || practiceRounds.every((round) => {
    const count = Number(practiceQuestionCounts[round]);
    return Number.isInteger(count) && count >= 1 && count <= MAX_QUESTION_COUNTS[round];
  });
  const canSubmit = hasRequiredSources
    && language
    && mode
    && hasValidRoundSelection
    && hasValidQuestionCounts
    && !isSubmitting;

  const availabilityMessage = !activeCv
    ? t('setup.cvRequired')
    : jdOptions.length === 0
      ? jdAvailabilityMessage || t('setup.jdRequired')
      : '';

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitError('');

    if (!activeCv || !selectedJd) {
      setSubmitError(t('setup.missingSources'));
      return;
    }

    if (!language || !mode) {
      setSubmitError(t('setup.requiredFields'));
      return;
    }

    if (mode === 'Practice' && practiceRounds.length === 0) {
      setSubmitError(t('setup.practiceRoundRequired'));
      return;
    }

    if (!hasValidQuestionCounts) {
      setSubmitError(t('setup.invalidQuestionCounts'));
      return;
    }

    setIsSubmitting(true);

    try {
      const setupPayload = {
        CVFileId: activeCv.cvFileId,
        JDFileId: selectedJd.file.jdFileId,
        IncludeCoding: includeCoding,
        SelectedRounds: mode === 'Practice' ? practiceRounds : [],
        QuestionCounts: mode === 'Practice'
          ? Object.fromEntries(practiceRounds.map((round) => [round, practiceQuestionCounts[round]]))
          : {},
        Language: language,
        Mode: mode,
      };
      const configurationKey = createConfigurationKey(setupPayload);
      const storedContext = getActiveInterviewContext();
      const currentDraft = getInterviewSetupDraft() || {};
      const existingCampaignId = storedContext?.campaign?.interviewCampaignId
        || currentDraft.campaignId
        || currentDraft.previousCampaignId;

      if (existingCampaignId) {
        const existingCampaign = await interviewSessionService.getCampaign(existingCampaignId);
        const isLiveCampaign = existingCampaign.status === 'Pending' || existingCampaign.status === 'Active';
        const existingConfigurationKey = storedContext?.configurationKey || currentDraft.configurationKey;

        if (isLiveCampaign && existingConfigurationKey === configurationKey) {
          saveActiveInterviewContext({
            campaign: existingCampaign,
            activeSessionId: storedContext?.activeSessionId || null,
            configurationKey,
          });
          notifyInterviewQuotaChanged(existingCampaign.remainingInterviewQuota);
          navigate(USER_ROUTES.DEVICE_CHECK);
          return;
        }

        if (isLiveCampaign) {
          const cancelledCampaign = await interviewSessionService.cancelCampaign(existingCampaignId);
          notifyInterviewQuotaChanged(cancelledCampaign.remainingInterviewQuota);
        }
        clearActiveInterviewContext();
      }

      const campaign = await interviewSessionService.createSession(setupPayload);
      saveActiveInterviewContext({ campaign, activeSessionId: null, configurationKey });
      saveInterviewSetupDraft({
        ...currentDraft,
        mode,
        cvFileId: activeCv.cvFileId,
        selectedJdId: String(selectedJd.file.jdFileId),
        language,
        includeCoding,
        practiceRounds,
        practiceQuestionCounts,
        campaignId: campaign.interviewCampaignId,
        configurationKey,
        previousCampaignId: null,
      });
      notifyInterviewQuotaChanged(campaign.remainingInterviewQuota);
      navigate(USER_ROUTES.DEVICE_CHECK);
    } catch (error) {
      setSubmitError(error.message || t('setup.createFailed'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const togglePracticeRound = (round) => {
    setPracticeRounds((currentRounds) => (
      currentRounds.includes(round)
        ? currentRounds.filter((currentRound) => currentRound !== round)
        : [...currentRounds, round]
    ));
  };

  const updatePracticeQuestionCount = (round, value) => {
    const nextValue = Math.min(
      MAX_QUESTION_COUNTS[round],
      Math.max(1, Number(value) || DEFAULT_QUESTION_COUNTS[round]),
    );
    setPracticeQuestionCounts((currentCounts) => ({ ...currentCounts, [round]: nextValue }));
  };

  const handleBack = () => {
    navigate(USER_ROUTES.INTERVIEW_MODE);
  };

  if (!hasValidMode) return null;

  return (
    <UserLayout>
      <div className="setup-page animate-pageEntrance">
        <header className="setup-page-header">
          <div>
            <span className="setup-eyebrow">AI Mock Interview</span>
            <h1>{t('setup.title')}</h1>
            <p>{t('setup.subtitle')}</p>
          </div>
        </header>

        <InterviewProgressStepper activeStep={1} />

        {isLoading ? (
          <section className="setup-loading-card" aria-live="polite">
            <Loader2 size={28} className="setup-spin" />
            <div>
              <h2>{t('setup.loadingTitle')}</h2>
              <p>{t('setup.loadingDescription')}</p>
            </div>
          </section>
        ) : loadError ? (
          <section className="setup-state-card setup-state-card--error" role="alert">
            <AlertCircle size={24} />
            <div>
              <h2>{t('setup.loadFailedTitle')}</h2>
              <p>{loadError}</p>
              <button type="button" onClick={loadSetupData}>{t('common.retry')}</button>
            </div>
          </section>
        ) : (
          <div className="setup-layout">
            <form id="interview-setup-form" className="setup-form-column" onSubmit={handleSubmit} noValidate>
              {availabilityMessage && (
                <div className="setup-inline-alert setup-inline-alert--warning" role="alert">
                  <AlertCircle size={20} />
                  <div>
                    <strong>{t('setup.missingInputTitle')}</strong>
                    <span>{availabilityMessage}</span>
                  </div>
                  <button type="button" onClick={() => navigate(USER_ROUTES.CV)}>
                    {t('setup.manageCvJd')}
                  </button>
                </div>
              )}

              <section className="setup-card" aria-labelledby="basic-information-title">
                <div className="setup-section-heading">
                  <span className="setup-section-icon" aria-hidden="true">
                    <BriefcaseBusiness size={22} />
                  </span>
                  <div>
                    <h2 id="basic-information-title">{t('setup.basicTitle')}</h2>
                    <p>{t('setup.basicDescription')}</p>
                  </div>
                </div>

                <div className="setup-source-strip" aria-label={t('setup.sourceAria')}>
                  <span className="setup-source-item">
                    <FileText size={16} />
                    <span className="setup-source-content">
                      <strong>CV:</strong>
                      <span className="setup-source-name">{activeCv?.fileName || t('setup.noCv')}</span>
                    </span>
                  </span>
                  <span className="setup-source-item">
                    <BriefcaseBusiness size={16} />
                    <span className="setup-source-content">
                      <strong>JD:</strong>
                      <span className="setup-source-name">{selectedJd?.file?.fileName || t('setup.noJd')}</span>
                    </span>
                  </span>
                </div>

                <div className="setup-fields-grid">
                  <div className="setup-field">
                    <label htmlFor="setup-job-position">{t('setup.position')}</label>
                    <div className="setup-control-wrap">
                      <select
                        id="setup-job-position"
                        value={selectedJdId}
                        onChange={(event) => setSelectedJdId(event.target.value)}
                        required
                        disabled={jdOptions.length === 0}
                        aria-invalid={jdOptions.length === 0}
                        aria-describedby={jdOptions.length === 0 ? 'setup-job-position-error' : undefined}
                      >
                        {jdOptions.length === 0 && <option value="">{t('setup.noPosition')}</option>}
                        {jdOptions.map(({ file, parsed, availableTypes }) => (
                          <option key={file.jdFileId} value={file.jdFileId}>
                            {parsed?.jobTitle || availableTypes?.roleTarget || file.fileName}
                          </option>
                        ))}
                      </select>
                    </div>
                    {jdOptions.length === 0 && (
                      <span id="setup-job-position-error" className="setup-field-error">
                        {t('setup.completeJdAnalysis')}
                      </span>
                    )}
                  </div>

                  <div className="setup-field">
                    <label htmlFor="setup-experience-level">{t('setup.level')}</label>
                    <input
                      id="setup-experience-level"
                      type="text"
                      value={experienceLevel}
                      readOnly
                      aria-readonly="true"
                    />
                    <span className="setup-field-meta">{t('setup.systemDifficulty')} <strong>{difficulty}</strong></span>
                  </div>

                  <div className="setup-field">
                    <label htmlFor="setup-interview-type">{t('setup.interviewType')}</label>
                    {mode === 'Practice' ? (
                      <input
                        id="setup-interview-type"
                        type="text"
                        value={formatInterviewType(practiceRounds, t)}
                        readOnly
                        aria-readonly="true"
                      />
                    ) : (
                      <div className="setup-control-wrap">
                        <select
                          id="setup-interview-type"
                          value={includeCoding ? 'with-coding' : 'standard'}
                          onChange={(event) => setIncludeCoding(event.target.value === 'with-coding')}
                          disabled={!selectedJd}
                        >
                          <option value="standard">{formatInterviewType(baseRounds, t)}</option>
                          {hasOptionalCoding && (
                            <option value="with-coding">
                              {formatInterviewType([...baseRounds, 'Code'], t)}
                            </option>
                          )}
                        </select>
                      </div>
                    )}
                  </div>

                  <div className="setup-field">
                    <label htmlFor="setup-language">{t('setup.language')}</label>
                    <div className="setup-control-wrap">
                      <select
                        id="setup-language"
                        value={language}
                        onChange={(event) => setLanguage(event.target.value)}
                        required
                      >
                        <option value="en">{t('common.english')}</option>
                        <option value="vi">{t('common.vietnamese')}</option>
                      </select>
                    </div>
                  </div>
                </div>

              </section>

              {mode === 'Practice' && (
                <section className="setup-card" aria-labelledby="practice-rounds-title">
                  <div className="setup-section-heading">
                    <span className="setup-section-icon" aria-hidden="true">
                      <Settings2 size={22} />
                    </span>
                    <div>
                      <h2 id="practice-rounds-title">{t('setup.practiceRoundsTitle')}</h2>
                      <p>{t('setup.practiceRoundsDescription')}</p>
                    </div>
                  </div>

                  <fieldset
                    className="setup-round-selector"
                    aria-describedby={practiceRounds.length === 0 ? 'setup-round-help setup-round-error' : 'setup-round-help'}
                  >
                    <legend>{t('setup.selectRounds')}</legend>
                    <p id="setup-round-help">{t('setup.roundHelp')}</p>
                    <div className="setup-round-options">
                      {selectablePracticeRounds.map((round) => {
                        const isSelected = practiceRounds.includes(round);
                        const maxQuestionCount = MAX_QUESTION_COUNTS[round];
                        return (
                          <div
                            key={round}
                            className={`setup-round-option${isSelected ? ' setup-round-option--selected' : ''}`}
                          >
                            <label className="setup-round-choice">
                              <input
                                type="checkbox"
                                checked={isSelected}
                                onChange={() => togglePracticeRound(round)}
                              />
                              <span className="setup-round-check" aria-hidden="true">
                                {isSelected && <Check size={14} />}
                              </span>
                              <span>{formatRoundName(round, t)}</span>
                            </label>
                            {isSelected && (
                              <label className="setup-question-count" htmlFor={`setup-question-count-${round}`}>
                                <span>{t('setup.questionCount')}</span>
                                <select
                                  id={`setup-question-count-${round}`}
                                  value={practiceQuestionCounts[round]}
                                  onChange={(event) => updatePracticeQuestionCount(round, event.target.value)}
                                >
                                  {Array.from({ length: maxQuestionCount }, (_, index) => index + 1).map((count) => (
                                    <option key={count} value={count}>{count}</option>
                                  ))}
                                </select>
                                <small>{t('setup.maxQuestions', { max: maxQuestionCount })}</small>
                              </label>
                            )}
                          </div>
                        );
                      })}
                    </div>
                    {practiceRounds.length === 0 && (
                      <span id="setup-round-error" className="setup-field-error" role="alert">
                        {t('setup.roundRequired')}
                      </span>
                    )}
                  </fieldset>
                </section>
              )}
            </form>

            <aside className="setup-summary-card" aria-labelledby="setup-summary-title">
              <h2 id="setup-summary-title">{t('setup.summaryTitle')}</h2>

              <dl className="setup-summary-list">
                <div>
                  <dt>{t('setup.position')}</dt>
                  <dd>{jobTitle}</dd>
                </div>
                <div>
                  <dt>{t('setup.level')}</dt>
                  <dd>{experienceLevel}</dd>
                </div>
                <div>
                  <dt>{t('setup.difficulty')}</dt>
                  <dd>{difficulty}</dd>
                </div>
                <div>
                  <dt>{t('setup.type')}</dt>
                  <dd>{interviewType}</dd>
                </div>
                <div>
                  <dt>{t('setup.language')}</dt>
                  <dd>{languageLabel}</dd>
                </div>
                {mode === 'Practice' && (
                  <div>
                    <dt>{t('setup.questionCount')}</dt>
                    <dd className="setup-summary-question-counts">
                      {practiceRounds.map((round) => (
                        <span key={round}>{formatRoundName(round, t)}: {practiceQuestionCounts[round]}</span>
                      ))}
                    </dd>
                  </div>
                )}
                <div>
                  <dt>{t('setup.mode')}</dt>
                  <dd>{modeLabel}</dd>
                </div>
              </dl>

              <div className="setup-note">
                <Info size={20} />
                <div>
                  <strong>{t('setup.noteTitle')}</strong>
                  <p>{t('setup.noteDescription')}</p>
                </div>
              </div>

              {submitError && (
                <div className="setup-submit-error" role="alert">
                  <AlertCircle size={18} />
                  <span>{submitError}</span>
                </div>
              )}

              <div className="setup-summary-actions">
                <button
                  type="submit"
                  form="interview-setup-form"
                  className="setup-primary-button"
                  disabled={!canSubmit}
                  aria-busy={isSubmitting}
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 size={20} className="setup-spin" />
                      {t('setup.creating')}
                    </>
                  ) : (
                    <>
                      {t('common.continue')}
                      <ArrowRight size={20} />
                    </>
                  )}
                </button>
                <button type="button" className="setup-secondary-button" onClick={handleBack} disabled={isSubmitting}>
                  <ArrowLeft size={18} />
                  {t('common.back')}
                </button>
              </div>
            </aside>
          </div>
        )}
      </div>
    </UserLayout>
  );
}

export default InterviewSetupPage;
