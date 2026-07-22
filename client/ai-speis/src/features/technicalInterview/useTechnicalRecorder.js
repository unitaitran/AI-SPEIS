import { useCallback, useEffect, useRef, useState } from 'react';
import audioService from '../../services/AudioService';
import { RecordingStatus, SttStatus } from './technicalInterview.types';

const stopStream = (stream) => {
  stream?.getTracks?.().forEach((track) => track.stop());
};

const getSupportedMimeType = () => {
  if (typeof MediaRecorder === 'undefined' || typeof MediaRecorder.isTypeSupported !== 'function') return '';
  return ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus']
    .find((type) => MediaRecorder.isTypeSupported(type)) || '';
};

export default function useTechnicalRecorder(language = 'vi') {
  const [recordingStatus, setRecordingStatus] = useState(RecordingStatus.IDLE);
  const [audioBlob, setAudioBlob] = useState(null);
  const [audioId, setAudioId] = useState(null);
  const [uploadStatus] = useState('IDLE');
  const [transcript, setTranscript] = useState('');
  const [sttStatus, setSttStatus] = useState(SttStatus.IDLE);
  const [sttError, setSttError] = useState(null);
  const [permissionError, setPermissionError] = useState(null);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const recorderRef = useRef(null);
  const streamRef = useRef(null);
  const chunksRef = useRef([]);
  const timerRef = useRef(null);
  const mountedRef = useRef(true);
  const startInFlightRef = useRef(false);
  const requestIdRef = useRef(0);
  const recognitionRef = useRef(null);
  const transcriptRef = useRef('');

  useEffect(() => {
    transcriptRef.current = transcript;
  }, [transcript]);

  const transcribe = useCallback(async (blob, requestId = requestIdRef.current) => {
    setSttError(null);
    setRecordingStatus(RecordingStatus.PROCESSING);
    setSttStatus(SttStatus.PROCESSING);
    try {
      const response = await audioService.checkSpeechToText(
        blob,
        language === 'en' ? 'en-US' : 'vi-VN',
      );
      if (!mountedRef.current || requestIdRef.current !== requestId) return null;
      if (response?.transcript?.trim()) {
        setTranscript(response.transcript);
      }
      setAudioId(response?.audioId || null);
      setSttStatus(SttStatus.COMPLETED);
      setRecordingStatus(RecordingStatus.READY);
      return response;
    } catch (error) {
      if (!mountedRef.current || requestIdRef.current !== requestId) return null;
      setSttError(error);
      setSttStatus(SttStatus.FAILED);
      setRecordingStatus(RecordingStatus.ERROR);
      throw error;
    }
  }, [language]);

  const clearTimer = useCallback(() => {
    if (timerRef.current) window.clearInterval(timerRef.current);
    timerRef.current = null;
  }, []);

  const cleanupMedia = useCallback(() => {
    requestIdRef.current += 1;
    startInFlightRef.current = false;
    clearTimer();
    if (recognitionRef.current) {
      try { recognitionRef.current.stop(); } catch { /* Already stopped */ }
      recognitionRef.current = null;
    }
    const recorder = recorderRef.current;
    if (recorder && recorder.state !== 'inactive') {
      recorder.ondataavailable = null;
      recorder.onstop = null;
      try { recorder.stop(); } catch { /* Recorder can already be stopping. */ }
    }
    recorderRef.current = null;
    stopStream(streamRef.current);
    streamRef.current = null;
  }, [clearTimer]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      cleanupMedia();
    };
  }, [cleanupMedia]);

  const startRecording = useCallback(async () => {
    if (startInFlightRef.current || recorderRef.current?.state === 'recording') return;
    startInFlightRef.current = true;
    setSttError(null);
    setPermissionError(null);
    setAudioBlob(null);
    setAudioId(null);
    setElapsedSeconds(0);

    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      startInFlightRef.current = false;
      setPermissionError(new Error('MEDIA_RECORDER_UNSUPPORTED'));
      setRecordingStatus(RecordingStatus.ERROR);
      return;
    }

    cleanupMedia();
    startInFlightRef.current = true;
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setRecordingStatus(RecordingStatus.REQUESTING_PERMISSION);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      if (!mountedRef.current || requestIdRef.current !== requestId) {
        stopStream(stream);
        return;
      }
      streamRef.current = stream;
      chunksRef.current = [];
      const mimeType = getSupportedMimeType();
      const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
      recorderRef.current = recorder;

      recorder.ondataavailable = (event) => {
        if (event.data?.size) chunksRef.current.push(event.data);
      };

      recorder.onstop = async () => {
        clearTimer();
        stopStream(streamRef.current);
        streamRef.current = null;
        recorderRef.current = null;
        if (!mountedRef.current) return;

        const currentRealtimeTranscript = transcriptRef.current?.trim() || '';

        if (!chunksRef.current.length && !currentRealtimeTranscript) {
          setRecordingStatus(RecordingStatus.ERROR);
          setSttStatus(SttStatus.FAILED);
          setSttError(new Error('NO_AUDIO_DATA'));
          return;
        }

        const blob = chunksRef.current.length
          ? new Blob(chunksRef.current, { type: recorder.mimeType || 'audio/webm' })
          : null;
        if (blob) setAudioBlob(blob);

        if (currentRealtimeTranscript) {
          setSttStatus(SttStatus.COMPLETED);
          setRecordingStatus(RecordingStatus.READY);
          if (blob) {
            audioService.checkSpeechToText(blob, language === 'en' ? 'en-US' : 'vi-VN')
              .then((response) => {
                if (mountedRef.current && response?.audioId) setAudioId(response.audioId);
              })
              .catch(() => {});
          }
          return;
        }

        if (blob) {
          try {
            await transcribe(blob, requestId);
          } catch { /* The retained audio can be transcribed again. */ }
        }
      };

      recorder.start(250);
      setRecordingStatus(RecordingStatus.RECORDING);
      timerRef.current = window.setInterval(() => {
        setElapsedSeconds((seconds) => seconds + 1);
      }, 1000);

      const SpeechRecognition = typeof window !== 'undefined'
        && (window.SpeechRecognition || window.webkitSpeechRecognition);
      if (SpeechRecognition) {
        try {
          const recognition = new SpeechRecognition();
          recognition.continuous = true;
          recognition.interimResults = true;
          recognition.lang = language === 'en' ? 'en-US' : 'vi-VN';

          recognition.onresult = (event) => {
            let fullText = '';
            for (let i = 0; i < event.results.length; i += 1) {
              fullText += event.results[i][0].transcript;
            }
            if (mountedRef.current && fullText.trim()) {
              setTranscript(fullText.trim());
            }
          };

          recognition.start();
          recognitionRef.current = recognition;
        } catch {
          // Ignore SpeechRecognition start errors
        }
      }
    } catch (error) {
      stopStream(streamRef.current);
      streamRef.current = null;
      setPermissionError(error);
      setRecordingStatus(RecordingStatus.ERROR);
    } finally {
      startInFlightRef.current = false;
    }
  }, [cleanupMedia, clearTimer, language, transcribe]);

  const stopRecording = useCallback(() => {
    if (recognitionRef.current) {
      try { recognitionRef.current.stop(); } catch { /* Ignore */ }
      recognitionRef.current = null;
    }
    const recorder = recorderRef.current;
    if (!recorder || recorder.state === 'inactive') return;
    setRecordingStatus(RecordingStatus.PROCESSING);
    recorder.stop();
  }, []);

  const reset = useCallback(() => {
    cleanupMedia();
    chunksRef.current = [];
    setRecordingStatus(RecordingStatus.IDLE);
    setAudioBlob(null);
    setAudioId(null);
    setTranscript('');
    setSttStatus(SttStatus.IDLE);
    setSttError(null);
    setPermissionError(null);
    setElapsedSeconds(0);
  }, [cleanupMedia]);

  const stopForSubmission = useCallback(() => {
    cleanupMedia();
    setRecordingStatus(transcript.trim() ? RecordingStatus.READY : RecordingStatus.IDLE);
    setSttStatus((current) => (
      current === SttStatus.PROCESSING ? SttStatus.IDLE : current
    ));
  }, [cleanupMedia, transcript]);

  const retryTranscription = useCallback(async () => {
    if (!audioBlob || sttStatus === SttStatus.PROCESSING) return null;
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    return transcribe(audioBlob, requestId);
  }, [audioBlob, sttStatus, transcribe]);

  return {
    recordingStatus,
    audioBlob,
    audioId,
    uploadStatus,
    transcript,
    sttStatus,
    sttError,
    permissionError,
    elapsedSeconds,
    startRecording,
    stopRecording,
    retryTranscription,
    setTranscript,
    stopForSubmission,
    reset,
    cleanup: cleanupMedia,
  };
}
