import { InterviewMode, normalizeInterviewMode } from './InterviewMode';
import {
  getInterviewStrategy,
  PracticeInterviewStrategy,
  RealInterviewStrategy,
} from './InterviewStrategy';

describe('InterviewStrategy Strategy Pattern Tests', () => {
  describe('normalizeInterviewMode', () => {
    test('normalizes Practice mode strings correctly', () => {
      expect(normalizeInterviewMode('Practice')).toBe(InterviewMode.PRACTICE);
      expect(normalizeInterviewMode('')).toBe(InterviewMode.PRACTICE);
      expect(normalizeInterviewMode(null)).toBe(InterviewMode.PRACTICE);
    });

    test('normalizes RealTest / Real mode strings correctly', () => {
      expect(normalizeInterviewMode('RealTest')).toBe(InterviewMode.REAL);
      expect(normalizeInterviewMode('Real')).toBe(InterviewMode.REAL);
      expect(normalizeInterviewMode('real')).toBe(InterviewMode.REAL);
    });
  });

  describe('PracticeInterviewStrategy', () => {
    const strategy = getInterviewStrategy('Practice');

    test('has correct Practice mode properties', () => {
      expect(strategy).toBeInstanceOf(PracticeInterviewStrategy);
      expect(strategy.isPractice).toBe(true);
      expect(strategy.isReal).toBe(false);
      expect(strategy.autoRecordAfterQuestionAudio).toBe(false);
      expect(strategy.defaultCountdownSeconds).toBeNull();
      expect(strategy.hasCountdownTimer).toBe(false);
      expect(strategy.allowReplayAudio).toBe(true);
      expect(strategy.showMicIdlePrompt).toBe(true);
      expect(strategy.showAudioControls).toBe(true);
      expect(strategy.forceAutoPlay).toBe(false);
    });

    test('does not auto start recording on question audio end', async () => {
      const startRecording = jest.fn();
      const startTimer = jest.fn();
      await strategy.onQuestionAudioEnded({ startRecording, startTimer });
      expect(startRecording).not.toHaveBeenCalled();
      expect(startTimer).not.toHaveBeenCalled();
    });
  });

  describe('RealInterviewStrategy', () => {
    const strategy = getInterviewStrategy('RealTest');

    test('has correct Real mode properties', () => {
      expect(strategy).toBeInstanceOf(RealInterviewStrategy);
      expect(strategy.isPractice).toBe(false);
      expect(strategy.isReal).toBe(true);
      expect(strategy.autoRecordAfterQuestionAudio).toBe(true);
      expect(strategy.defaultCountdownSeconds).toBe(120);
      expect(strategy.hasCountdownTimer).toBe(true);
      expect(strategy.allowReplayAudio).toBe(false);
      expect(strategy.showMicIdlePrompt).toBe(false);
      expect(strategy.showAudioControls).toBe(false);
      expect(strategy.forceAutoPlay).toBe(true);
    });

    test('auto starts recording and 2-minute countdown timer on audio end', async () => {
      const startRecording = jest.fn().mockResolvedValue();
      const startTimer = jest.fn();
      await strategy.onQuestionAudioEnded({ startRecording, startTimer });

      expect(startRecording).toHaveBeenCalledTimes(1);
      expect(startTimer).toHaveBeenCalledWith(120);
    });

    test('stops recording and auto-submits answer when timer expires', async () => {
      const stopRecording = jest.fn().mockResolvedValue();
      const submitAnswer = jest.fn().mockResolvedValue();
      await strategy.onTimerExpired({ stopRecording, submitAnswer });

      expect(stopRecording).toHaveBeenCalledTimes(1);
      expect(submitAnswer).toHaveBeenCalledTimes(1);
    });
  });
});
