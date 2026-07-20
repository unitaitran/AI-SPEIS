import { act, renderHook } from '@testing-library/react';
import useTechnicalRecorder from './useTechnicalRecorder';

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
    if (this.onstop) this.onstop();
  }
}

describe('useTechnicalRecorder', () => {
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
});
