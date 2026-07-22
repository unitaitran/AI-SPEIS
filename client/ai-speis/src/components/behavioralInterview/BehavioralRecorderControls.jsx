import React, { useEffect, useMemo } from 'react';
import {
  Loader2,
  Mic,
  RefreshCw,
  RotateCcw,
  Send,
  Square,
  TriangleAlert,
} from 'lucide-react';
import { RecordingStatus, SttStatus } from '../../features/technicalInterview/technicalInterview.types';

const formatDuration = (seconds) => {
  const safeSeconds = Math.max(0, Number(seconds) || 0);
  return `${String(Math.floor(safeSeconds / 60)).padStart(2, '0')}:${String(safeSeconds % 60).padStart(2, '0')}`;
};

function BehavioralRecorderControls({ recorder, disabled, isSubmitting, timeLimitSeconds, onSubmit, t }) {
  const audioUrl = useMemo(
    () => (recorder.audioBlob ? URL.createObjectURL(recorder.audioBlob) : ''),
    [recorder.audioBlob],
  );

  useEffect(() => () => {
    if (audioUrl) URL.revokeObjectURL(audioUrl);
  }, [audioUrl]);

  useEffect(() => {
    if (
      timeLimitSeconds
      && recorder.recordingStatus === RecordingStatus.RECORDING
      && recorder.elapsedSeconds >= timeLimitSeconds
    ) {
      recorder.stopRecording();
    }
  }, [recorder, timeLimitSeconds]);

  const isRecording = recorder.recordingStatus === RecordingStatus.RECORDING;
  const isProcessing = recorder.recordingStatus === RecordingStatus.PROCESSING
    || recorder.sttStatus === SttStatus.PROCESSING;
  const hasTranscript = Boolean(recorder.transcript.trim());
  const permissionDenied = Boolean(recorder.permissionError);
  const sttFailed = recorder.sttStatus === SttStatus.FAILED
    || (recorder.sttStatus === SttStatus.COMPLETED && recorder.audioBlob && !hasTranscript);

  if (isRecording) {
    return (
      <section className="behavior-recorder behavior-recorder--recording" aria-label={t('recordingControls')}>
        <div className="behavior-recorder__status" role="status" aria-live="polite">
          <span className="behavior-recorder__live-dot" />
          <strong>{t('recording')}</strong>
          <time>{formatDuration(recorder.elapsedSeconds)}</time>
          {timeLimitSeconds ? <span>/ {formatDuration(timeLimitSeconds)}</span> : null}
        </div>
        <div className="behavior-recorder__wave" aria-hidden="true">
          {Array.from({ length: 18 }).map((_, index) => <span key={index} />)}
        </div>
        <button type="button" className="behavior-recorder__stop" onClick={recorder.stopRecording}>
          <Square size={20} fill="currentColor" />
          {t('stopRecording')}
        </button>
      </section>
    );
  }

  if (isProcessing) {
    return (
      <section className="behavior-recorder behavior-recorder--processing" role="status" aria-live="polite">
        <Loader2 size={24} className="behavior-spin" />
        <div>
          <strong>{t('processingTranscript')}</strong>
          <p>{t('processingTranscriptDescription')}</p>
        </div>
      </section>
    );
  }

  if ((permissionDenied || sttFailed) && !hasTranscript) {
    return (
      <section className="behavior-recorder behavior-recorder--error" role="alert">
        <TriangleAlert size={22} />
        <div>
          <strong>{permissionDenied ? t('microphoneUnavailable') : t('transcriptionFailed')}</strong>
          <p>{permissionDenied ? t('microphoneHelp') : t('audioPreserved')}</p>
        </div>
        <div className="behavior-recorder__actions">
          {sttFailed && recorder.audioBlob ? (
            <button type="button" onClick={recorder.retryTranscription} disabled={disabled}>
              <RefreshCw size={17} />
              {t('retryTranscription')}
            </button>
          ) : (
            <button type="button" onClick={recorder.startRecording} disabled={disabled}>
              <RefreshCw size={17} />
              {t('tryAgain')}
            </button>
          )}
          {recorder.audioBlob ? (
            <button type="button" onClick={recorder.reset} disabled={disabled}>
              <RotateCcw size={17} />
              {t('recordAgain')}
            </button>
          ) : null}
        </div>
      </section>
    );
  }

  if (hasTranscript) {
    return (
      <section className="behavior-recorder behavior-recorder--review" aria-label={t('answerReview')}>
        <div className="behavior-recorder__review-head">
          <div>
            <strong>{t('answerReady')}</strong>
            <p>{t('reviewBeforeSubmit')}</p>
          </div>
          <time>{formatDuration(recorder.elapsedSeconds)}</time>
        </div>
        {audioUrl ? <audio controls preload="metadata" src={audioUrl}>{t('audioUnsupported')}</audio> : null}
        <div className="behavior-recorder__transcript">
          <label htmlFor="interview-answer-transcript">{t('yourTranscript')}</label>
          <textarea
            id="interview-answer-transcript"
            value={recorder.transcript}
            onChange={(event) => recorder.setTranscript(event.target.value)}
            placeholder={t('transcriptPlaceholder')}
            readOnly={disabled || isSubmitting}
            aria-readonly={disabled || isSubmitting}
          />
          <p>{t('transcriptHelper')}</p>
        </div>
        <div className="behavior-recorder__actions behavior-recorder__actions--review">
          <button type="button" onClick={recorder.reset} disabled={disabled || isSubmitting}>
            <RotateCcw size={17} />
            {t('recordAgain')}
          </button>
          <button
            type="button"
            className="behavior-recorder__submit"
            onClick={onSubmit}
            disabled={disabled || isSubmitting || !hasTranscript}
          >
            {isSubmitting ? <Loader2 size={18} className="behavior-spin" /> : <Send size={18} />}
            {isSubmitting ? t('submitting') : t('submitAnswer')}
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="behavior-recorder behavior-recorder--idle">
      <button
        type="button"
        className="behavior-recorder__start"
        onClick={recorder.startRecording}
        disabled={disabled || recorder.recordingStatus === RecordingStatus.REQUESTING_PERMISSION}
        aria-label={t('startRecording')}
      >
        {recorder.recordingStatus === RecordingStatus.REQUESTING_PERMISSION
          ? <Loader2 size={28} className="behavior-spin" />
          : <Mic size={28} />}
      </button>
      <div>
        <strong>{recorder.recordingStatus === RecordingStatus.REQUESTING_PERMISSION
          ? t('requestingMicrophone')
          : t('tapToAnswer')}</strong>
        <p>{t('recordingPrivacy')}</p>
      </div>
    </section>
  );
}

export default BehavioralRecorderControls;
