import React, { useEffect, useRef } from 'react';
import { AlertCircle, Bot, Loader2, Mic, UserRound, X } from 'lucide-react';
import { RecordingStatus, SttStatus } from '../../features/technicalInterview/technicalInterview.types';
import {
  TechnicalTranscriptItemStatus,
  TechnicalTranscriptRole,
} from '../../features/technicalInterview/useTechnicalInterviewTranscript';
import TechnicalTranscriptEditor from './TechnicalTranscriptEditor';

const NEAR_BOTTOM_THRESHOLD = 80;

function TechnicalTranscriptPanel({
  items,
  recorder,
  currentTranscript,
  hasActiveAttempt,
  transcriptEditable,
  disabled,
  isOpen,
  onClose,
  t,
}) {
  const closeButtonRef = useRef(null);
  const listRef = useRef(null);
  const isNearBottomRef = useRef(true);

  useEffect(() => {
    if (!isOpen) return undefined;
    closeButtonRef.current?.focus({ preventScroll: true });
    const handleKeyDown = (event) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  useEffect(() => {
    if (!isOpen || !isNearBottomRef.current) return;
    const list = listRef.current;
    if (list) list.scrollTop = list.scrollHeight;
  }, [isOpen, items, currentTranscript]);

  const handleScroll = () => {
    const list = listRef.current;
    if (!list) return;
    isNearBottomRef.current = list.scrollHeight - list.scrollTop - list.clientHeight
      <= NEAR_BOTTOM_THRESHOLD;
  };

  const isListening = recorder.recordingStatus === RecordingStatus.RECORDING;
  const isRequestingPermission = recorder.recordingStatus === RecordingStatus.REQUESTING_PERMISSION;
  const isProcessing = recorder.recordingStatus === RecordingStatus.PROCESSING
    || recorder.sttStatus === SttStatus.PROCESSING;
  const hasError = Boolean(recorder.permissionError || recorder.sttError);

  const liveState = hasError
    ? { icon: AlertCircle, label: recorder.permissionError ? t('room.microphoneError') : t('room.sttError'), tone: 'error' }
    : isProcessing
      ? { icon: Loader2, label: t('room.transcriptProcessing'), tone: 'processing' }
      : isListening
        ? { icon: Mic, label: t('room.transcriptListening'), tone: 'listening' }
        : isRequestingPermission
          ? { icon: Loader2, label: t('room.transcriptRequestingPermission'), tone: 'processing' }
          : null;
  const LiveStateIcon = liveState?.icon;

  return (
    <aside
      id="technical-transcript-panel"
      className="technical-transcript-panel"
      aria-labelledby="technical-transcript-panel-title"
    >
      <header className="technical-transcript-panel__header">
        <div>
          <h2 id="technical-transcript-panel-title">{t('room.transcriptPanelTitle')}</h2>
          <p>{t('room.transcriptPanelDescription')}</p>
        </div>
        <button
          ref={closeButtonRef}
          type="button"
          className="technical-icon-button"
          onClick={onClose}
          aria-label={t('room.closeTranscript')}
        >
          <X size={20} aria-hidden="true" />
        </button>
      </header>

      {liveState && (
        <div className={`technical-transcript-live technical-transcript-live--${liveState.tone}`} role="status">
          <LiveStateIcon
            size={17}
            className={liveState.tone === 'processing' ? 'animate-spin' : undefined}
            aria-hidden="true"
          />
          <span>{liveState.label}</span>
        </div>
      )}

      <div
        ref={listRef}
        className="technical-transcript-panel__list"
        role="log"
        aria-live="polite"
        aria-relevant="additions text"
        onScroll={handleScroll}
      >
        {items.length === 0 ? (
          <div className="technical-transcript-empty">
            <Bot size={24} aria-hidden="true" />
            <p>{t('room.transcriptEmpty')}</p>
          </div>
        ) : items.map((item) => {
          const isInterviewer = item.role === TechnicalTranscriptRole.INTERVIEWER;
          const ItemIcon = isInterviewer ? Bot : UserRound;
          return (
            <article
              key={item.id}
              className={`technical-transcript-item technical-transcript-item--${isInterviewer ? 'interviewer' : 'candidate'}`}
            >
              <div className="technical-transcript-item__speaker">
                <ItemIcon size={16} aria-hidden="true" />
                <strong>{isInterviewer ? t('room.transcriptInterviewer') : t('room.transcriptCandidate')}</strong>
                {item.status && item.status !== TechnicalTranscriptItemStatus.FINAL && (
                  <span>{t(`room.transcriptStatuses.${item.status}`)}</span>
                )}
              </div>
              <p>{item.content}</p>
            </article>
          );
        })}
      </div>

      {(hasActiveAttempt || currentTranscript || recorder.recordingStatus !== RecordingStatus.IDLE) && (
        <div className="technical-transcript-panel__editor">
          <TechnicalTranscriptEditor
            value={currentTranscript}
            onChange={recorder.setTranscript}
            disabled={disabled}
            editable={transcriptEditable}
            t={t}
          />
        </div>
      )}
    </aside>
  );
}

export default TechnicalTranscriptPanel;
