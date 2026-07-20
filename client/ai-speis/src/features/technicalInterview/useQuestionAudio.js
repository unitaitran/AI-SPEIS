import { useCallback, useEffect, useRef, useState } from 'react';
import audioService from '../../services/AudioService';

const AUTO_PLAY_STORAGE_KEY = 'ai-speis:technical-interview:auto-play-question';

export const QuestionAudioStatus = Object.freeze({
  IDLE: 'IDLE',
  LOADING: 'LOADING',
  READY: 'READY',
  ERROR: 'ERROR',
});

const readAutoPlayPreference = () => {
  try {
    return localStorage.getItem(AUTO_PLAY_STORAGE_KEY) !== 'false';
  } catch {
    return true;
  }
};

export default function useQuestionAudio({ question, sessionId, language }) {
  const [status, setStatus] = useState(QuestionAudioStatus.IDLE);
  const [isPlaying, setIsPlaying] = useState(false);
  const [error, setError] = useState(null);
  const [autoPlay, setAutoPlay] = useState(readAutoPlayPreference);
  const audioRef = useRef(null);
  const audioUrlRef = useRef(null);
  const requestRef = useRef(null);
  const requestIdRef = useRef(0);
  const autoPlayRef = useRef(autoPlay);

  const questionKey = question
    ? `${question.attemptId || question.questionId || 'question'}:${question.content || ''}`
    : null;

  const releaseResources = useCallback(() => {
    requestIdRef.current += 1;
    requestRef.current?.abort();
    requestRef.current = null;

    const audio = audioRef.current;
    if (audio) {
      audio.pause();
      audio.removeAttribute('src');
      audio.load?.();
    }
    audioRef.current = null;

    if (audioUrlRef.current) {
      URL.revokeObjectURL(audioUrlRef.current);
      audioUrlRef.current = null;
    }
  }, []);

  const synthesize = useCallback(async () => {
    releaseResources();
    setIsPlaying(false);
    setError(null);

    if (!question?.content || !sessionId) {
      setStatus(QuestionAudioStatus.IDLE);
      return;
    }

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    const controller = new AbortController();
    requestRef.current = controller;
    setStatus(QuestionAudioStatus.LOADING);

    try {
      const audioBlob = await audioService.synthesizeSpeech({
        text: question.content,
        languageCode: language === 'en' ? 'en-US' : 'vi-VN',
        sessionId,
        questionId: question.questionId,
        attemptId: question.attemptId,
      }, { signal: controller.signal });

      if (requestIdRef.current !== requestId) return;
      const audioUrl = URL.createObjectURL(audioBlob);
      const audio = new Audio(audioUrl);
      audio.preload = 'auto';
      audio.onplay = () => setIsPlaying(true);
      audio.onpause = () => setIsPlaying(false);
      audio.onended = () => setIsPlaying(false);
      audio.onerror = () => {
        setIsPlaying(false);
        setStatus(QuestionAudioStatus.ERROR);
        setError({ code: 'TTS_AUDIO_PLAYBACK_FAILED' });
      };

      audioUrlRef.current = audioUrl;
      audioRef.current = audio;
      requestRef.current = null;
      setStatus(QuestionAudioStatus.READY);

      if (autoPlayRef.current) {
        // Browser autoplay policies may reject play() after the async request.
        // Audio remains ready for the explicit Play control in that case.
        audio.play().catch(() => setIsPlaying(false));
      }
    } catch (requestError) {
      if (controller.signal.aborted || requestIdRef.current !== requestId) return;
      requestRef.current = null;
      setStatus(QuestionAudioStatus.ERROR);
      setError(requestError);
    }
  }, [language, question, releaseResources, sessionId]);

  useEffect(() => {
    synthesize();
    return releaseResources;
  }, [questionKey, releaseResources, synthesize]);

  useEffect(() => {
    autoPlayRef.current = autoPlay;
  }, [autoPlay]);

  const play = useCallback(async () => {
    const audio = audioRef.current;
    if (!audio) return;
    try {
      await audio.play();
    } catch (playError) {
      setIsPlaying(false);
      setError(playError);
    }
  }, []);

  const pause = useCallback(() => {
    audioRef.current?.pause();
  }, []);

  const replay = useCallback(async () => {
    const audio = audioRef.current;
    if (!audio) return;
    audio.currentTime = 0;
    try {
      await audio.play();
    } catch (playError) {
      setIsPlaying(false);
      setError(playError);
    }
  }, []);

  const toggleAutoPlay = useCallback(() => {
    setAutoPlay((current) => {
      const next = !current;
      autoPlayRef.current = next;
      try {
        localStorage.setItem(AUTO_PLAY_STORAGE_KEY, String(next));
      } catch {
        // The in-memory preference still works when storage is unavailable.
      }
      if (next && audioRef.current && !isPlaying) {
        audioRef.current.play().catch(() => setIsPlaying(false));
      }
      return next;
    });
  }, [isPlaying]);

  return {
    status,
    isPlaying,
    autoPlay,
    error,
    play,
    pause,
    replay,
    retry: synthesize,
    toggleAutoPlay,
  };
}
