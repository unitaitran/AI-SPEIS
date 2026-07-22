import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  Clipboard,
  Info,
  Loader2,
  Mic,
  Radio,
  RefreshCw,
  Sparkles,
  Wifi,
  XCircle,
} from 'lucide-react';
import InterviewProgressStepper from '../../components/user/InterviewProgressStepper/InterviewProgressStepper';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { getCodingInterviewRoomPath, getInterviewRoomPath, USER_ROUTES } from '../../routes/routePaths';
import audioService from '../../services/AudioService';
import behavioralInterviewApi from '../../services/behavioralInterviewApi';
import interviewSessionService from '../../services/InterviewSessionService';
import { calculateAccuracy } from '../../utils/stringUtils';
import {
  getActiveInterviewContext,
  getInterviewSetupDraft,
  getNextPendingSession,
  saveActiveInterviewContext,
} from '../../utils/interviewContext';
import '../../styles/user/DeviceReadinessCheckPage.css';

const CHECK_STATUS = Object.freeze({
  CHECKING: 'checking',
  PASSED: 'passed',
  FAILED: 'failed',
  WARNING: 'warning',
});

const REQUIRED_CHECK_IDS = ['microphone', 'recording'];
const VOICE_ACTIVITY_THRESHOLD = 0.025;

const createCheckingState = (t) => ({
  microphone: {
    status: CHECK_STATUS.CHECKING,
    title: t('device.microphone'),
    detail: t('device.checkingMicrophone'),
    meta: t('device.required'),
    required: true,
  },
  recording: {
    status: CHECK_STATUS.CHECKING,
    title: t('device.recording'),
    detail: t('device.preparingRecording'),
    meta: t('device.noAudioSaved'),
    required: true,
  },
  network: {
    status: CHECK_STATUS.CHECKING,
    title: t('device.network'),
    detail: t('device.checkingNetwork'),
    meta: t('device.recommended'),
    required: false,
  },
});

function stopMediaStream(stream) {
  if (!stream) return;

  stream.getTracks().forEach((track) => {
    track.stop();
  });
}

function stopRecorder(recorder) {
  if (!recorder || recorder.state === 'inactive') return;

  try {
    recorder.stop();
  } catch (error) {
    // Recorder can already be stopping; track cleanup is handled separately.
  }
}

function stopAudioContext(audioContext) {
  if (!audioContext || audioContext.state === 'closed') return;

  audioContext.close().catch(() => {
    // AudioContext can be closing already; MediaStream cleanup is handled separately.
  });
}

async function prepareInterviewRound(session) {
  if (session?.interviewRoundType === 'Behavior') {
    await behavioralInterviewApi.start(session.interviewSessionId);
  }
}

function getMediaSupport() {
  const hasNavigator = typeof navigator !== 'undefined';
  const hasMediaDevices = Boolean(hasNavigator && navigator.mediaDevices);
  const hasGetUserMedia = Boolean(hasMediaDevices && navigator.mediaDevices.getUserMedia);
  const hasMediaRecorder = typeof window !== 'undefined' && typeof window.MediaRecorder === 'function';

  return {
    hasMediaDevices,
    hasGetUserMedia,
    hasMediaRecorder,
  };
}

function getMicrophoneError(error, t) {
  const name = error?.name || '';

  if (name === 'NotAllowedError' || name === 'PermissionDeniedError' || name === 'SecurityError') {
    return {
      title: t('device.permissionDeniedTitle'),
      detail: t('device.permissionDeniedDetail'),
    };
  }

  if (name === 'NotFoundError' || name === 'DevicesNotFoundError') {
    return {
      title: t('device.notFoundTitle'),
      detail: t('device.notFoundDetail'),
    };
  }

  if (name === 'NotReadableError' || name === 'TrackStartError') {
    return {
      title: t('device.notReadyTitle'),
      detail: t('device.notReadyDetail'),
    };
  }

  return {
    title: t('device.accessFailedTitle'),
    detail: error?.message || t('device.accessFailedDetail'),
  };
}

function getNetworkCheck(t) {
  const connection =
    navigator.connection ||
    navigator.mozConnection ||
    navigator.webkitConnection;

  if (navigator.onLine === false) {
    return {
      status: CHECK_STATUS.WARNING,
      title: t('device.network'),
      detail: t('device.offline'),
      meta: t('common.warning'),
      required: false,
    };
  }

  const parts = [];

  if (connection?.rtt) {
    parts.push(t('device.ping', { rtt: connection.rtt }));
  }

  if (connection?.downlink) {
    parts.push(`${connection.downlink}Mbps`);
  }

  if (connection?.effectiveType) {
    parts.push(connection.effectiveType.toUpperCase());
  }

  return {
    status: CHECK_STATUS.PASSED,
    title: t('device.network'),
    detail: parts.length > 0 ? parts.join(' | ') : t('device.online'),
    meta: t('common.passed'),
    required: false,
  };
}

function getStatusLabel(status, fallback, t) {
  if (fallback) return fallback;

  switch (status) {
    case CHECK_STATUS.CHECKING:
      return t('common.checking');
    case CHECK_STATUS.PASSED:
      return t('common.passed');
    case CHECK_STATUS.FAILED:
      return t('common.failed');
    case CHECK_STATUS.WARNING:
      return t('common.warning');
    default:
      return t('common.unknown');
  }
}

function StatusBadge({ status, label, t }) {
  const isChecking = status === CHECK_STATUS.CHECKING;
  const Icon = isChecking
    ? Loader2
    : status === CHECK_STATUS.PASSED
      ? CheckCircle2
      : status === CHECK_STATUS.FAILED
        ? XCircle
        : AlertTriangle;

  return (
    <span className={`device-status-badge device-status-badge--${status}`}>
      <Icon size={14} className={isChecking ? 'device-spin' : ''} />
      {getStatusLabel(status, label, t)}
    </span>
  );
}

function ReadinessCard({ icon: Icon, check, t }) {
  return (
    <article className={`device-card device-card--${check.status}`}>
      <div className="device-card-top">
        <div className="device-card-icon" aria-hidden="true">
          <Icon size={24} />
        </div>
        <StatusBadge status={check.status} label={check.meta} t={t} />
      </div>
      <div>
        <h2>{check.title}</h2>
        <p>{check.detail}</p>
      </div>
      <span className="device-card-footnote">
        {check.required ? t('device.requiredFootnote') : t('device.optionalFootnote')}
      </span>
    </article>
  );
}

function DeviceReadinessCheckPage() {
  const [interviewContext, setInterviewContext] = useState(() => getActiveInterviewContext());
  const configuredLanguage = interviewContext?.campaign?.language || getInterviewSetupDraft()?.language;
  const interviewLanguage = configuredLanguage === 'en' ? 'en' : 'vi';
  const { t: translate } = useTranslation('interview');
  const t = useCallback((key, options = {}) => (
    translate(key, { ...options, lng: interviewLanguage })
  ), [interviewLanguage, translate]);
  const sampleText = t('device.sampleText');
  const [checks, setChecks] = useState(() => createCheckingState(t));
  const [isChecking, setIsChecking] = useState(false);
  const [message, setMessage] = useState(null);
  const [voiceActive, setVoiceActive] = useState(false);
  const [transcript, setTranscript] = useState(null);
  const [accuracy, setAccuracy] = useState(null);
  const [isRecording, setIsRecording] = useState(false);
  const [liveTranscript, setLiveTranscript] = useState('');
  const [contextError, setContextError] = useState('');
  const [isStartingSession, setIsStartingSession] = useState(false);
  const recordingChunksRef = useRef([]);
  const speechRecognitionRef = useRef(null);
  const activeStreamRef = useRef(null);
  const activeRecorderRef = useRef(null);
  const activeAudioContextRef = useRef(null);
  const voiceActivityFrameRef = useRef(null);
  const runIdRef = useRef(0);

  useEffect(() => {
    const sessions = interviewContext?.campaign?.sessions || [];
    const isOnlyCoding = sessions.length > 0 && sessions.every((s) => s.interviewRoundType === 'Code');
    if (isOnlyCoding) {
      const codingSession = sessions.find((s) => s.interviewRoundType === 'Code') || sessions[0];
      if (codingSession) {
        saveActiveInterviewContext({
          campaign: interviewContext.campaign,
          activeSessionId: codingSession.interviewSessionId,
          configurationKey: interviewContext.configurationKey,
        });
        navigate(getCodingInterviewRoomPath(codingSession.interviewSessionId), { replace: true });
      }
    }
  }, [interviewContext]);

  const cleanupActiveMedia = useCallback(() => {
    if (speechRecognitionRef.current) {
      speechRecognitionRef.current.stop();
      speechRecognitionRef.current = null;
    }
    stopRecorder(activeRecorderRef.current);
    activeRecorderRef.current = null;
    stopAudioContext(activeAudioContextRef.current);
    activeAudioContextRef.current = null;
    if (voiceActivityFrameRef.current !== null) {
      window.cancelAnimationFrame(voiceActivityFrameRef.current);
      voiceActivityFrameRef.current = null;
    }
    stopMediaStream(activeStreamRef.current);
    activeStreamRef.current = null;
  }, []);

  const updateCheck = useCallback((id, nextCheck) => {
    setChecks((current) => ({
      ...current,
      [id]: {
        ...current[id],
        ...nextCheck,
      },
    }));
  }, []);

  const startRecording = useCallback(() => {
    if (!activeStreamRef.current) {
      setMessage({ type: 'warning', text: t('device.microphoneMissing') });
      return;
    }
    
    setIsChecking(true);
    setIsRecording(true);
    setVoiceActive(false);
    setTranscript(null);
    setLiveTranscript('');
    setAccuracy(null);
    setMessage({ type: 'info', text: t('device.recordingNow') });
    updateCheck('recording', { status: CHECK_STATUS.CHECKING, detail: t('device.recordingShort'), meta: t('device.recording') });
    
    recordingChunksRef.current = [];
    
    try {
      const recorder = new MediaRecorder(activeStreamRef.current);
      activeRecorderRef.current = recorder;
      
      const AudioContextConstructor = window.AudioContext || window.webkitAudioContext;
      if (AudioContextConstructor) {
        const audioContext = new AudioContextConstructor();
        activeAudioContextRef.current = audioContext;
        const source = audioContext.createMediaStreamSource(activeStreamRef.current);
        const analyser = audioContext.createAnalyser();
        analyser.fftSize = 1024;
        analyser.smoothingTimeConstant = 0.72;
        source.connect(analyser);
        const frameData = new Uint8Array(analyser.fftSize);
        
        let previousVoiceActive = false;
        const watchVoiceActivity = () => {
          analyser.getByteTimeDomainData(frameData);
          let total = 0;
          for (let index = 0; index < frameData.length; index += 1) {
            const centeredSample = (frameData[index] - 128) / 128;
            total += centeredSample * centeredSample;
          }
          const rms = Math.sqrt(total / frameData.length);
          const nextVoiceActive = rms >= VOICE_ACTIVITY_THRESHOLD;
          if (previousVoiceActive !== nextVoiceActive) {
            previousVoiceActive = nextVoiceActive;
            setVoiceActive(nextVoiceActive);
          }
          voiceActivityFrameRef.current = window.requestAnimationFrame(watchVoiceActivity);
        };
        watchVoiceActivity();
      }

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          recordingChunksRef.current.push(event.data);
        }
      };

      recorder.onstop = async () => {
        stopAudioContext(activeAudioContextRef.current);
        activeAudioContextRef.current = null;
        if (voiceActivityFrameRef.current !== null) {
          window.cancelAnimationFrame(voiceActivityFrameRef.current);
          voiceActivityFrameRef.current = null;
        }
        setVoiceActive(false);
        setIsRecording(false);
        
        if (recordingChunksRef.current.length === 0) {
           updateCheck('recording', { status: CHECK_STATUS.FAILED, detail: t('device.noAudioData'), meta: t('common.failed') });
           setIsChecking(false);
           return;
        }

        const blob = new Blob(recordingChunksRef.current, { type: 'audio/webm' });
        setMessage({ type: 'info', text: t('device.checkingAccuracy') });
        
        try {
          const { transcript: resultText } = await audioService.checkSpeechToText(
            blob,
            interviewLanguage === 'en' ? 'en-US' : 'vi-VN',
          );
          setTranscript(resultText);
          const acc = calculateAccuracy(sampleText, resultText);
          setAccuracy(acc);

          if (acc >= 70) {
            updateCheck('recording', {
              status: CHECK_STATUS.PASSED,
              detail: t('device.accuracyPassed', { accuracy: acc }),
              meta: t('common.passed'),
            });
            setMessage({ type: 'success', text: t('device.allPassed') });
          } else {
            updateCheck('recording', {
              status: CHECK_STATUS.FAILED,
              detail: t('device.accuracyFailed', { accuracy: acc }),
              meta: t('common.failed'),
            });
            setMessage({ type: 'error', text: t('device.readAgain') });
          }
        } catch (error) {
          updateCheck('recording', {
            status: CHECK_STATUS.FAILED,
            detail: t('device.serverError', { message: error.message }),
            meta: t('common.failed'),
          });
          setMessage({ type: 'error', text: t('device.sttFailed') });
        } finally {
          setIsChecking(false);
        }
      };

      recorder.start(100);

      // Start live transcription using Web Speech API if supported
      const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
      if (SpeechRecognition) {
        const recognition = new SpeechRecognition();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = interviewLanguage === 'en' ? 'en-US' : 'vi-VN';
        
        recognition.onresult = (event) => {
          let fullTranscript = '';
          for (let i = 0; i < event.results.length; ++i) {
            fullTranscript += event.results[i][0].transcript;
          }
          setLiveTranscript(fullTranscript.trim());
        };

        recognition.start();
        speechRecognitionRef.current = recognition;
      }
    } catch (err) {
      setIsChecking(false);
      setIsRecording(false);
      setMessage({ type: 'error', text: t('device.recorderFailed') });
    }
  }, [interviewLanguage, sampleText, t, updateCheck]);

  const stopRecording = useCallback(() => {
    if (speechRecognitionRef.current) {
      speechRecognitionRef.current.stop();
      speechRecognitionRef.current = null;
    }
    if (activeRecorderRef.current && activeRecorderRef.current.state !== 'inactive') {
      activeRecorderRef.current.stop();
    }
  }, []);

    const runReadinessCheck = useCallback(async () => {
    const runId = runIdRef.current + 1;
    runIdRef.current = runId;

    cleanupActiveMedia();
    setIsChecking(false);
    setIsRecording(false);
    setVoiceActive(false);
    setTranscript(null);
    setAccuracy(null);
    setMessage({
      type: 'info',
      text: t('device.checkingDevices'),
    });
    
    // Set recording check to default state waiting for user interaction
    const nextChecks = createCheckingState(t);
    nextChecks.network = getNetworkCheck(t);
    nextChecks.recording = {
      status: CHECK_STATUS.WARNING,
      title: t('device.recording'),
      detail: t('device.startRecordingHint'),
      meta: t('device.needsCheck'),
      required: true,
    };
    setChecks(nextChecks);

    const support = getMediaSupport();

    if (!support.hasMediaDevices || !support.hasGetUserMedia) {
      setChecks((current) => ({
        ...current,
        microphone: {
          ...current.microphone,
          status: CHECK_STATUS.FAILED,
          detail: t('device.mediaApiUnsupported'),
          meta: t('common.failed'),
        },
        recording: {
          ...current.recording,
          status: CHECK_STATUS.FAILED,
          detail: t('device.recordingApiMissing'),
          meta: t('common.failed'),
        },
      }));
      setMessage({ type: 'error', text: t('device.browserUnsupported') });
      return;
    }

    let stream = null;

    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      if (runIdRef.current !== runId) {
        stopMediaStream(stream);
        return;
      }
      activeStreamRef.current = stream;

      const audioTrack = stream.getAudioTracks()[0];
      const microphoneLabel = audioTrack?.label || t('device.defaultMicrophone');
      updateCheck('microphone', {
        status: CHECK_STATUS.PASSED,
        detail: microphoneLabel,
        meta: t('common.passed'),
      });

      if (!support.hasMediaRecorder) {
        updateCheck('recording', {
          status: CHECK_STATUS.FAILED,
          detail: t('device.mediaRecorderUnsupported'),
          meta: t('common.failed'),
        });
        setMessage({ type: 'error', text: t('device.mediaRecorderUnsupported') });
        return;
      }

      setMessage({ type: 'info', text: t('device.clickRecord') });
      
    } catch (error) {
      if (runIdRef.current !== runId) return;
      
      const microphoneError = getMicrophoneError(error, t);
      updateCheck('microphone', {
        status: CHECK_STATUS.FAILED,
        detail: microphoneError.detail,
        meta: t('common.failed'),
      });
      updateCheck('recording', {
        status: CHECK_STATUS.FAILED,
        detail: t('device.recordingBlocked'),
        meta: t('common.failed'),
      });
      setMessage({ type: 'error', text: microphoneError.title });
    }
  }, [cleanupActiveMedia, t, updateCheck]);

  useEffect(() => {
    runReadinessCheck();

    return () => {
      runIdRef.current += 1;
      cleanupActiveMedia();
    };
  }, [cleanupActiveMedia, runReadinessCheck]);

  useEffect(() => {
    const syncNetwork = () => {
      updateCheck('network', getNetworkCheck(t));
    };

    window.addEventListener('online', syncNetwork);
    window.addEventListener('offline', syncNetwork);

    return () => {
      window.removeEventListener('online', syncNetwork);
      window.removeEventListener('offline', syncNetwork);
    };
  }, [t, updateCheck]);

  useEffect(() => {
    const storedContext = getActiveInterviewContext();
    const campaignId = storedContext?.campaign?.interviewCampaignId;

    if (!campaignId) {
      setContextError(t('device.campaignMissing'));
      return undefined;
    }

    let isMounted = true;
    interviewSessionService.getCampaign(campaignId)
      .then((campaign) => {
        if (!isMounted) return;
        if (campaign.status === 'Expired' || campaign.status === 'Cancelled' || campaign.status === 'Completed') {
          setContextError(t('device.campaignInvalid', { status: campaign.status }));
          return;
        }
        const nextContext = {
          campaign,
          activeSessionId: storedContext.activeSessionId || null,
          configurationKey: storedContext.configurationKey,
        };
        saveActiveInterviewContext(nextContext);
        setInterviewContext(nextContext);
        setContextError('');
      })
      .catch((error) => {
        if (!isMounted) return;
        setContextError(error.message || t('device.campaignLoadFailed'));
      });

    return () => {
      isMounted = false;
    };
  }, [t]);

  const requiredPassed = useMemo(() => {
    return REQUIRED_CHECK_IDS.every((id) => checks[id]?.status === CHECK_STATUS.PASSED);
  }, [checks]);

  const hasFailure = useMemo(() => {
    return Object.values(checks).some((check) => check.status === CHECK_STATUS.FAILED);
  }, [checks]);

  const panelState = requiredPassed ? CHECK_STATUS.PASSED : hasFailure ? CHECK_STATUS.FAILED : CHECK_STATUS.CHECKING;

  const handleContinue = async () => {
    if (!requiredPassed) {
      setMessage({
        type: 'warning',
        text: t('device.checksRequired'),
      });
      return;
    }

    const campaign = interviewContext?.campaign;
    if (!campaign) {
      setContextError(t('device.campaignMissing'));
      return;
    }

    const activeSession = (campaign.sessions || []).find((session) => session.status === 'Active');

    if (activeSession?.status === 'Active') {
      setIsStartingSession(true);
      setContextError('');
      try {
        await prepareInterviewRound(activeSession);
        const nextContext = {
          campaign,
          activeSessionId: activeSession.interviewSessionId,
          configurationKey: interviewContext.configurationKey,
        };
        saveActiveInterviewContext(nextContext);
        navigate(getInterviewRoomPath(activeSession.interviewSessionId));
      } catch (error) {
        setContextError(error.message || t('device.startFailed'));
      } finally {
        setIsStartingSession(false);
      }
      return;
    }

    const pendingSession = getNextPendingSession(campaign);
    if (!pendingSession) {
      setContextError(t('device.noPendingSession'));
      return;
    }

    setIsStartingSession(true);
    setContextError('');

    try {
      const startedCampaign = await interviewSessionService.startSession(pendingSession.interviewSessionId);
      const startedSession = (startedCampaign.sessions || [])
        .find((candidate) => candidate.status === 'Active');
      if (!startedSession) {
        throw new Error(t('device.activeSessionMissing'));
      }
      await prepareInterviewRound(startedSession);
      const nextContext = {
        campaign: startedCampaign,
        activeSessionId: startedSession.interviewSessionId,
        configurationKey: interviewContext.configurationKey,
      };

      saveActiveInterviewContext(nextContext);
      setInterviewContext(nextContext);
      navigate(getInterviewRoomPath(startedSession.interviewSessionId));
    } catch (error) {
      setContextError(error.message || t('device.startFailed'));
    } finally {
      setIsStartingSession(false);
    }
  };

  const handleCopySample = async () => {
    if (!navigator.clipboard) return;

    try {
      await navigator.clipboard.writeText(sampleText);
      setMessage({
        type: 'success',
        text: t('device.copied'),
      });
    } catch {
      setMessage({
        type: 'warning',
        text: t('device.copyFailed'),
      });
    }
  };

  return (
    <UserLayout>
      <div className="device-page animate-pageEntrance" lang={interviewLanguage}>
        <header className="device-header">
          <div>
            <h1>{t('device.title')}</h1>
            <p>{t('device.subtitle')}</p>
          </div>
          <button
            type="button"
            className="device-retry-button"
            onClick={runReadinessCheck}
            disabled={isChecking}
          >
            <RefreshCw size={18} className={isChecking ? 'device-spin' : ''} />
            {t('device.refresh')}
          </button>
        </header>

        <InterviewProgressStepper activeStep={2} language={interviewLanguage} />

        {contextError ? (
          <div className="device-alert device-alert--error" role="alert">
            <XCircle size={18} />
            <span>{contextError}</span>
          </div>
        ) : null}

        {message && message.type !== 'info' && (
          <div className={`device-alert device-alert--${message.type}`} role="status">
            {message.type === 'error' ? (
              <XCircle size={18} />
            ) : message.type === 'warning' ? (
              <AlertTriangle size={18} />
            ) : message.type === 'success' ? (
              <CheckCircle2 size={18} />
            ) : (
              <Info size={18} />
            )}
            <span>{message.text}</span>
          </div>
        )}

        <section className="device-card-grid" aria-label={t('device.statusAria')}>
          <ReadinessCard icon={Mic} check={checks.microphone} t={t} />
          <ReadinessCard icon={Radio} check={checks.recording} t={t} />
          <ReadinessCard icon={Wifi} check={checks.network} t={t} />
        </section>

        <section className="device-main-grid">
          <div className={`device-recording-panel device-recording-panel--${panelState}`}>
            <div className="device-recording-icon" aria-hidden="true">
              {panelState === CHECK_STATUS.FAILED ? <XCircle size={34} /> : <Mic size={34} />}
            </div>
            <div>
              <h2>{requiredPassed ? t('device.readyTitle') : isChecking ? t('device.recordingTitle') : t('device.retryTitle')}</h2>
              <p>
                {requiredPassed
                  ? t('device.readyDescription')
                  : t('device.retryDescription')}
              </p>
            </div>
            {checks.microphone.status === CHECK_STATUS.PASSED && (
              <div className="flex gap-2">
                {!isRecording ? (
                  <button
                    type="button"
                    className="device-panel-retry-button"
                    onClick={startRecording}
                    disabled={isChecking && !isRecording}
                  >
                    <Mic size={18} />
                    {t('device.startRecording')}
                  </button>
                ) : (
                  <button
                    type="button"
                    className="device-panel-retry-button !bg-red-500 !text-white !border-red-500 hover:!bg-red-600"
                    onClick={stopRecording}
                  >
                    <Radio size={18} />
                    {t('device.stopRecording')}
                  </button>
                )}
              </div>
            )}
            <div className={`device-waveform${voiceActive ? ' device-waveform--active' : ''}`} aria-hidden="true">
              {Array.from({ length: 15 }).map((_, index) => (
                <span key={index} />
              ))}
            </div>
          </div>

          <aside className="device-script-panel">
            <div className="device-script-header">
              <span>{t('device.sampleTitle')}</span>
              <button type="button" onClick={handleCopySample} aria-label={t('device.copySampleAria')}>
                <Clipboard size={18} />
              </button>
            </div>
            <blockquote>{sampleText}</blockquote>
            <div className="device-transcript-preview">
              <div className="flex justify-between items-center mb-1">
                <span className="text-sm font-medium">{t('device.liveTranscript')}</span>
                {accuracy !== null && (
                  <span className={`text-xs px-2 py-0.5 rounded-full ${accuracy >= 70 ? 'bg-success/20 text-success' : 'bg-error/20 text-error'}`}>
                    {t('device.accuracy', { accuracy })}
                  </span>
                )}
              </div>
              <strong className="text-sm">
                {isRecording
                  ? (liveTranscript ? liveTranscript : t('device.listening'))
                  : isChecking
                    ? (transcript !== null ? transcript : t('device.processing'))
                    : transcript !== null
                      ? (transcript !== '' ? transcript : t('device.emptyTranscript'))
                      : requiredPassed
                        ? t('device.readyForInterview')
                        : t('device.notReadyForInterview')}
              </strong>
            </div>
          </aside>
        </section>

        <footer className="device-action-bar">
          <div className="device-note">
            <Info size={22} />
            <div>
              <strong>{t('device.privacyTitle')}</strong>
              <p>{t('device.privacyDescription')}</p>
            </div>
          </div>
          <div className="device-actions">
            <button
              type="button"
              className="device-secondary-button"
              onClick={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
            >
              <ArrowLeft size={18} />
              {t('common.back')}
            </button>
            <button
              type="button"
              className="device-primary-button"
              onClick={handleContinue}
              disabled={!requiredPassed || isChecking || isStartingSession || !interviewContext?.campaign}
            >
              {isStartingSession ? (
                <>
                  <Loader2 size={20} className="device-spin" />
                  {t('device.starting')}
                </>
              ) : (
                <>
                  {t('common.continue')}
                  <ArrowRight size={20} />
                </>
              )}
            </button>
          </div>
        </footer>

        {isStartingSession && (
          <div className="device-setup-modal-overlay" role="dialog" aria-modal="true" aria-label={t('device.setupModalTitle')}>
            <div className="device-setup-modal">
              <div className="device-setup-modal-icon">
                <Loader2 size={44} className="device-spin" />
              </div>
              <h2>{t('device.setupModalTitle')}</h2>
              <p>{t('device.setupModalDescription')}</p>
              <div className="device-setup-modal-tip">
                <Sparkles size={16} />
                <span>{t('device.setupModalTip')}</span>
              </div>
            </div>
          </div>
        )}
      </div>
    </UserLayout>
  );
}

export default DeviceReadinessCheckPage;
