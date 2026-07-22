import { act, renderHook } from '@testing-library/react';
import audioService from '../../services/AudioService';
import useTechnicalRecorder from './useTechnicalRecorder';
import { RecordingStatus, SttStatus } from './technicalInterview.types';

jest.mock('../../services/AudioService', () => ({
  __esModule: true,
  default: {
    checkSpeechToText: jest.fn(),
  },
}));

class FakeMediaRecorder {
  static isTypeSupported() { return true; }

  constructor(stream) {
    this.stream = stream;
    this.state = 'inactive';
    this.mimeType = 'audio/webm';
    this.ondataavailable = null;
    this.onstop = null;
  }

  start() { this.state = 'recording'; }

  stop() {
    this.state = 'inactive';
    if (this.ondataavailable) this.ondataavailable({ data: new Blob(['recorded audio']) });
    if (this.onstop) this.onstop();
  }
}

describe('useTechnicalRecorder', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    delete window.SpeechRecognition;
    delete window.webkitSpeechRecognition;
  });

  test('stops the active microphone stream when the room unmounts', async () => {
    const stopTrack = jest.fn();
    const stream = { getTracks: () => [{ stop: stopTrack }] };
    Object.defineProperty(global.navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: jest.fn().mockResolvedValue(stream) },
    });
    global.MediaRecorder = FakeMediaRecorder;
    const { result, unmount } = renderHook(() => useTechnicalRecorder('vi'));

    await act(async () => {
      await result.current.startRecording();
    });
    expect(global.navigator.mediaDevices.getUserMedia).toHaveBeenCalledTimes(1);

    unmount();
    expect(stopTrack).toHaveBeenCalled();
  });

  test('stops and releases the microphone before submitting an existing transcript', async () => {
    const stopTrack = jest.fn();
    const stream = { getTracks: () => [{ stop: stopTrack }] };
    Object.defineProperty(global.navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: jest.fn().mockResolvedValue(stream) },
    });
    global.MediaRecorder = FakeMediaRecorder;
    const { result } = renderHook(() => useTechnicalRecorder('vi'));

    await act(async () => {
      await result.current.startRecording();
      result.current.setTranscript('Typed transcript remains available');
    });
    act(() => result.current.stopForSubmission());

    expect(stopTrack).toHaveBeenCalled();
    expect(result.current.transcript).toBe('Typed transcript remains available');
  });

  test('uses browser recognition only as preview and waits for Chirp 3 before becoming ready', async () => {
    const stream = { getTracks: () => [{ stop: jest.fn() }] };
    Object.defineProperty(global.navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: jest.fn().mockResolvedValue(stream) },
    });
    global.MediaRecorder = FakeMediaRecorder;

    class FakeSpeechRecognition {
      start() {
        this.onresult?.({
          results: [[{ transcript: 'Browser preview is inaccurate' }]],
        });
      }

      stop() {}
    }
    window.SpeechRecognition = FakeSpeechRecognition;

    let resolveChirp;
    audioService.checkSpeechToText.mockReturnValue(new Promise((resolve) => {
      resolveChirp = resolve;
    }));

    const { result } = renderHook(() => useTechnicalRecorder('vi'));
    await act(async () => result.current.startRecording());
    expect(result.current.transcript).toBe('Browser preview is inaccurate');

    act(() => result.current.stopRecording());
    expect(result.current.recordingStatus).toBe(RecordingStatus.PROCESSING);
    expect(result.current.sttStatus).toBe(SttStatus.PROCESSING);

    await act(async () => {
      resolveChirp({ transcript: 'Kết quả chính xác từ Chirp 3' });
      await Promise.resolve();
    });

    expect(result.current.transcript).toBe('Kết quả chính xác từ Chirp 3');
    expect(result.current.recordingStatus).toBe(RecordingStatus.READY);
    expect(result.current.sttStatus).toBe(SttStatus.COMPLETED);
  });

  test('does not submit a browser preview when Chirp 3 returns no transcript', async () => {
    const stream = { getTracks: () => [{ stop: jest.fn() }] };
    Object.defineProperty(global.navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: jest.fn().mockResolvedValue(stream) },
    });
    global.MediaRecorder = FakeMediaRecorder;
    audioService.checkSpeechToText.mockResolvedValue({ transcript: '   ' });

    const { result } = renderHook(() => useTechnicalRecorder('vi'));
    await act(async () => result.current.startRecording());
    act(() => result.current.setTranscript('Unverified browser preview'));
    await act(async () => result.current.stopRecording());

    expect(result.current.transcript).toBe('Unverified browser preview');
    expect(result.current.recordingStatus).toBe(RecordingStatus.ERROR);
    expect(result.current.sttStatus).toBe(SttStatus.FAILED);
    expect(result.current.sttError?.message).toBe('EMPTY_TRANSCRIPT');
  });
});
