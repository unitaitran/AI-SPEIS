import React from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  Briefcase,
  CheckCircle2,
  CircleAlert,
  FileText,
  Loader2,
  Plus,
  RefreshCw,
  Sparkles,
  Upload,
  X,
} from 'lucide-react';
import { useCvJdFastCheck } from './useCvJdFastCheck';
import FastCheckResult from './FastCheckResult';
import './FastCheckPanel.css';

const STATUS_INT_MAP = {
  0: 'Pending',
  1: 'Processing',
  2: 'ConfirmationRequired',
  3: 'Confirmed',
  4: 'Failed',
  5: 'AnalysisFailed',
  6: 'Archived',
};

const normalizeStatus = (status) => (
  typeof status === 'number' ? STATUS_INT_MAP[status] || String(status) : String(status || '')
);

const formatFileSize = (bytes) => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '';
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
};

function FastCheckPanel({
  currentCv,
  jds,
  loadingSources,
  onAddJd,
  onCvUploaded,
  onSourcesChanged,
}) {
  const { t } = useTranslation('cvjd');
  const {
    activeCv,
    clearPendingCvFile,
    cvFileError,
    error,
    isBusy,
    pendingCvFile,
    phase,
    result,
    selectCvFile,
    selectedJd,
    selectedJdId,
    setSelectedJdId,
    submit,
  } = useCvJdFastCheck({ currentCv, jds, onCvUploaded, onSourcesChanged });

  const statusLabels = {
    Pending: t('fastCheckPanel.status.pending'),
    Processing: t('fastCheckPanel.status.processing'),
    ConfirmationRequired: t('fastCheckPanel.status.extracted'),
    Confirmed: t('fastCheckPanel.status.confirmed'),
    Failed: t('fastCheckPanel.status.uploadFailed'),
    AnalysisFailed: t('fastCheckPanel.status.analysisFailed'),
    Archived: t('fastCheckPanel.status.archived'),
  };

  const phaseContent = {
    'uploading-cv': [t('fastCheckPanel.phase.uploadingCvTitle'), t('fastCheckPanel.phase.uploadingCvDesc')],
    'parsing-cv': [t('fastCheckPanel.phase.parsingCvTitle'), t('fastCheckPanel.phase.parsingCvDesc')],
    'parsing-jd': [t('fastCheckPanel.phase.parsingJdTitle'), t('fastCheckPanel.phase.parsingJdDesc')],
    matching: [t('fastCheckPanel.phase.matchingTitle'), t('fastCheckPanel.phase.matchingDesc')],
  };

  const getStatusLabel = (status) => statusLabels[normalizeStatus(status)] || t('notSpecified');

  const hasCvInput = Boolean(pendingCvFile || activeCv?.cvFileId);
  const canSubmit = hasCvInput && Boolean(selectedJdId) && !cvFileError && !isBusy && !loadingSources;
  const loadingContent = phaseContent[phase];

  return (
    <section id="cv-jd-fast-check" className="fast-check" aria-labelledby="fast-check-title">
      <div className="fast-check__header">
        <div className="fast-check__header-icon" aria-hidden="true">
          <Sparkles size={24} />
        </div>
        <div>
          <p className="fast-check__eyebrow">{t('fastCheckPanel.eyebrow')}</p>
          <h2 id="fast-check-title">{t('fastCheckPanel.title')}</h2>
          <p>{t('fastCheckPanel.subtitle')}</p>
        </div>
      </div>

      <div className="fast-check__input-grid">
        <div className={`fast-check__input-card ${cvFileError ? 'fast-check__input-card--error' : ''}`}>
          <div className="fast-check__input-heading">
            <div className="fast-check__step">1</div>
            <div>
              <h3>{t('fastCheckPanel.cvTitle')}</h3>
              <p>{t('fastCheckPanel.cvHint')}</p>
            </div>
          </div>

          {pendingCvFile ? (
            <div className="fast-check__file-row">
              <div className="fast-check__file-icon"><FileText size={20} /></div>
              <div className="fast-check__file-copy">
                <strong title={pendingCvFile.name}>{pendingCvFile.name}</strong>
                <span>{formatFileSize(pendingCvFile.size)} · {t('fastCheckPanel.readyToUpload')}</span>
              </div>
              <button
                type="button"
                className="fast-check__icon-button"
                onClick={clearPendingCvFile}
                disabled={isBusy}
                aria-label={t('fastCheckPanel.removeSelectedCv')}
              >
                <X size={18} />
              </button>
            </div>
          ) : activeCv ? (
            <div className="fast-check__file-row">
              <div className="fast-check__file-icon fast-check__file-icon--success"><CheckCircle2 size={20} /></div>
              <div className="fast-check__file-copy">
                <strong title={activeCv.fileName}>{activeCv.fileName}</strong>
                <span>{t('fastCheckPanel.currentCv')} · {getStatusLabel(activeCv.status)}</span>
              </div>
            </div>
          ) : (
            <div className="fast-check__empty-input">
              <FileText size={24} />
              <span>{t('fastCheckPanel.noCv')}</span>
            </div>
          )}

          <label className={`fast-check__secondary-button ${isBusy ? 'fast-check__secondary-button--disabled' : ''}`}>
            <Upload size={16} />
            {hasCvInput ? t('fastCheckPanel.chooseAnotherCv') : t('fastCheckPanel.choosePdfCv')}
            <input
              type="file"
              accept="application/pdf,.pdf"
              disabled={isBusy}
              onChange={(event) => {
                selectCvFile(event.target.files?.[0] || null);
                event.target.value = '';
              }}
              hidden
            />
          </label>
          {cvFileError && (
            <div className="fast-check__field-error">
              <CircleAlert size={14} />
              <span>{cvFileError}</span>
              <button type="button" onClick={clearPendingCvFile}>{t('fastCheckPanel.skip')}</button>
            </div>
          )}
        </div>

        <div className="fast-check__input-card">
          <div className="fast-check__input-heading">
            <div className="fast-check__step">2</div>
            <div>
              <h3>{t('fastCheckPanel.jdTitle')}</h3>
              <p>{t('fastCheckPanel.jdHint')}</p>
            </div>
          </div>

          <label className="fast-check__select-label" htmlFor="fast-check-jd">{t('fastCheckPanel.jdSelectLabel')}</label>
          <div className="fast-check__select-wrap">
            <Briefcase size={18} aria-hidden="true" />
            <select
              id="fast-check-jd"
              value={selectedJdId}
              onChange={(event) => setSelectedJdId(event.target.value)}
              disabled={isBusy || loadingSources}
            >
              <option value="">{loadingSources ? t('fastCheckPanel.loadingJds') : t('fastCheckPanel.chooseJd')}</option>
              {jds.map((jd) => (
                <option key={jd.jdFileId} value={jd.jdFileId}>
                  {jd.fileName || t('fastCheckPanel.textJd')} · {getStatusLabel(jd.status)}
                </option>
              ))}
            </select>
          </div>

          {selectedJd ? (
            <div className="fast-check__selection-note">
              <CheckCircle2 size={16} />
              <span>
                <strong>{selectedJd.fileName || t('fastCheckPanel.textJd')}</strong>
                {t('fastCheckPanel.jdAutoAnalyze')}
              </span>
            </div>
          ) : (
            <p className="fast-check__helper">{t('fastCheckPanel.jdRequiredHint')}</p>
          )}

          <button
            type="button"
            className="fast-check__secondary-button"
            onClick={onAddJd}
            disabled={isBusy || jds.length >= 5}
          >
            <Plus size={16} /> {jds.length >= 5 ? t('fastCheckPanel.jdLimitReached') : t('fastCheckPanel.addNewJd')}
          </button>
        </div>
      </div>

      <div className="fast-check__action-row">
        <div className="fast-check__privacy-note">
          <CheckCircle2 size={16} />
          {t('fastCheckPanel.privacyNote')}
        </div>
        <button type="button" className="fast-check__submit" onClick={submit} disabled={!canSubmit}>
          {isBusy ? <Loader2 size={18} className="fast-check__spinner" /> : <Sparkles size={18} />}
          {isBusy ? t('fastCheckPanel.processing') : t('fastCheckPanel.submit')}
        </button>
      </div>

      {!hasCvInput && <p className="fast-check__required-hint">{t('fastCheckPanel.enableHint')}</p>}

      {loadingContent && (
        <div className="fast-check__loading" role="status" aria-live="polite">
          <div className="fast-check__loading-icon"><Loader2 size={26} /></div>
          <div>
            <strong>{loadingContent[0]}</strong>
            <p>{loadingContent[1]}</p>
          </div>
          <div className="fast-check__loading-track" aria-hidden="true"><span /></div>
        </div>
      )}

      {error && !isBusy && (
        <div className="fast-check__error" role="alert">
          <AlertCircle size={20} />
          <div>
            <strong>{t('fastCheckPanel.errorTitle')}</strong>
            <p>{error}</p>
          </div>
          <button type="button" onClick={submit} disabled={!canSubmit}>
            <RefreshCw size={15} /> {t('fastCheckPanel.retry')}
          </button>
        </div>
      )}

      {result && <FastCheckResult result={result} />}
    </section>
  );
}

export default FastCheckPanel;
