import React from 'react';
import { Loader2, Pause, Play, RefreshCw, RotateCcw, Volume2 } from 'lucide-react';
import { QuestionAudioStatus } from '../../features/technicalInterview/useQuestionAudio';

function QuestionAudioControls({ audio, t }) {
  const isLoading = audio.status === QuestionAudioStatus.IDLE
    || audio.status === QuestionAudioStatus.LOADING;
  const isReady = audio.status === QuestionAudioStatus.READY;
  const hasError = audio.status === QuestionAudioStatus.ERROR;

  return (
    <div className="technical-audio" aria-live="polite">
      <div className="technical-audio__row">
        <span className="technical-audio__status">
          {isLoading ? <Loader2 size={18} className="animate-spin" aria-hidden="true" /> : <Volume2 size={18} aria-hidden="true" />}
          {isLoading
            ? t('room.audioLoading')
            : audio.isPlaying
              ? t('room.audioPlaying')
              : hasError
                ? t('room.audioUnavailable')
                : t('room.audioReady')}
        </span>

        <div className="technical-audio__actions">
          <button
            type="button"
            className="technical-audio__button"
            onClick={audio.isPlaying ? audio.pause : audio.play}
            disabled={!isReady}
            aria-label={audio.isPlaying ? t('room.pauseQuestionAudio') : t('room.playQuestionAudio')}
          >
            {audio.isPlaying ? <Pause size={17} aria-hidden="true" /> : <Play size={17} aria-hidden="true" />}
            {audio.isPlaying ? t('room.pauseAudio') : t('room.playAudio')}
          </button>
          <button
            type="button"
            className="technical-audio__button"
            onClick={audio.replay}
            disabled={!isReady}
            aria-label={t('room.replayQuestionAudio')}
          >
            <RotateCcw size={17} aria-hidden="true" />
            {t('room.replayAudio')}
          </button>
          <button
            type="button"
            className={`technical-audio__toggle${audio.autoPlay ? ' technical-audio__toggle--active' : ''}`}
            role="switch"
            aria-checked={audio.autoPlay}
            onClick={audio.toggleAutoPlay}
          >
            {t('room.autoPlayAudio')}: {audio.autoPlay ? t('room.on') : t('room.off')}
          </button>
        </div>
      </div>

      {hasError && (
        <div className="technical-audio__error" role="status">
          <span>{t('room.audioFallback')}</span>
          <button type="button" onClick={audio.retry} disabled={isLoading}>
            <RefreshCw size={16} aria-hidden="true" />
            {t('room.retryAudio')}
          </button>
        </div>
      )}
    </div>
  );
}

export default QuestionAudioControls;
