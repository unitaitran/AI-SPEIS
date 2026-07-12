import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
  Wifi,
  XCircle,
} from 'lucide-react';
import InterviewProgressStepper from '../../components/user/InterviewProgressStepper/InterviewProgressStepper';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import audioService from '../../services/AudioService';
import interviewSessionService from '../../services/InterviewSessionService';
import { calculateAccuracy } from '../../utils/stringUtils';
import {
  getActiveInterviewContext,
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
const VOICE_ACTIVITY_REQUIRED_FRAMES = 5;

const SAMPLE_TEXT =
  'Xin chào, tôi đang chuẩn bị phỏng vấn và tôi sẽ vượt qua nó';

const CHECKING_STATE = Object.freeze({
  microphone: {
    status: CHECK_STATUS.CHECKING,
    title: 'Microphone',
    detail: 'Đang kiểm tra quyền truy cập microphone...',
    meta: 'Yêu cầu bắt buộc',
    required: true,
  },
  recording: {
    status: CHECK_STATUS.CHECKING,
    title: 'Ghi âm thử',
    detail: 'Đang chuẩn bị phiên ghi âm ngắn bằng MediaRecorder...',
    meta: 'Không lưu audio',
    required: true,
  },
  network: {
    status: CHECK_STATUS.CHECKING,
    title: 'Kết nối mạng',
    detail: 'Đang đọc trạng thái kết nối từ trình duyệt...',
    meta: 'Khuyến nghị',
    required: false,
  },
});

function cloneCheckingState() {
  return {
    microphone: { ...CHECKING_STATE.microphone },
    recording: { ...CHECKING_STATE.recording },
    network: { ...CHECKING_STATE.network },
  };
}

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

function createNoVoiceDetectedError() {
  const error = new Error('Không nhận diện được tín hiệu giọng nói trong phiên ghi âm thử.');
  error.name = 'NoVoiceDetectedError';
  return error;
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

function getMicrophoneError(error) {
  const name = error?.name || '';

  if (name === 'NotAllowedError' || name === 'PermissionDeniedError' || name === 'SecurityError') {
    return {
      title: 'Bạn đã từ chối quyền microphone.',
      detail: 'Hãy cho phép quyền microphone trong trình duyệt rồi bấm Retry Check để kiểm tra lại.',
    };
  }

  if (name === 'NotFoundError' || name === 'DevicesNotFoundError') {
    return {
      title: 'Không tìm thấy microphone.',
      detail: 'Vui lòng kết nối microphone hoặc chọn đúng thiết bị đầu vào trước khi thử lại.',
    };
  }

  if (name === 'NotReadableError' || name === 'TrackStartError') {
    return {
      title: 'Microphone chưa sẵn sàng.',
      detail: 'Thiết bị có thể đang được ứng dụng khác sử dụng. Hãy đóng ứng dụng đó rồi thử lại.',
    };
  }

  return {
    title: 'Không thể truy cập microphone.',
    detail: error?.message || 'Đã có lỗi khi trình duyệt xin quyền microphone.',
  };
}

function getNetworkCheck() {
  const connection =
    navigator.connection ||
    navigator.mozConnection ||
    navigator.webkitConnection;

  if (navigator.onLine === false) {
    return {
      status: CHECK_STATUS.WARNING,
      title: 'Kết nối mạng',
      detail: 'Trình duyệt đang báo offline. Bạn vẫn cần kết nối ổn định trước khi phỏng vấn.',
      meta: 'Warning',
      required: false,
    };
  }

  const parts = [];

  if (connection?.rtt) {
    parts.push(`Ping khoảng ${connection.rtt}ms`);
  }

  if (connection?.downlink) {
    parts.push(`${connection.downlink}Mbps`);
  }

  if (connection?.effectiveType) {
    parts.push(connection.effectiveType.toUpperCase());
  }

  return {
    status: CHECK_STATUS.PASSED,
    title: 'Kết nối mạng',
    detail: parts.length > 0 ? parts.join(' | ') : 'Trình duyệt đang online.',
    meta: 'Good',
    required: false,
  };
}

function getStatusLabel(status, fallback) {
  if (fallback) return fallback;

  switch (status) {
    case CHECK_STATUS.CHECKING:
      return 'Checking';
    case CHECK_STATUS.PASSED:
      return 'Passed';
    case CHECK_STATUS.FAILED:
      return 'Failed';
    case CHECK_STATUS.WARNING:
      return 'Warning';
    default:
      return 'Unknown';
  }
}

function StatusBadge({ status, label }) {
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
      {getStatusLabel(status, label)}
    </span>
  );
}

function ReadinessCard({ icon: Icon, check }) {
  return (
    <article className={`device-card device-card--${check.status}`}>
      <div className="device-card-top">
        <div className="device-card-icon" aria-hidden="true">
          <Icon size={24} />
        </div>
        <StatusBadge status={check.status} label={check.meta} />
      </div>
      <div>
        <h2>{check.title}</h2>
        <p>{check.detail}</p>
      </div>
      <span className="device-card-footnote">
        {check.required ? 'Bắt buộc để bắt đầu phỏng vấn' : 'Không chặn tiếp tục'}
      </span>
    </article>
  );
}

function DeviceReadinessCheckPage() {
  const [checks, setChecks] = useState(() => cloneCheckingState());
  const [isChecking, setIsChecking] = useState(false);
  const [message, setMessage] = useState(null);
  const [voiceActive, setVoiceActive] = useState(false);
  const [transcript, setTranscript] = useState(null);
  const [accuracy, setAccuracy] = useState(null);
  const [isRecording, setIsRecording] = useState(false);
  const [liveTranscript, setLiveTranscript] = useState('');
  const [interviewContext, setInterviewContext] = useState(() => getActiveInterviewContext());
  const [contextError, setContextError] = useState('');
  const [isStartingSession, setIsStartingSession] = useState(false);
  const recordingChunksRef = useRef([]);
  const speechRecognitionRef = useRef(null);
  const activeStreamRef = useRef(null);
  const activeRecorderRef = useRef(null);
  const activeAudioContextRef = useRef(null);
  const runIdRef = useRef(0);

  const cleanupActiveMedia = useCallback(() => {
    if (speechRecognitionRef.current) {
      speechRecognitionRef.current.stop();
      speechRecognitionRef.current = null;
    }
    stopRecorder(activeRecorderRef.current);
    activeRecorderRef.current = null;
    stopAudioContext(activeAudioContextRef.current);
    activeAudioContextRef.current = null;
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
      setMessage({ type: 'warning', text: 'Chưa kết nối được Microphone. Vui lòng cấp quyền trước.' });
      return;
    }
    
    setIsChecking(true);
    setIsRecording(true);
    setVoiceActive(false);
    setTranscript(null);
    setLiveTranscript('');
    setAccuracy(null);
    setMessage({ type: 'info', text: 'Đang ghi âm... Hãy đọc to đoạn văn mẫu bên phải.' });
    updateCheck('recording', { status: CHECK_STATUS.CHECKING, detail: 'Đang ghi âm...', meta: 'Recording' });
    
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
        
        let animationFrameId;
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
          animationFrameId = window.requestAnimationFrame(watchVoiceActivity);
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
        setVoiceActive(false);
        setIsRecording(false);
        
        if (recordingChunksRef.current.length === 0) {
           updateCheck('recording', { status: CHECK_STATUS.FAILED, detail: 'Không thu được dữ liệu âm thanh.', meta: 'Failed' });
           setIsChecking(false);
           return;
        }

        const blob = new Blob(recordingChunksRef.current, { type: 'audio/webm' });
        setMessage({ type: 'info', text: 'Đang gửi đoạn ghi âm để kiểm tra độ chính xác...' });
        
        try {
          const { transcript: resultText } = await audioService.checkSpeechToText(blob);
          setTranscript(resultText);
          const acc = calculateAccuracy(SAMPLE_TEXT, resultText);
          setAccuracy(acc);

          if (acc >= 70) {
            updateCheck('recording', {
              status: CHECK_STATUS.PASSED,
              detail: `Đã phân tích giọng nói (Độ chính xác: ${acc}%). Thiết bị hoạt động tốt.`,
              meta: 'Passed',
            });
            setMessage({ type: 'success', text: 'Các kiểm tra bắt buộc đã hoàn tất. Bạn có thể tiếp tục vào phòng phỏng vấn.' });
          } else {
            updateCheck('recording', {
              status: CHECK_STATUS.FAILED,
              detail: `Nội dung bạn đọc chưa đạt độ chính xác (Độ chính xác: ${acc}%). Vui lòng thử lại.`,
              meta: 'Failed',
            });
            setMessage({ type: 'error', text: 'Không nghe rõ hoặc đọc sai quá nhiều. Hãy đọc to, rõ đoạn văn mẫu và ghi âm lại.' });
          }
        } catch (error) {
          updateCheck('recording', {
            status: CHECK_STATUS.FAILED,
            detail: `Lỗi Server: ${error.message}`,
            meta: 'Failed',
          });
          setMessage({ type: 'error', text: 'Lỗi kết nối hoặc xử lý STT từ máy chủ. Vui lòng thử lại.' });
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
        recognition.lang = 'vi-VN';
        
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
      setMessage({ type: 'error', text: 'Không thể khởi chạy MediaRecorder.' });
    }
  }, [updateCheck]);

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
      text: 'Đang kiểm tra kết nối thiết bị...',
    });
    
    // Set recording check to default state waiting for user interaction
    const nextChecks = cloneCheckingState();
    nextChecks.network = getNetworkCheck();
    nextChecks.recording = {
      status: CHECK_STATUS.WARNING,
      title: 'Ghi âm thử',
      detail: 'Nhấn Bắt đầu ghi âm để thử nghiệm thu âm.',
      meta: 'Cần kiểm tra',
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
          detail: 'Trình duyệt không hỗ trợ navigator.mediaDevices hoặc getUserMedia.',
          meta: 'Failed',
        },
        recording: {
          ...current.recording,
          status: CHECK_STATUS.FAILED,
          detail: 'Không thể kiểm tra ghi âm vì trình duyệt thiếu API microphone.',
          meta: 'Failed',
        },
      }));
      setMessage({ type: 'error', text: 'Browser không hỗ trợ kiểm tra thiết bị.' });
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
      const microphoneLabel = audioTrack?.label || 'Microphone mặc định';
      updateCheck('microphone', {
        status: CHECK_STATUS.PASSED,
        detail: microphoneLabel,
        meta: 'Passed',
      });

      if (!support.hasMediaRecorder) {
        updateCheck('recording', {
          status: CHECK_STATUS.FAILED,
          detail: 'Trình duyệt không hỗ trợ MediaRecorder.',
          meta: 'Failed',
        });
        setMessage({ type: 'error', text: 'Browser không hỗ trợ MediaRecorder.' });
        return;
      }

      setMessage({ type: 'info', text: 'Hãy bấm nút Bắt đầu ghi âm ở bên dưới để thử mic.' });
      
    } catch (error) {
      if (runIdRef.current !== runId) return;
      
      const microphoneError = getMicrophoneError(error);
      updateCheck('microphone', {
        status: CHECK_STATUS.FAILED,
        detail: microphoneError.detail,
        meta: 'Failed',
      });
      updateCheck('recording', {
        status: CHECK_STATUS.FAILED,
        detail: 'Không thể kiểm tra ghi âm khi microphone chưa sẵn sàng.',
        meta: 'Blocked',
      });
      setMessage({ type: 'error', text: microphoneError.title });
    }
  }, [cleanupActiveMedia, updateCheck]);

  useEffect(() => {
    runReadinessCheck();

    return () => {
      runIdRef.current += 1;
      cleanupActiveMedia();
    };
  }, [cleanupActiveMedia, runReadinessCheck]);

  useEffect(() => {
    const syncNetwork = () => {
      updateCheck('network', getNetworkCheck());
    };

    window.addEventListener('online', syncNetwork);
    window.addEventListener('offline', syncNetwork);

    return () => {
      window.removeEventListener('online', syncNetwork);
      window.removeEventListener('offline', syncNetwork);
    };
  }, [updateCheck]);

  useEffect(() => {
    const storedContext = getActiveInterviewContext();
    const campaignId = storedContext?.campaign?.interviewCampaignId;

    if (!campaignId) {
      setContextError('Không tìm thấy campaign phỏng vấn. Vui lòng quay lại bước Thiết lập.');
      return undefined;
    }

    let isMounted = true;
    interviewSessionService.getCampaign(campaignId)
      .then((campaign) => {
        if (!isMounted) return;
        const nextContext = {
          campaign,
          activeSessionId: storedContext.activeSessionId || null,
        };
        saveActiveInterviewContext(nextContext);
        setInterviewContext(nextContext);
        setContextError('');
      })
      .catch((error) => {
        if (!isMounted) return;
        setContextError(error.message || 'Không thể tải campaign phỏng vấn.');
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const requiredPassed = useMemo(() => {
    return REQUIRED_CHECK_IDS.every((id) => checks[id]?.status === CHECK_STATUS.PASSED);
  }, [checks]);

  const hasFailure = useMemo(() => {
    return Object.values(checks).some((check) => check.status === CHECK_STATUS.FAILED);
  }, [checks]);

  const recordingFailed = checks.recording?.status === CHECK_STATUS.FAILED;
  const panelState = requiredPassed ? CHECK_STATUS.PASSED : hasFailure ? CHECK_STATUS.FAILED : CHECK_STATUS.CHECKING;

  const handleContinue = async () => {
    if (!requiredPassed) {
      setMessage({
        type: 'warning',
        text: 'Bạn cần hoàn tất các kiểm tra bắt buộc trước khi bắt đầu phỏng vấn.',
      });
      return;
    }

    const campaign = interviewContext?.campaign;
    if (!campaign) {
      setContextError('Không tìm thấy campaign phỏng vấn. Vui lòng quay lại bước Thiết lập.');
      return;
    }

    const activeSession = (campaign.sessions || []).find((session) => session.status === 'Active');

    if (activeSession?.status === 'Active') {
      const nextContext = {
        campaign,
        activeSessionId: activeSession.interviewSessionId,
      };
      saveActiveInterviewContext(nextContext);
      navigate(USER_ROUTES.INTERVIEW_ROOM);
      return;
    }

    const pendingSession = getNextPendingSession(campaign);
    if (!pendingSession) {
      setContextError('Campaign không còn phiên phỏng vấn đang chờ để bắt đầu.');
      return;
    }

    setIsStartingSession(true);
    setContextError('');

    try {
      const startedSession = await interviewSessionService.startSession(pendingSession.interviewSessionId);
      const updatedCampaign = {
        ...campaign,
        sessions: (campaign.sessions || []).map((session) => (
          session.interviewSessionId === startedSession.interviewSessionId
            ? startedSession
            : session
        )),
      };
      const nextContext = {
        campaign: updatedCampaign,
        activeSessionId: startedSession.interviewSessionId,
      };

      saveActiveInterviewContext(nextContext);
      setInterviewContext(nextContext);
      navigate(USER_ROUTES.INTERVIEW_ROOM);
    } catch (error) {
      setContextError(error.message || 'Không thể bắt đầu phiên phỏng vấn.');
    } finally {
      setIsStartingSession(false);
    }
  };

  const handleCopySample = async () => {
    if (!navigator.clipboard) return;

    try {
      await navigator.clipboard.writeText(SAMPLE_TEXT);
      setMessage({
        type: 'success',
        text: 'Đã sao chép đoạn văn mẫu.',
      });
    } catch {
      setMessage({
        type: 'warning',
        text: 'Không thể sao chép tự động trong trình duyệt này.',
      });
    }
  };

  return (
    <UserLayout>
      <div className="device-page animate-pageEntrance">
        <header className="device-header">
          <div>
            <h1>Kiểm tra phần cứng</h1>
            <p>
              Kiểm tra microphone và khả năng ghi âm ngắn trước khi vào phòng phỏng vấn AI.
            </p>
          </div>
          <button
            type="button"
            className="device-retry-button"
            onClick={runReadinessCheck}
            disabled={isChecking}
          >
            <RefreshCw size={18} className={isChecking ? 'device-spin' : ''} />
            Làm mới kết nối
          </button>
        </header>

        <InterviewProgressStepper activeStep={2} />

        {contextError ? (
          <div className="device-alert device-alert--error" role="alert">
            <XCircle size={18} />
            <span>{contextError}</span>
          </div>
        ) : interviewContext?.campaign ? (
          <div className="device-alert device-alert--info" role="status">
            <Info size={18} />
            <span>
              Campaign #{interviewContext.campaign.interviewCampaignId}
              {' · '}{interviewContext.campaign.durationMinutes} phút
              {' · '}{interviewContext.campaign.sessions?.length || 0} vòng
            </span>
          </div>
        ) : null}

        {message && (
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

        <section className="device-card-grid" aria-label="Device readiness status">
          <ReadinessCard icon={Mic} check={checks.microphone} />
          <ReadinessCard icon={Radio} check={checks.recording} />
          <ReadinessCard icon={Wifi} check={checks.network} />
        </section>

        <section className="device-main-grid">
          <div className={`device-recording-panel device-recording-panel--${panelState}`}>
            <div className="device-recording-icon" aria-hidden="true">
              {panelState === CHECK_STATUS.FAILED ? <XCircle size={34} /> : <Mic size={34} />}
            </div>
            <div>
              <h2>{requiredPassed ? 'Thiết bị đã sẵn sàng' : isChecking ? 'Thử ghi âm' : 'Cần kiểm tra lại'}</h2>
              <p>
                {requiredPassed
                  ? 'Microphone và MediaRecorder đã vượt qua kiểm tra bắt buộc.'
                  : 'Đọc đoạn văn mẫu ở bên phải trong môi trường yên tĩnh rồi bấm Retry Check nếu cần.'}
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
                    Bắt đầu ghi âm
                  </button>
                ) : (
                  <button
                    type="button"
                    className="device-panel-retry-button !bg-red-500 !text-white !border-red-500 hover:!bg-red-600"
                    onClick={stopRecording}
                  >
                    <Radio size={18} />
                    Kết thúc ghi âm
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
              <span>Đoạn văn mẫu</span>
              <button type="button" onClick={handleCopySample} aria-label="Sao chép đoạn văn mẫu">
                <Clipboard size={18} />
              </button>
            </div>
            <blockquote>{SAMPLE_TEXT}</blockquote>
            <div className="device-transcript-preview">
              <div className="flex justify-between items-center mb-1">
                <span className="text-sm font-medium">Transcript (Live)</span>
                {accuracy !== null && (
                  <span className={`text-xs px-2 py-0.5 rounded-full ${accuracy >= 70 ? 'bg-success/20 text-success' : 'bg-error/20 text-error'}`}>
                    Độ chính xác: {accuracy}%
                  </span>
                )}
              </div>
              <strong className="text-sm">
                {isRecording
                  ? (liveTranscript ? liveTranscript : 'Đang lắng nghe...')
                  : isChecking
                    ? (transcript !== null ? transcript : 'Đang xử lý phân tích âm thanh bằng AI...')
                    : transcript !== null
                      ? (transcript !== '' ? transcript : 'Không nhận diện được giọng nói (kết quả STT trống).')
                      : requiredPassed
                        ? 'Sẵn sàng ghi nhận câu trả lời trong phòng phỏng vấn.'
                        : 'Chưa thể bắt đầu khi kiểm tra bắt buộc chưa đạt.'}
              </strong>
            </div>
          </aside>
        </section>

        <footer className="device-action-bar">
          <div className="device-note">
            <Info size={22} />
            <div>
              <strong>Lưu ý</strong>
              <p>AI-SPEIS không upload, lưu hoặc persist audio trong bước kiểm tra thiết bị.</p>
            </div>
          </div>
          <div className="device-actions">
            <button
              type="button"
              className="device-secondary-button"
              onClick={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
            >
              <ArrowLeft size={18} />
              Quay lại
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
                  Đang bắt đầu
                </>
              ) : (
                <>
                  Tiếp tục
                  <ArrowRight size={20} />
                </>
              )}
            </button>
          </div>
        </footer>
      </div>
    </UserLayout>
  );
}

export default DeviceReadinessCheckPage;
