import React from 'react';
import { AlertCircle, Loader2, Mic } from 'lucide-react';
import InterviewRoomTranscriptPanel from '../interviewRoom/InterviewRoomTranscriptPanel';
import { RecordingStatus, SttStatus } from '../../features/technicalInterview/technicalInterview.types';
import {
  TechnicalTranscriptItemStatus,
} from '../../features/technicalInterview/useTechnicalInterviewTranscript';
import TechnicalTranscriptEditor from './TechnicalTranscriptEditor';

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
  const isListening = recorder.recordingStatus === RecordingStatus.RECORDING;
  const isRequestingPermission = recorder.recordingStatus === RecordingStatus.REQUESTING_PERMISSION;
  const isProcessing = recorder.recordingStatus === RecordingStatus.PROCESSING
    || recorder.sttStatus === SttStatus.PROCESSING;
  const hasError = Boolean(recorder.permissionError || recorder.sttError);

  const liveState = hasError
    ? { icon: AlertCircle, label: recorder.permissionError ? t('room.microphoneError') : t('room.sttError'), tone: 'error' }
    : isProcessing
      ? { icon: Loader2, label: t('room.transcriptProcessing'), tone: 'processing', spin: true }
      : isListening
        ? { icon: Mic, label: t('room.transcriptListening'), tone: 'listening' }
        : isRequestingPermission
          ? { icon: Loader2, label: t('room.transcriptRequestingPermission'), tone: 'processing', spin: true }
          : null;

  const normalizedItems = items.map((item) => ({
    ...item,
    statusLabel: item.status && item.status !== TechnicalTranscriptItemStatus.FINAL
      ? t(`room.transcriptStatuses.${item.status}`)
      : '',
  }));

  return (
    <InterviewRoomTranscriptPanel
      candidateLabel={t('room.transcriptCandidate')}
      closeLabel={t('room.closeTranscript')}
      description={t('room.transcriptPanelDescription')}
      emptyMessage={t('room.transcriptEmpty')}
      interviewerLabel={t('room.transcriptInterviewer')}
      isOpen={isOpen}
      items={normalizedItems}
      liveState={liveState}
      onClose={onClose}
      title={t('room.transcriptPanelTitle')}
    >
      {(hasActiveAttempt || currentTranscript || recorder.recordingStatus !== RecordingStatus.IDLE) ? (
        <TechnicalTranscriptEditor
          value={currentTranscript}
          onChange={recorder.setTranscript}
          disabled={disabled}
          editable={transcriptEditable}
          t={t}
        />
      ) : null}
    </InterviewRoomTranscriptPanel>
  );
}

export default TechnicalTranscriptPanel;
