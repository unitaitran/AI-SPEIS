import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { InterviewMode } from './InterviewMode';
import { getInterviewStrategy } from './InterviewStrategy';
import { getActiveInterviewContext, getInterviewSetupDraft } from '../../utils/interviewContext';

export default function useInterviewStrategy(overrideMode = null) {
  const activeContext = getActiveInterviewContext();
  const setupDraft = getInterviewSetupDraft();

  const currentModeStr = overrideMode
    || activeContext?.campaign?.mode
    || activeContext?.mode
    || setupDraft?.mode
    || InterviewMode.PRACTICE;

  const strategy = useMemo(() => getInterviewStrategy(currentModeStr), [currentModeStr]);

  const [remainingSeconds, setRemainingSeconds] = useState(strategy.defaultCountdownSeconds);
  const [isTimerRunning, setIsTimerRunning] = useState(false);
  const timerRef = useRef(null);
  const onTimeoutRef = useRef(null);

  const stopTimer = useCallback(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
    setIsTimerRunning(false);
  }, []);

  const startTimer = useCallback((seconds = strategy.defaultCountdownSeconds, onTimeout = null) => {
    stopTimer();
    if (!strategy.hasCountdownTimer || !seconds) return;

    onTimeoutRef.current = onTimeout;
    setRemainingSeconds(seconds);
    setIsTimerRunning(true);

    timerRef.current = setInterval(() => {
      setRemainingSeconds((prev) => {
        if (prev <= 1) {
          stopTimer();
          if (typeof onTimeoutRef.current === 'function') {
            onTimeoutRef.current();
          }
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, [stopTimer, strategy.defaultCountdownSeconds, strategy.hasCountdownTimer]);

  const resetTimer = useCallback(() => {
    stopTimer();
    setRemainingSeconds(strategy.defaultCountdownSeconds);
  }, [stopTimer, strategy.defaultCountdownSeconds]);

  useEffect(() => {
    return () => stopTimer();
  }, [stopTimer]);

  // Strategy delegation helper: on AI question audio ended
  const handleQuestionAudioEnded = useCallback(async ({ startRecording } = {}) => {
    await strategy.onQuestionAudioEnded({
      startRecording,
      startTimer,
    });
  }, [startTimer, strategy]);

  // Strategy delegation helper: on timer expired
  const handleTimerExpired = useCallback(async ({ stopRecording, submitAnswer } = {}) => {
    await strategy.onTimerExpired({
      stopRecording,
      submitAnswer,
    });
  }, [strategy]);

  return {
    strategy,
    mode: strategy.mode,
    isPractice: strategy.isPractice,
    isReal: strategy.isReal,
    remainingSeconds,
    isTimerRunning,
    startTimer,
    stopTimer,
    resetTimer,
    handleQuestionAudioEnded,
    handleTimerExpired,
  };
}
