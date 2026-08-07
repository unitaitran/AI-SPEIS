import { InterviewMode, normalizeInterviewMode } from './InterviewMode';

export { InterviewMode, normalizeInterviewMode };

/**
 * Abstract Base Strategy for Interview Flow
 */
export class BaseInterviewStrategy {
  get mode() {
    return InterviewMode.PRACTICE;
  }

  get isPractice() {
    return true;
  }

  get isReal() {
    return false;
  }

  get autoRecordAfterQuestionAudio() {
    return false;
  }

  get defaultCountdownSeconds() {
    return null;
  }

  get hasCountdownTimer() {
    return false;
  }

  get allowReplayAudio() {
    return true;
  }

  get showMicIdlePrompt() {
    return true;
  }

  get showAudioControls() {
    return true;
  }

  get forceAutoPlay() {
    return false;
  }

  // Strategy hook when AI question audio finishes reading
  async onQuestionAudioEnded({ startRecording, startTimer } = {}) {
    // Override in concrete strategies
  }

  // Strategy hook when countdown timer expires
  async onTimerExpired({ stopRecording, submitAnswer } = {}) {
    // Override in concrete strategies
  }
}

/**
 * Practice Mode Strategy
 * - Manual recording trigger via Microphone button
 * - No time limit countdown
 * - Replay question audio allowed
 * - Idle Mic prompt shown
 */
export class PracticeInterviewStrategy extends BaseInterviewStrategy {
  get mode() {
    return InterviewMode.PRACTICE;
  }

  get isPractice() {
    return true;
  }

  get isReal() {
    return false;
  }

  get autoRecordAfterQuestionAudio() {
    return false;
  }

  get defaultCountdownSeconds() {
    return null;
  }

  get hasCountdownTimer() {
    return false;
  }

  get allowReplayAudio() {
    return true;
  }

  get showMicIdlePrompt() {
    return true;
  }

  get showAudioControls() {
    return true;
  }

  get forceAutoPlay() {
    return false;
  }

  async onQuestionAudioEnded() {
    // Practice mode does nothing on audio end; waits for user to click Mic.
  }

  async onTimerExpired() {
    // Practice mode has no timer.
  }
}

/**
 * Real Mode Strategy
 * - Automatically begins recording immediately after AI finishes reading the question
 * - Starts a 2-minute (120s) countdown timer
 * - Automatically stops recording & submits answer when timer hits 0s
 * - Disables question replay audio
 * - Hides idle Mic waiting prompt
 * - Forces autoPlay to true & hides manual audio control buttons
 */
export class RealInterviewStrategy extends BaseInterviewStrategy {
  get mode() {
    return InterviewMode.REAL;
  }

  get isPractice() {
    return false;
  }

  get isReal() {
    return true;
  }

  get autoRecordAfterQuestionAudio() {
    return true;
  }

  get defaultCountdownSeconds() {
    return 120; // 2 minutes
  }

  get hasCountdownTimer() {
    return true;
  }

  get allowReplayAudio() {
    return false;
  }

  get showMicIdlePrompt() {
    return false;
  }

  get showAudioControls() {
    return false;
  }

  get forceAutoPlay() {
    return true;
  }

  async onQuestionAudioEnded({ startRecording, startTimer } = {}) {
    // 1. Start recording immediately
    if (typeof startRecording === 'function') {
      try {
        await startRecording();
      } catch (err) {
        console.error('RealInterviewStrategy auto-record failed:', err);
      }
    }

    // 2. Start 2-minute countdown timer (120s)
    if (typeof startTimer === 'function') {
      startTimer(120);
    }
  }

  async onTimerExpired({ stopRecording, submitAnswer } = {}) {
    // When 2-minute timer expires: stop recording -> process audio -> submit for evaluation
    if (typeof stopRecording === 'function') {
      try {
        await stopRecording();
      } catch (err) {
        console.error('RealInterviewStrategy stop recording failed:', err);
      }
    }

    if (typeof submitAnswer === 'function') {
      try {
        await submitAnswer();
      } catch (err) {
        console.error('RealInterviewStrategy auto submit failed:', err);
      }
    }
  }
}

/**
 * Strategy Resolver Factory
 * Returns singleton/new strategy instance based on normalized mode string
 */
const practiceStrategyInstance = new PracticeInterviewStrategy();
const realStrategyInstance = new RealInterviewStrategy();

export function getInterviewStrategy(modeInput) {
  const normalized = normalizeInterviewMode(modeInput);
  if (normalized === InterviewMode.REAL) {
    return realStrategyInstance;
  }
  return practiceStrategyInstance;
}
