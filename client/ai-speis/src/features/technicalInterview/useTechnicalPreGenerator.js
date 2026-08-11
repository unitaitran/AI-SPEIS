import { useCallback, useEffect, useRef, useState } from 'react';
import technicalInterviewApi from '../../services/technicalInterviewApi';

/**
 * Trạng thái Pre-Generation (đồng bộ với backend enum).
 */
export const PreGenerationStatus = Object.freeze({
  IDLE: 'Idle',
  GENERATING: 'Generating',
  COMPLETED: 'Completed',
  FAILED: 'Failed',
});

/**
 * Custom hook quản lý việc tạo trước câu hỏi Technical chạy ngầm.
 *
 * Sử dụng:
 * - Gọi `trigger(technicalSessionId)` khi câu hỏi Behavioral đầu tiên hiển thị.
 * - Hook sẽ tự gọi API, theo dõi trạng thái, và cleanup khi unmount.
 * - `cancel()` để hủy tiến trình ngầm (ví dụ khi user thoát phỏng vấn).
 */
export default function useTechnicalPreGenerator() {
  const [status, setStatus] = useState(PreGenerationStatus.IDLE);
  const [error, setError] = useState(null);
  const abortControllerRef = useRef(null);
  const triggeredSessionRef = useRef(null);
  const unmountedRef = useRef(false);

  // Cleanup khi component unmount
  useEffect(() => {
    unmountedRef.current = false;
    return () => {
      unmountedRef.current = true;
      abortControllerRef.current?.abort();
    };
  }, []);

  /**
   * Kích hoạt tạo trước câu hỏi Technical.
   * Idempotent: nếu đã trigger cho session này rồi thì không gọi lại.
   */
  const trigger = useCallback(async (technicalSessionId) => {
    if (!technicalSessionId) return;

    // Tránh trigger trùng lặp cho cùng 1 session
    if (triggeredSessionRef.current === technicalSessionId) return;
    triggeredSessionRef.current = technicalSessionId;

    // Tạo AbortController để hỗ trợ cancel
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setStatus(PreGenerationStatus.GENERATING);
    setError(null);

    try {
      const result = await technicalInterviewApi.preGenerate(
        technicalSessionId,
        { signal: controller.signal },
      );

      if (unmountedRef.current) return;

      // Map backend status sang frontend
      const backendStatus = result?.status;
      if (backendStatus === 'Completed' || backendStatus === 2) {
        setStatus(PreGenerationStatus.COMPLETED);
      } else if (backendStatus === 'Failed' || backendStatus === 3) {
        setStatus(PreGenerationStatus.FAILED);
        setError(result?.errorMessage || 'Pre-generation failed');
      } else {
        // Generating hoặc Idle → coi như đang chạy ngầm
        setStatus(PreGenerationStatus.GENERATING);
      }
    } catch (triggerError) {
      if (triggerError?.name === 'AbortError') return;
      if (unmountedRef.current) return;

      // Không block UI nếu pre-gen lỗi – fallback sẽ là sync initialization
      console.warn('[PreGen] Trigger failed (will fallback to sync init):', triggerError);
      setStatus(PreGenerationStatus.FAILED);
      setError(triggerError?.message || 'Pre-generation trigger failed');
    }
  }, []);

  /**
   * Hủy tiến trình tạo trước.
   */
  const cancel = useCallback(async (technicalSessionId) => {
    abortControllerRef.current?.abort();
    triggeredSessionRef.current = null;
    setStatus(PreGenerationStatus.IDLE);
    setError(null);

    if (technicalSessionId) {
      try {
        await technicalInterviewApi.cancelPreGenerate(technicalSessionId);
      } catch {
        // Best-effort cancel – không cần xử lý lỗi
      }
    }
  }, []);

  return {
    /** Trạng thái hiện tại: Idle, Generating, Completed, Failed */
    status,
    /** Thông báo lỗi (nếu có) */
    error,
    /** Kích hoạt pre-generation cho 1 technical session ID */
    trigger,
    /** Hủy pre-generation */
    cancel,
    /** Kiểm tra đã hoàn thành chưa */
    isCompleted: status === PreGenerationStatus.COMPLETED,
    /** Kiểm tra đang chạy */
    isGenerating: status === PreGenerationStatus.GENERATING,
  };
}
