import React from 'react';
import { Loader2, Mic, Square } from 'lucide-react';
import { RecordingStatus, SttStatus } from '../../features/technicalInterview/technicalInterview.types';

const formatElapsed = (elapsedSeconds) => {
  const minutes = Math.floor(elapsedSeconds / 60).toString().padStart(2, '0');
  const seconds = (elapsedSeconds % 60).toString().padStart(2, '0');
  return `${minutes}:${seconds}`;
};

function TechnicalRecorder({ recorder, disabled, t }) {
  const isRecording = recorder.recordingStatus === RecordingStatus.RECORDING;
  const isRequestingPermission = recorder.recordingStatus === RecordingStatus.REQUESTING_PERMISSION;
  const isProcessing = recorder.recordingStatus === RecordingStatus.PROCESSING
    || recorder.sttStatus === SttStatus.PROCESSING;
  const stateLabel = isRecording
    ? t('room.recordingNow')
    : isProcessing
      ? t('room.processingSpeech')
      : recorder.recordingStatus === RecordingStatus.READY
        ? t('room.recordingReady')
        : t('room.recordingIdle');

  return (
    <div className="technical-recorder">
      <div className="technical-recorder__state" role="status" aria-live="polite">
        {isRecording ? (
          <span className="technical-recorder__pulse" aria-hidden="true" />
        ) : isProcessing ? (
          <Loader2 size={20} className="animate-spin" aria-hidden="true" />
        ) : (
          <Mic size={20} aria-hidden="true" />
        )}
        <div>
          <strong>{stateLabel}</strong>
          <span>{isRecording ? formatElapsed(recorder.elapsedSeconds) : t('room.recordingHint')}</span>
        </div>
      </div>
      <button
        type="button"
        className={`technical-recorder-button${isRecording ? ' technical-recorder-button--recording' : ''}`}
        onClick={isRecording ? recorder.stopRecording : recorder.startRecording}
        disabled={disabled || isProcessing || isRequestingPermission}
        aria-label={isRecording ? t('room.stopRecording') : t('room.startRecording')}
      >
        {isRecording ? <Square size={18} aria-hidden="true" /> : <Mic size={18} aria-hidden="true" />}
        {isRecording ? t('room.stopRecording') : t('room.startRecording')}
      </button>
    </div>
  );
}

export default TechnicalRecorder;
