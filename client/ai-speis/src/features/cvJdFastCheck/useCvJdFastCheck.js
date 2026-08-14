import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import cvService from '../../services/CVService';
import jdService from '../../services/JDService';
import { mapCvJdMatchResponse } from './cvJdMatchAdapter';

const READY_STATUSES = new Set(['ConfirmationRequired', 'Confirmed']);
const RETRYABLE_STATUSES = new Set(['Pending', 'Failed', 'AnalysisFailed']);
const FAILED_STATUSES = new Set(['Failed', 'AnalysisFailed', 'Archived']);
const STATUS_INT_MAP = {
  0: 'Pending',
  1: 'Processing',
  2: 'ConfirmationRequired',
  3: 'Confirmed',
  4: 'Failed',
  5: 'AnalysisFailed',
  6: 'Archived',
};
const MAX_FILE_SIZE = 5 * 1024 * 1024;
const MIN_FILE_SIZE = 1024;
const POLL_INTERVAL_MS = 2500;
const MAX_POLL_ATTEMPTS = 48;

const normalizeStatus = (status) => (
  typeof status === 'number' ? STATUS_INT_MAP[status] || String(status) : String(status || '')
);

const delay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

const validateCvFile = (file) => {
  if (!file) return 'Vui lòng chọn CV.';
  if (!file.name.toLowerCase().endsWith('.pdf')) return 'CV phải là tệp PDF (.pdf).';
  if (file.type && file.type.toLowerCase() !== 'application/pdf') return 'Nội dung tệp không đúng định dạng PDF.';
  if (file.size < MIN_FILE_SIZE) return 'CV phải có dung lượng tối thiểu 1 KB.';
  if (file.size > MAX_FILE_SIZE) return 'CV không được vượt quá 5 MB.';
  return '';
};

const toFriendlyError = (error) => {
  const message = error instanceof Error ? error.message : '';
  if (/failed to fetch|networkerror|network request failed/i.test(message)) {
    return 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra mạng và thử lại.';
  }
  if (/authentication token not found/i.test(message)) {
    return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
  }
  return message || 'Không thể hoàn tất Fast Check. Vui lòng thử lại.';
};

/**
 * @param {{
 *  currentCv: Object | null,
 *  jds: Object[],
 *  onCvUploaded?: (cv: Object) => void,
 *  onSourcesChanged?: () => void
 * }} options
 */
export const useCvJdFastCheck = ({ currentCv, jds, onCvUploaded, onSourcesChanged }) => {
  const [selectedJdId, setSelectedJdIdState] = useState('');
  const [pendingCvFile, setPendingCvFile] = useState(null);
  const [uploadedCv, setUploadedCv] = useState(null);
  const [cvFileError, setCvFileError] = useState('');
  const [phase, setPhase] = useState('idle');
  const [error, setError] = useState('');
  const [result, setResult] = useState(null);
  const runIdRef = useRef(0);
  const isSubmittingRef = useRef(false);

  useEffect(() => () => {
    runIdRef.current += 1;
  }, []);

  useEffect(() => {
    if (currentCv?.cvFileId && uploadedCv?.cvFileId === currentCv.cvFileId) {
      setUploadedCv(null);
    }
  }, [currentCv, uploadedCv]);

  const activeCv = uploadedCv || currentCv;
  const selectedJd = useMemo(
    () => jds.find((jd) => String(jd.jdFileId) === String(selectedJdId)) || null,
    [jds, selectedJdId],
  );
  const isBusy = ['uploading-cv', 'parsing-cv', 'parsing-jd', 'matching'].includes(phase);

  const ensureActive = useCallback((runId) => {
    if (runId !== runIdRef.current) throw new Error('Fast Check đã được hủy.');
  }, []);

  const pollCvUntilReady = useCallback(async (cvId, runId) => {
    for (let attempt = 0; attempt < MAX_POLL_ATTEMPTS; attempt += 1) {
      await delay(POLL_INTERVAL_MS);
      ensureActive(runId);
      const response = await cvService.getParseStatus(cvId);
      const status = normalizeStatus(response?.status ?? response?.data?.status);

      if (READY_STATUSES.has(status)) return status;
      if (FAILED_STATUSES.has(status)) {
        throw new Error(response?.errorMessage || 'AI không thể trích xuất CV này. Vui lòng kiểm tra nội dung và thử lại.');
      }
    }
    throw new Error('Phân tích CV mất nhiều thời gian hơn dự kiến. Vui lòng thử lại sau.');
  }, [ensureActive]);

  const pollJdUntilReady = useCallback(async (jdId, runId) => {
    for (let attempt = 0; attempt < MAX_POLL_ATTEMPTS; attempt += 1) {
      await delay(POLL_INTERVAL_MS);
      ensureActive(runId);
      const response = await jdService.getParseStatus(jdId);
      const statusData = response?.data || response;
      const status = normalizeStatus(statusData?.status);

      if (READY_STATUSES.has(status)) return status;
      if (FAILED_STATUSES.has(status)) {
        throw new Error(statusData?.errorMessage || 'AI không thể trích xuất JD này. Vui lòng kiểm tra nội dung và thử lại.');
      }
    }
    throw new Error('Phân tích JD mất nhiều thời gian hơn dự kiến. Vui lòng thử lại sau.');
  }, [ensureActive]);

  const prepareCv = useCallback(async (cv, runId) => {
    const statusResponse = await cvService.getParseStatus(cv.cvFileId);
    ensureActive(runId);
    const status = normalizeStatus(statusResponse?.status ?? statusResponse?.data?.status ?? cv?.status);
    if (READY_STATUSES.has(status)) return;
    if (status === 'Archived') throw new Error('CV đã chọn không còn khả dụng. Vui lòng chọn CV khác.');

    setPhase('parsing-cv');
    if (RETRYABLE_STATUSES.has(status)) await cvService.triggerParse(cv.cvFileId);
    await pollCvUntilReady(cv.cvFileId, runId);
  }, [ensureActive, pollCvUntilReady]);

  const prepareJd = useCallback(async (jd, runId) => {
    const statusResponse = await jdService.getParseStatus(jd.jdFileId);
    ensureActive(runId);
    const statusData = statusResponse?.data || statusResponse;
    const status = normalizeStatus(statusData?.status ?? jd?.status);
    if (READY_STATUSES.has(status)) return;
    if (status === 'Archived') throw new Error('JD đã chọn không còn khả dụng. Vui lòng chọn JD khác.');

    setPhase('parsing-jd');
    if (RETRYABLE_STATUSES.has(status)) await jdService.triggerParse(jd.jdFileId);
    await pollJdUntilReady(jd.jdFileId, runId);
  }, [ensureActive, pollJdUntilReady]);

  const selectCvFile = useCallback((file) => {
    const validationError = validateCvFile(file);
    setCvFileError(validationError);
    setError('');
    setResult(null);

    if (validationError) {
      setPendingCvFile(null);
      return;
    }

    setPendingCvFile(file);
    setUploadedCv(null);
  }, []);

  const clearPendingCvFile = useCallback(() => {
    setPendingCvFile(null);
    setCvFileError('');
    setError('');
    setResult(null);
  }, []);

  const setSelectedJdId = useCallback((value) => {
    setSelectedJdIdState(value);
    setError('');
    setResult(null);
  }, []);

  const submit = useCallback(async () => {
    if (isBusy || isSubmittingRef.current) return;

    if (!pendingCvFile && !activeCv?.cvFileId) {
      setError('Vui lòng chọn hoặc tải lên CV trước khi Fast Check.');
      return;
    }
    if (!selectedJd) {
      setError('Vui lòng chọn Job Description để đối chiếu.');
      return;
    }

    const runId = runIdRef.current + 1;
    runIdRef.current = runId;
    isSubmittingRef.current = true;
    setError('');
    setResult(null);
    setPhase('parsing-cv');

    try {
      let cv = activeCv;
      if (pendingCvFile) {
        setPhase('uploading-cv');
        cv = await cvService.uploadCV(pendingCvFile);
        ensureActive(runId);
        if (!cv?.cvFileId) throw new Error('Máy chủ không trả về thông tin CV vừa tải lên.');
        setUploadedCv(cv);
        setPendingCvFile(null);
        onCvUploaded?.(cv);
      }

      await prepareCv(cv, runId);
      ensureActive(runId);
      await prepareJd(selectedJd, runId);
      ensureActive(runId);

      setPhase('matching');
      const response = await jdService.matchCvToJd(selectedJd.jdFileId, cv.cvFileId);
      ensureActive(runId);
      setResult(mapCvJdMatchResponse(response));
      setPhase('success');
      onSourcesChanged?.();
    } catch (submitError) {
      if (runId === runIdRef.current) {
        setError(toFriendlyError(submitError));
        setPhase('error');
      }
    } finally {
      if (runId === runIdRef.current) isSubmittingRef.current = false;
    }
  }, [
    activeCv,
    ensureActive,
    isBusy,
    onCvUploaded,
    onSourcesChanged,
    pendingCvFile,
    prepareCv,
    prepareJd,
    selectedJd,
  ]);

  return {
    activeCv,
    clearPendingCvFile,
    cvFileError,
    error,
    isBusy,
    pendingCvFile,
    phase,
    result,
    selectCvFile,
    selectedJd,
    selectedJdId,
    setSelectedJdId,
    submit,
  };
};
