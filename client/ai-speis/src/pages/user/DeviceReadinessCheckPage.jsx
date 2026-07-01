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
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import '../../styles/user/DeviceReadinessCheckPage.css';

const CHECK_STATUS = Object.freeze({
  CHECKING: 'checking',
  PASSED: 'passed',
  FAILED: 'failed',
  WARNING: 'warning',
});

const REQUIRED_CHECK_IDS = ['microphone', 'recording'];

const SAMPLE_TEXT =
  'Xin chào, tôi tên là [Tên của bạn]. Tôi đang thực hiện kiểm tra thiết bị cho nền tảng AI-SPEIS. Tôi xác nhận rằng tôi đang ở trong môi trường yên tĩnh và sẵn sàng cho buổi phỏng vấn.';

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

function Stepper() {
  const steps = ['Chế độ', 'Thiết lập', 'Kiểm tra thiết bị', 'Bắt đầu', 'Đánh giá', 'Kết quả'];

  return (
    <ol className="device-stepper" aria-label="Interview progress">
      {steps.map((step, index) => {
        const isActive = index === 2;

        return (
          <li className="device-stepper-item" key={step}>
            <span className={`device-step-number${isActive ? ' device-step-number--active' : ''}`}>
              {index + 1}
            </span>
            <span className="device-step-label">{step}</span>
          </li>
        );
      })}
    </ol>
  );
}

function DeviceReadinessCheckPage() {
  const [checks, setChecks] = useState(() => cloneCheckingState());
  const [isChecking, setIsChecking] = useState(false);
  const [message, setMessage] = useState(null);
  const activeStreamRef = useRef(null);
  const activeRecorderRef = useRef(null);
  const runIdRef = useRef(0);

  const cleanupActiveMedia = useCallback(() => {
    stopRecorder(activeRecorderRef.current);
    activeRecorderRef.current = null;
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

  const runRecordingProbe = useCallback((stream) => {
    return new Promise((resolve, reject) => {
      let hasSettled = false;
      let recorder;
      let stopTimer;
      let guardTimer;

      const settle = (callback) => {
        if (hasSettled) return;
        hasSettled = true;
        window.clearTimeout(stopTimer);
        window.clearTimeout(guardTimer);
        callback();
      };

      try {
        recorder = new MediaRecorder(stream);
        activeRecorderRef.current = recorder;
      } catch (error) {
        reject(error);
        return;
      }

      recorder.ondataavailable = () => {
        // Intentionally do not read, store, upload, or persist audio chunks.
      };

      recorder.onerror = (event) => {
        settle(() => reject(event.error || new Error('MediaRecorder failed while testing.')));
      };

      recorder.onstop = () => {
        settle(() => resolve());
      };

      try {
        recorder.start(100);

        stopTimer = window.setTimeout(() => {
          if (recorder.state !== 'inactive') {
            recorder.stop();
          }
        }, 700);

        guardTimer = window.setTimeout(() => {
          if (recorder.state !== 'inactive') {
            stopRecorder(recorder);
          }
          settle(() => reject(new Error('Recording readiness check timed out.')));
        }, 2500);
      } catch (error) {
        settle(() => reject(error));
      }
    });
  }, []);

  const runReadinessCheck = useCallback(async () => {
    const runId = runIdRef.current + 1;
    runIdRef.current = runId;

    cleanupActiveMedia();
    setIsChecking(true);
    setMessage({
      type: 'info',
      text: 'AI-SPEIS đang kiểm tra trình duyệt, microphone và khả năng ghi âm ngắn.',
    });
    setChecks({
      ...cloneCheckingState(),
      network: getNetworkCheck(),
    });

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
      setMessage({
        type: 'error',
        text: 'Browser không hỗ trợ kiểm tra thiết bị. Hãy dùng Chrome, Edge, Firefox hoặc Safari bản mới trên HTTPS/localhost.',
      });
      setIsChecking(false);
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
          detail: 'Trình duyệt không hỗ trợ MediaRecorder nên không thể bắt đầu phỏng vấn giọng nói.',
          meta: 'Failed',
        });
        setMessage({
          type: 'error',
          text: 'Browser không hỗ trợ MediaRecorder. Vui lòng chuyển sang trình duyệt hiện đại hơn.',
        });
        return;
      }

      await runRecordingProbe(stream);

      if (runIdRef.current !== runId) return;

      updateCheck('recording', {
        status: CHECK_STATUS.PASSED,
        detail: 'MediaRecorder đã tạo và dừng phiên ghi âm thử thành công.',
        meta: 'Passed',
      });
      setMessage({
        type: 'success',
        text: 'Các kiểm tra bắt buộc đã hoàn tất. Bạn có thể tiếp tục vào phòng phỏng vấn.',
      });
    } catch (error) {
      if (runIdRef.current !== runId) return;

      const supportError = error?.name === 'NotSupportedError';

      if (supportError || error?.message?.includes('Recording readiness')) {
        updateCheck('recording', {
          status: CHECK_STATUS.FAILED,
          detail: error?.message || 'Không thể khởi chạy MediaRecorder.',
          meta: 'Failed',
        });
        setMessage({
          type: 'error',
          text: 'Lỗi recording: trình duyệt không thể bắt đầu hoặc dừng ghi âm thử.',
        });
      } else {
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
        setMessage({
          type: 'error',
          text: microphoneError.title,
        });
      }
    } finally {
      stopRecorder(activeRecorderRef.current);
      stopMediaStream(stream);

      if (activeStreamRef.current === stream) {
        activeStreamRef.current = null;
      }

      activeRecorderRef.current = null;

      if (runIdRef.current === runId) {
        setIsChecking(false);
      }
    }
  }, [cleanupActiveMedia, runRecordingProbe, updateCheck]);

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

  const requiredPassed = useMemo(() => {
    return REQUIRED_CHECK_IDS.every((id) => checks[id]?.status === CHECK_STATUS.PASSED);
  }, [checks]);

  const hasFailure = useMemo(() => {
    return Object.values(checks).some((check) => check.status === CHECK_STATUS.FAILED);
  }, [checks]);

  const panelState = requiredPassed ? CHECK_STATUS.PASSED : hasFailure ? CHECK_STATUS.FAILED : CHECK_STATUS.CHECKING;

  const handleContinue = () => {
    if (!requiredPassed) {
      setMessage({
        type: 'warning',
        text: 'Bạn cần hoàn tất các kiểm tra bắt buộc trước khi bắt đầu phỏng vấn.',
      });
      return;
    }

    navigate(USER_ROUTES.INTERVIEW_ROOM);
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
            Retry Check
          </button>
        </header>

        <Stepper />

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
            <div className="device-waveform" aria-hidden="true">
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
              <span>Transcript (Live)</span>
              <strong>
                {isChecking
                  ? 'Đang chờ tín hiệu microphone...'
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
              onClick={() => navigate(USER_ROUTES.DASHBOARD)}
            >
              <ArrowLeft size={18} />
              Quay lại
            </button>
            <button
              type="button"
              className="device-primary-button"
              onClick={handleContinue}
              disabled={!requiredPassed || isChecking}
            >
              Tiếp tục
              <ArrowRight size={20} />
            </button>
          </div>
        </footer>
      </div>
    </UserLayout>
  );
}

export default DeviceReadinessCheckPage;
