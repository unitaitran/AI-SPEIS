import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Upload,
  FileText,
  CheckCircle2,
  AlertCircle,
  Loader2,
  Eye,
  Plus,
  Award,
  Briefcase,
  FolderGit2,
  Trash2,
  Info,
  Sparkles,
  GraduationCap,
  Target,
  Check,
  X,
  Edit3,
  Clock,
  ArrowLeft,
  ChevronRight
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import cvService from '../../services/CVService';
import { API_BASE_URL } from '../../config/api';
import { beginNewInterviewCampaign } from '../../utils/interviewContext';
import '../../styles/user/MyCVPage.css';

/* ========================================================================= */
/*  STATUS CONSTANTS matching backend CVFileStatus enum                      */
/* ========================================================================= */
const STATUS = {
  PENDING: 'Pending',
  PROCESSING: 'Processing',
  CONFIRMATION_REQUIRED: 'ConfirmationRequired',
  CONFIRMED: 'Confirmed',
  FAILED: 'Failed',
  ANALYSIS_FAILED: 'AnalysisFailed',
  ARCHIVED: 'Archived',
};

/** Map numeric enum values (from CVDto) to string names.
 *  CvParseStatusResponse already returns strings via .ToString(). */
const STATUS_INT_MAP = {
  0: STATUS.PENDING,
  1: STATUS.PROCESSING,
  2: STATUS.CONFIRMATION_REQUIRED,
  3: STATUS.CONFIRMED,
  4: STATUS.FAILED,
  5: STATUS.ANALYSIS_FAILED,
  6: STATUS.ARCHIVED,
};

const normalizeStatus = (raw) => {
  if (typeof raw === 'number') return STATUS_INT_MAP[raw] || String(raw);
  return String(raw);
};

/* ========================================================================= */
/*  POLLING INTERVAL                                                         */
/* ========================================================================= */
const POLL_INTERVAL_MS = 2500;

const renderFeedbackList = (text, type, t) => {
  if (!text) return <p className="mycv-empty-note">{t('mycv.no_data', 'Chưa có thông tin.')}</p>;

  const items = text
    .split('\n')
    .map(line => line.trim())
    .filter(line => line.length > 0);

  if (items.length === 0) return <p className="mycv-empty-note">{t('mycv.no_data', 'Chưa có thông tin.')}</p>;

  return items.map((item, idx) => {
    const cleanItem = item.replace(/^[-*•\d\.\s]+/, '');
    const colonIdx = cleanItem.indexOf(':');
    const dashIdx = cleanItem.indexOf(' - ');

    let title = cleanItem;
    let desc = '';

    if (colonIdx !== -1 && (dashIdx === -1 || colonIdx < dashIdx)) {
      title = cleanItem.substring(0, colonIdx).trim();
      desc = cleanItem.substring(colonIdx + 1).trim();
    } else if (dashIdx !== -1) {
      title = cleanItem.substring(0, dashIdx).trim();
      desc = cleanItem.substring(dashIdx + 3).trim();
    }

    const Icon = type === 'success' ? CheckCircle2 : AlertCircle;
    const itemClass = type === 'success' ? 'mycv-feedback-item mycv-feedback-item--success' : 'mycv-feedback-item mycv-feedback-item--warning';

    return (
      <div key={idx} className={itemClass}>
        <Icon size={16} />
        <div>
          <h4>{title}</h4>
          {desc && <p>{desc}</p>}
        </div>
      </div>
    );
  });
};

function MyCVPage() {
  const { t } = useTranslation('dashboard');

  /* -----------------------------------------------------------------------
   *  Core state
   * --------------------------------------------------------------------- */
  const [cvData, setCvData] = useState(null);        // CVDto from GET /MyCV
  const [parsedData, setParsedData] = useState(null); // CvParsedDataResponse
  const [cvStatus, setCvStatus] = useState(null);     // string status

  /* UI state */
  const [isLoading, setIsLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [isParsing, setIsParsing] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadStep, setUploadStep] = useState('');
  const [error, setError] = useState(null);
  const [showFeedbackModal, setShowFeedbackModal] = useState(false);
  const [showPdfModal, setShowPdfModal] = useState(false);
  const [activeTab, setActiveTab] = useState('overall');
  const [isDragging, setIsDragging] = useState(false);

  /* Editing state (for confirm flow) */
  const [isEditing, setIsEditing] = useState(false);
  const [editData, setEditData] = useState(null);

  /* Refs for polling */
  const pollTimerRef = useRef(null);
  const isMountedRef = useRef(true);

  /* -----------------------------------------------------------------------
   *  Helpers
   * --------------------------------------------------------------------- */
  const formatDate = (dateStr) => {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    return date.toLocaleDateString('vi-VN', { year: 'numeric', month: '2-digit', day: '2-digit' });
  };

  const getStatusBadge = (status) => {
    const map = {
      [STATUS.PENDING]: { cls: 'mycv-badge--warning', icon: Clock, label: t('mycv.status_pending', 'Chờ phân tích') },
      [STATUS.PROCESSING]: { cls: 'mycv-badge--info', icon: Loader2, label: t('mycv.status_processing', 'Đang phân tích') },
      [STATUS.CONFIRMATION_REQUIRED]: { cls: 'mycv-badge--info', icon: AlertCircle, label: t('mycv.status_confirm_req', 'Cần xác nhận') },
      [STATUS.CONFIRMED]: { cls: 'mycv-badge--success', icon: CheckCircle2, label: t('mycv.status_confirmed', 'Đã xác nhận') },
      [STATUS.FAILED]: { cls: 'mycv-badge--error', icon: AlertCircle, label: t('mycv.status_failed', 'Tải lên thất bại') },
      [STATUS.ANALYSIS_FAILED]: { cls: 'mycv-badge--error', icon: AlertCircle, label: t('mycv.status_analysis_fail', 'Phân tích thất bại') },
    };
    return map[status] || { cls: 'mycv-badge--default', icon: Info, label: status };
  };

  /* -----------------------------------------------------------------------
   *  Fetch CV + parsed data
   * --------------------------------------------------------------------- */
  const fetchCV = useCallback(async () => {
    try {
      const cv = await cvService.getMyCV();
      if (!isMountedRef.current) return;
      setCvData(cv);
      setCvStatus(normalizeStatus(cv.status));

      // If status is ConfirmationRequired or Confirmed, try fetching parsed data
      const normalized = normalizeStatus(cv.status);
      if (
        normalized === STATUS.CONFIRMATION_REQUIRED ||
        normalized === STATUS.CONFIRMED
      ) {
        try {
          const parsed = await cvService.getParsedData(cv.cvFileId);
          if (!isMountedRef.current) return;
          setParsedData(parsed);
        } catch {
          // parsed data not available yet — that's okay
        }
      }

      return cv;
    } catch (err) {
      if (!isMountedRef.current) return null;
      if (err.message?.includes('404') || err.message?.includes('Không tìm thấy')) {
        setCvData(null);
        setCvStatus(null);
        setParsedData(null);
      }
      return null;
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  /* -----------------------------------------------------------------------
   *  Polling logic for Processing status
   * --------------------------------------------------------------------- */
  const stopPolling = useCallback(() => {
    if (pollTimerRef.current) {
      clearInterval(pollTimerRef.current);
      pollTimerRef.current = null;
    }
  }, []);

  const startPolling = useCallback((cvFileId) => {
    stopPolling();
    setIsParsing(true);

    pollTimerRef.current = setInterval(async () => {
      try {
        const statusResp = await cvService.getParseStatus(cvFileId);
        if (!isMountedRef.current) { stopPolling(); return; }

        setCvStatus(normalizeStatus(statusResp.status));

        if (normalizeStatus(statusResp.status) === STATUS.CONFIRMATION_REQUIRED) {
          stopPolling();
          setIsParsing(false);
          // Fetch the parsed data
          const parsed = await cvService.getParsedData(cvFileId);
          if (isMountedRef.current) {
            setParsedData(parsed);
            // Also refresh CV data
            const cv = await cvService.getMyCV();
            if (isMountedRef.current) {
              setCvData(cv);
              setCvStatus(normalizeStatus(cv.status));
            }
          }
        } else if (
          normalizeStatus(statusResp.status) === STATUS.ANALYSIS_FAILED ||
          normalizeStatus(statusResp.status) === STATUS.FAILED
        ) {
          stopPolling();
          setIsParsing(false);
          const errorMsg = "Đây không phải là JD/CV hoặc bạn đang upload chưa phải thông tin CV hoàn thiện. Hãy thử lại.";
          setError(errorMsg);
          // Refresh CV data
          await fetchCV();
        } else if (normalizeStatus(statusResp.status) === STATUS.CONFIRMED) {
          stopPolling();
          setIsParsing(false);
          const parsed = await cvService.getParsedData(cvFileId);
          if (isMountedRef.current) {
            setParsedData(parsed);
            const cv = await cvService.getMyCV();
            if (isMountedRef.current) {
              setCvData(cv);
              setCvStatus(normalizeStatus(cv.status));
            }
          }
        }
      } catch {
        // network error — keep polling
      }
    }, POLL_INTERVAL_MS);
  }, [stopPolling, fetchCV, t]);

  /* -----------------------------------------------------------------------
   *  Init
   * --------------------------------------------------------------------- */
  useEffect(() => {
    isMountedRef.current = true;

    const init = async () => {
      setIsLoading(true);
      const cv = await fetchCV();
      if (isMountedRef.current) setIsLoading(false);

      // If it's currently processing, start polling
      if (cv && normalizeStatus(cv.status) === STATUS.PROCESSING) {
        startPolling(cv.cvFileId);
      }
    };

    init();

    return () => {
      isMountedRef.current = false;
      stopPolling();
    };
  }, [fetchCV, startPolling, stopPolling]);

  /* -----------------------------------------------------------------------
   *  Upload handler
   * --------------------------------------------------------------------- */
  const processFile = async (file) => {
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.pdf')) {
      setError(t('mycv.error_pdf_only', 'Chỉ hỗ trợ tệp tin định dạng PDF'));
      return;
    }

    setIsUploading(true);
    setUploadProgress(0);
    setError(null);
    setUploadStep(t('mycv.step_preparing', 'Đang chuẩn bị tệp tin...'));

    // Simulate progress while uploading
    const progressInterval = setInterval(() => {
      setUploadProgress((prev) => {
        if (prev >= 90) return prev;
        const next = prev + Math.random() * 12 + 3;
        if (next >= 30) setUploadStep(t('mycv.step_uploading', 'Đang tải tệp tin lên máy chủ...'));
        if (next >= 60) setUploadStep(t('mycv.step_analyzing', 'Đang phân tích cấu trúc CV...'));
        return Math.min(next, 90);
      });
    }, 200);

    try {
      const result = await cvService.uploadCV(file);
      clearInterval(progressInterval);
      setUploadProgress(100);
      setUploadStep(t('mycv.step_completed', 'Hoàn tất tải lên!'));

      // Brief pause to show 100%
      await new Promise((r) => setTimeout(r, 600));

      if (!isMountedRef.current) return;
      setCvData(result);
      setCvStatus(normalizeStatus(result.status));
      setParsedData(null);
      setIsUploading(false);
    } catch (err) {
      clearInterval(progressInterval);
      if (!isMountedRef.current) return;
      setIsUploading(false);
      setError(err.message || t('mycv.error_upload', 'Không thể tải lên file CV. Vui lòng thử lại.'));
    }
  };

  const handleFileUpload = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    e.target.value = '';
    await processFile(file);
  };

  const handleDragOver = (e) => {
    e.preventDefault();
    e.stopPropagation();
  };

  const handleDragEnter = (e) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  };

  const handleDragLeave = (e) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleDrop = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    const file = e.dataTransfer.files[0];
    if (file) {
      await processFile(file);
    }
  };

  /* -----------------------------------------------------------------------
   *  Trigger Parse handler
   * --------------------------------------------------------------------- */
  const handleTriggerParse = async () => {
    if (!cvData) return;
    setError(null);
    setIsParsing(true);

    try {
      await cvService.triggerParse(cvData.cvFileId);
      setCvStatus(STATUS.PROCESSING);
      startPolling(cvData.cvFileId);
    } catch (err) {
      setIsParsing(false);
      setError(err.message);
    }
  };

  /* -----------------------------------------------------------------------
   *  Delete handler
   * --------------------------------------------------------------------- */
  const handleRemoveCV = async () => {
    if (!cvData?.cvFileId) {
      setError(t('mycv.error_find_cv', 'Không tìm thấy thông tin tệp CV cần xóa.'));
      return;
    }
    if (!window.confirm(t('mycv.confirm_delete', 'Bạn có chắc chắn muốn xóa CV này?'))) return;

    try {
      await cvService.deleteCV(cvData.cvFileId);
      setCvData(null);
      setCvStatus(null);
      setParsedData(null);
      stopPolling();
    } catch (err) {
      setError(err.message || t('mycv.error_remove', 'Lỗi khi xóa CV trên máy chủ.'));
    }
  };

  /* -----------------------------------------------------------------------
   *  Confirm handler
   * --------------------------------------------------------------------- */
  const handleConfirm = async () => {
    if (!cvData?.cvFileId) return;
    setIsConfirming(true);
    setError(null);

    const dataToConfirm = isEditing && editData ? editData : {
      roleTarget: parsedData?.roleTarget || '',
      education: parsedData?.education || [],
      experience: parsedData?.experience || [],
      projects: (parsedData?.projects || []).map((p) => ({
        projectName: p.projectName,
        roleDescription: p.roleDescription,
        technologyStack: p.technologyStack,
        projectSummary: p.projectSummary,
        duration: p.duration,
      })),
      skills: (parsedData?.skills || []).map((s) => ({
        skillName: s.skillName,
        source: s.source,
        category: s.category,
      })),
    };

    try {
      await cvService.confirmParsedData(cvData.cvFileId, dataToConfirm);
      // Refresh
      const cv = await cvService.getMyCV();
      if (!isMountedRef.current) return;
      setCvData(cv);
      setCvStatus(normalizeStatus(cv.status));
      const parsed = await cvService.getParsedData(cv.cvFileId);
      if (!isMountedRef.current) return;
      setParsedData(parsed);
      setIsEditing(false);
      setEditData(null);
    } catch (err) {
      setError(err.message);
    } finally {
      if (isMountedRef.current) setIsConfirming(false);
    }
  };

  /* -----------------------------------------------------------------------
   *  Editing helpers
   * --------------------------------------------------------------------- */
  const startEditing = () => {
    if (!parsedData) return;
    setEditData({
      roleTarget: parsedData.roleTarget || '',
      education: [...(parsedData.education || [])],
      experience: [...(parsedData.experience || [])],
      projects: (parsedData.projects || []).map((p) => ({
        projectName: p.projectName || '',
        roleDescription: p.roleDescription || '',
        technologyStack: p.technologyStack || '',
        projectSummary: p.projectSummary || '',
        duration: p.duration || '',
      })),
      skills: (parsedData.skills || []).map((s) => ({
        skillName: s.skillName || '',
        source: s.source || '',
        category: s.category || 'Other',
      })),
    });
    setIsEditing(true);
  };

  const cancelEditing = () => {
    setIsEditing(false);
    setEditData(null);
  };

  /* -----------------------------------------------------------------------
   *  Determine which data to render
   * --------------------------------------------------------------------- */
  const displayData = isEditing ? editData : parsedData;
  const hasExtractedData = parsedData && (
    parsedData.skills?.length > 0 ||
    parsedData.projects?.length > 0 ||
    parsedData.education?.length > 0 ||
    parsedData.experience?.length > 0
  );

  const needsConfirmation = cvStatus === STATUS.CONFIRMATION_REQUIRED;
  const isConfirmed = cvStatus === STATUS.CONFIRMED;
  const canTriggerParse = cvStatus === STATUS.PENDING || cvStatus === STATUS.ANALYSIS_FAILED;

  /* -----------------------------------------------------------------------
   *  RENDER: Loading
   * --------------------------------------------------------------------- */
  if (isLoading) {
    return (
      <UserLayout>
        <div className="mycv-loading">
          <Loader2 size={36} className="mycv-spinner" />
          <p>{t('mycv.loading', 'Đang tải thông tin CV của bạn...')}</p>
        </div>
      </UserLayout>
    );
  }

  /* -----------------------------------------------------------------------
   *  RENDER
   * --------------------------------------------------------------------- */
  return (
    <UserLayout>
      <div className="mycv-container animate-pageEntrance">
        {/* Page Header */}
        <section className="mycv-header relative flex flex-col md:flex-row">
          <button 
            className="md:absolute md:right-[100%] md:mr-4 w-10 h-10 flex-shrink-0 flex items-center justify-center bg-surface-1 text-text-secondary hover:text-primary rounded-xl border border-border shadow-sm transition-colors mt-0 mb-4 md:mb-0 z-10"
            onClick={() => navigate(USER_ROUTES.CV)}
            title="Quay lại"
          >
            <ArrowLeft size={20} />
          </button>
          <div>
            <h1 className="mycv-title">{t('mycv.title', 'CV của tôi')}</h1>
            <p className="mycv-subtitle">
              {t('mycv.subtitle', 'Quản lý CV để AI phân tích kỹ năng, dự án và tạo câu hỏi phỏng vấn cá nhân hóa.')}
            </p>
          </div>
        </section>

        {/* Error Banner */}
        {error && (
          <div className="mycv-error-banner">
            <AlertCircle size={16} />
            <span>{error}</span>
            <button onClick={() => setError(null)} className="mycv-error-close">
              <X size={14} />
            </button>
          </div>
        )}

        {/* ==================== UPLOADING STATE ==================== */}
        {isUploading && (
          <div className="mycv-upload-progress-card">
            <div className="mycv-upload-progress-icon">
              <Loader2 size={48} className="mycv-spinner" />
              <Sparkles size={20} className="mycv-pulse-icon" />
            </div>
            <h3>{t('mycv.analyzing_title', 'Đang tải CV lên máy chủ')}</h3>
            <p className="mycv-upload-step">{uploadStep}</p>
            <div className="mycv-progress-bar-track">
              <div
                className="mycv-progress-bar-fill"
                style={{ width: `${uploadProgress}%` }}
              />
            </div>
            <span className="mycv-progress-percent">{Math.round(uploadProgress)}%</span>
          </div>
        )}

        {/* ==================== PARSING STATE ==================== */}
        {!isUploading && isParsing && (
          <div className="mycv-upload-progress-card">
            <div className="mycv-upload-progress-icon">
              <Loader2 size={48} className="mycv-spinner" />
              <Sparkles size={20} className="mycv-pulse-icon" />
            </div>
            <h3>{t('mycv.ai_parsing_title', 'AI đang phân tích CV của bạn')}</h3>
            <p className="mycv-upload-step">
              {t('mycv.ai_parsing_desc', 'Đang trích xuất kỹ năng, dự án, học vấn và kinh nghiệm...')}
            </p>
            <div className="mycv-progress-bar-track">
              <div className="mycv-progress-bar-fill mycv-progress-bar-indeterminate" />
            </div>
            <p className="mycv-polling-hint">
              {t('mycv.polling_hint', 'Quá trình này có thể mất từ 30 giây đến 2 phút.')}
            </p>
          </div>
        )}

        {/* ==================== NO CV UPLOADED ==================== */}
        {!isUploading && !isParsing && !cvData && (
          <div className="mycv-empty-layout">
            {/* Left: Mascot */}
            <div className="mycv-mascot-col">
              <div className="mycv-speech-bubble">
                <p>{t('mycv.mascot_say', 'Hãy tải lên CV của bạn!')}</p>
                <div className="mycv-speech-arrow" />
              </div>
              <div className="mycv-mascot-circle">
                <img src="/teaching_mascot.jpg" alt="Teaching Mascot" />
              </div>
            </div>

            {/* Right: Upload Zone */}
            <div className="mycv-upload-zone-col">
              <div
                className={`mycv-upload-dropzone ${isDragging ? 'mycv-upload-dropzone--dragging' : ''}`}
                onDragOver={handleDragOver}
                onDragEnter={handleDragEnter}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
              >
                <div className="mycv-upload-icon-wrap">
                  <Upload size={28} />
                </div>
                <h3>{t('mycv.upload_title', 'Tải lên CV của bạn')}</h3>
                <p>{t('mycv.upload_desc', 'Bạn chưa tải lên CV của mình. Kéo thả tệp tin vào đây hoặc chọn tệp tin để AI có thể phân tích kỹ năng, trích xuất thông tin dự án và cá nhân hóa câu hỏi phỏng vấn tối ưu nhất cho bạn.')}</p>
                <label className="mycv-upload-btn">
                  <Plus size={18} />
                  {t('mycv.select_file', 'Chọn tệp tin CV (PDF)')}
                  <input type="file" accept=".pdf" onChange={handleFileUpload} hidden />
                </label>
                <span className="mycv-file-hint">
                  {t('mycv.file_hint', 'Hỗ trợ định dạng PDF tối đa 5MB')}
                </span>
              </div>
            </div>
          </div>
        )}

        {/* ==================== CV UPLOADED — SHOW INFO ==================== */}
        {!isUploading && !isParsing && cvData && (
          <div className="mycv-active">
            {/* ---------- CV Info Card ---------- */}
            <div className="mycv-info-card">
              <div className="mycv-info-card-accent" />
              <div className="mycv-info-left">
                <div className="mycv-info-icon-box">
                  <FileText size={24} />
                </div>
                <div className="mycv-info-details">
                  <div className="mycv-info-name-row">
                    <h3
                      className="cursor-pointer hover:text-primary hover:underline transition-colors"
                      onClick={() => setShowPdfModal(true)}
                      title={t('mycv.click_to_view', 'Nhấp để xem PDF')}
                    >
                      {cvData.fileName}
                    </h3>
                    {(() => {
                      const badge = getStatusBadge(cvStatus);
                      const BadgeIcon = badge.icon;
                      return (
                        <span className={`mycv-badge ${badge.cls}`}>
                          <BadgeIcon size={10} className={cvStatus === STATUS.PROCESSING ? 'mycv-spinner' : ''} />
                          {badge.label}
                        </span>
                      );
                    })()}
                  </div>
                  <p className="mycv-info-date">
                    {t('mycv.uploaded_on', 'Ngày tải lên: {{date}}', { date: formatDate(cvData.uploadedAt) })}
                  </p>
                  {hasExtractedData && (
                    <div className="mycv-info-stats">
                      <span><Award size={14} /> {t('mycv.skills_detected', '{{count}} kỹ năng phát hiện', { count: parsedData?.skills?.length || 0 })}</span>
                      <span><FolderGit2 size={14} /> {t('mycv.projects_detected', '{{count}} project phát hiện', { count: parsedData?.projects?.length || 0 })}</span>
                    </div>
                  )}
                </div>
              </div>

              <div className="mycv-info-actions">
                {/* Trigger Parse button — only when Pending or AnalysisFailed */}
                {canTriggerParse && (
                  <button onClick={handleTriggerParse} className="mycv-btn mycv-btn--primary">
                    <Sparkles size={14} />
                    {t('mycv.trigger_parse', 'Phân tích CV bằng AI')}
                  </button>
                )}

                {/* View feedback button */}
                {hasExtractedData && (
                  <button onClick={() => setShowFeedbackModal(true)} className="mycv-btn mycv-btn--dark">
                    <Eye size={14} />
                    {t('mycv.view_feedback', 'Xem feedback CV')}
                  </button>
                )}

                {/* Upload new */}
                <label className="mycv-btn mycv-btn--outline">
                  <Upload size={14} />
                  {t('mycv.upload_new', 'Tải CV mới')}
                  <input type="file" accept=".pdf" onChange={handleFileUpload} hidden />
                </label>

                {/* Delete */}
                <button onClick={handleRemoveCV} className="mycv-btn-icon mycv-btn-icon--danger" title={t('mycv.delete_cv', 'Xóa CV')}>
                  <Trash2 size={16} />
                </button>
              </div>
            </div>

            {/* ---------- Confirmation required banner ---------- */}
            {needsConfirmation && hasExtractedData && (
              <div className="mycv-confirm-banner">
                <div className="mycv-confirm-banner-content">
                  <div className="mycv-confirm-banner-icon">
                    <AlertCircle size={20} />
                  </div>
                  <div>
                    <h4>{t('mycv.confirm_required_title', 'AI đã hoàn tất phân tích — Vui lòng xác nhận')}</h4>
                    <p>{t('mycv.confirm_required_desc', 'Kiểm tra thông tin AI trích xuất bên dưới. Bạn có thể chỉnh sửa trước khi xác nhận.')}</p>
                  </div>
                </div>
                <div className="mycv-confirm-banner-actions">
                  {!isEditing ? (
                    <>
                      <button onClick={startEditing} className="mycv-btn mycv-btn--outline mycv-btn--sm">
                        <Edit3 size={14} /> {t('mycv.edit', 'Chỉnh sửa')}
                      </button>
                      <button onClick={handleConfirm} disabled={isConfirming} className="mycv-btn mycv-btn--primary mycv-btn--sm">
                        {isConfirming ? <Loader2 size={14} className="mycv-spinner" /> : <Check size={14} />}
                        {t('mycv.confirm_btn', 'Xác nhận')}
                      </button>
                    </>
                  ) : (
                    <>
                      <button onClick={cancelEditing} className="mycv-btn mycv-btn--outline mycv-btn--sm">
                        <X size={14} /> {t('mycv.cancel', 'Hủy')}
                      </button>
                      <button onClick={handleConfirm} disabled={isConfirming} className="mycv-btn mycv-btn--primary mycv-btn--sm">
                        {isConfirming ? <Loader2 size={14} className="mycv-spinner" /> : <Check size={14} />}
                        {t('mycv.save_confirm', 'Lưu & Xác nhận')}
                      </button>
                    </>
                  )}
                </div>
              </div>
            )}



            {/* ---------- Extracted Details ---------- */}
            {hasExtractedData && (
              <>
                <div className="mycv-section-heading">
                  <Sparkles size={20} />
                  <h2>{t('mycv.extracted_info', 'Thông tin AI trích xuất')}</h2>
                  {isConfirmed && (
                    <span className="mycv-badge mycv-badge--success mycv-badge--lg">
                      <CheckCircle2 size={12} /> {t('mycv.data_confirmed', 'Đã xác nhận')}
                    </span>
                  )}
                </div>

                {/* ---------- Role Target ---------- */}
                {(displayData?.roleTarget || isEditing) && (
                  <div className="mycv-role-target-card">
                    <Target size={18} />
                    {isEditing ? (
                      <input
                        type="text"
                        value={editData?.roleTarget || ''}
                        onChange={(e) => setEditData({ ...editData, roleTarget: e.target.value })}
                        className="mycv-edit-input"
                        placeholder={t('mycv.role_target_placeholder', 'VD: Frontend Developer, Backend Engineer...')}
                      />
                    ) : (
                      <div className="mycv-role-target-text">
                        <span className="mycv-role-target-label">{t('mycv.role_target', 'Vị trí mục tiêu')}</span>
                        <strong>{displayData?.roleTarget}</strong>
                      </div>
                    )}
                  </div>
                )}

                {/* ---------- 4-Column Grid: Skills, Education, Projects, Experience ---------- */}
                <div className="mycv-grid">
                  {/* Skills */}
                  <div className="mycv-card">
                    <div className="mycv-card-header">
                      <Award size={18} />
                      <h3>{t('mycv.skills', 'Kỹ năng')}</h3>
                    </div>
                    <div className="mycv-card-body">
                      {isEditing ? (
                        <div className="mycv-edit-skills">
                          {editData?.skills?.map((skill, idx) => (
                            <div key={idx} className="mycv-edit-skill-row">
                              <input
                                type="text"
                                value={skill.skillName}
                                onChange={(e) => {
                                  const updated = [...editData.skills];
                                  updated[idx] = { ...updated[idx], skillName: e.target.value };
                                  setEditData({ ...editData, skills: updated });
                                }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <button
                                onClick={() => {
                                  const updated = editData.skills.filter((_, i) => i !== idx);
                                  setEditData({ ...editData, skills: updated });
                                }}
                                className="mycv-btn-icon mycv-btn-icon--danger mycv-btn-icon--xs"
                              >
                                <X size={12} />
                              </button>
                            </div>
                          ))}
                          <button
                            onClick={() => setEditData({
                              ...editData,
                              skills: [...(editData.skills || []), { skillName: '', source: 'USER', category: 'Other' }],
                            })}
                            className="mycv-add-btn"
                          >
                            <Plus size={14} /> {t('mycv.add_skill', 'Thêm kỹ năng')}
                          </button>
                        </div>
                      ) : (
                        <div className="mycv-skill-tags">
                          {(displayData?.skills || []).map((skill, idx) => (
                            <span key={idx} className="mycv-skill-tag">{skill.skillName}</span>
                          ))}
                          {(!displayData?.skills || displayData.skills.length === 0) && (
                            <p className="mycv-empty-note">{t('mycv.no_skills', 'Chưa có kỹ năng')}</p>
                          )}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Education */}
                  <div className="mycv-card">
                    <div className="mycv-card-header">
                      <GraduationCap size={18} />
                      <h3>{t('mycv.education', 'Học vấn')}</h3>
                    </div>
                    <div className="mycv-card-body">
                      {isEditing ? (
                        <div className="mycv-edit-list">
                          {editData?.education?.map((edu, idx) => (
                            <div key={idx} className="mycv-edit-block">
                              <input type="text" value={edu.school} placeholder={t('mycv.school', 'Trường')}
                                onChange={(e) => { const u = [...editData.education]; u[idx] = { ...u[idx], school: e.target.value }; setEditData({ ...editData, education: u }); }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <input type="text" value={edu.major} placeholder={t('mycv.major', 'Chuyên ngành')}
                                onChange={(e) => { const u = [...editData.education]; u[idx] = { ...u[idx], major: e.target.value }; setEditData({ ...editData, education: u }); }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <div className="mycv-edit-row-pair">
                                <input type="text" value={edu.gpa || ''} placeholder="GPA"
                                  onChange={(e) => { const u = [...editData.education]; u[idx] = { ...u[idx], gpa: e.target.value }; setEditData({ ...editData, education: u }); }}
                                  className="mycv-edit-input mycv-edit-input--sm"
                                />
                                <input type="text" value={edu.graduationYear || ''} placeholder={t('mycv.grad_year', 'Năm tốt nghiệp')}
                                  onChange={(e) => { const u = [...editData.education]; u[idx] = { ...u[idx], graduationYear: e.target.value }; setEditData({ ...editData, education: u }); }}
                                  className="mycv-edit-input mycv-edit-input--sm"
                                />
                              </div>
                              <button onClick={() => { const u = editData.education.filter((_, i) => i !== idx); setEditData({ ...editData, education: u }); }}
                                className="mycv-remove-block-btn"><X size={12} /> {t('mycv.remove', 'Xóa')}</button>
                            </div>
                          ))}
                          <button onClick={() => setEditData({ ...editData, education: [...(editData.education || []), { school: '', major: '', gpa: '', graduationYear: '' }] })}
                            className="mycv-add-btn"><Plus size={14} /> {t('mycv.add_education', 'Thêm học vấn')}</button>
                        </div>
                      ) : (
                        <div className="mycv-education-list">
                          {(displayData?.education || []).map((edu, idx) => (
                            <div key={idx} className="mycv-education-item">
                              <h4>{edu.school}</h4>
                              <p className="mycv-edu-major">{edu.major}</p>
                              <div className="mycv-edu-meta">
                                {edu.gpa && <span>GPA: {edu.gpa}</span>}
                                {edu.graduationYear && <span>{edu.graduationYear}</span>}
                              </div>
                            </div>
                          ))}
                          {(!displayData?.education || displayData.education.length === 0) && (
                            <p className="mycv-empty-note">{t('mycv.no_education', 'Chưa có thông tin học vấn')}</p>
                          )}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Projects */}
                  <div className="mycv-card">
                    <div className="mycv-card-header">
                      <FolderGit2 size={18} />
                      <h3>{t('mycv.projects', 'Dự án')}</h3>
                    </div>
                    <div className="mycv-card-body">
                      {isEditing ? (
                        <div className="mycv-edit-list">
                          {editData?.projects?.map((proj, idx) => (
                            <div key={idx} className="mycv-edit-block">
                              <input type="text" value={proj.projectName} placeholder={t('mycv.project_name', 'Tên dự án')}
                                onChange={(e) => { const u = [...editData.projects]; u[idx] = { ...u[idx], projectName: e.target.value }; setEditData({ ...editData, projects: u }); }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <input type="text" value={proj.roleDescription || ''} placeholder={t('mycv.role_desc', 'Vai trò')}
                                onChange={(e) => { const u = [...editData.projects]; u[idx] = { ...u[idx], roleDescription: e.target.value }; setEditData({ ...editData, projects: u }); }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <textarea value={proj.projectSummary || ''} placeholder={t('mycv.project_summary', 'Mô tả dự án')}
                                onChange={(e) => { const u = [...editData.projects]; u[idx] = { ...u[idx], projectSummary: e.target.value }; setEditData({ ...editData, projects: u }); }}
                                className="mycv-edit-textarea" rows={2}
                              />
                              <button onClick={() => { const u = editData.projects.filter((_, i) => i !== idx); setEditData({ ...editData, projects: u }); }}
                                className="mycv-remove-block-btn"><X size={12} /> {t('mycv.remove', 'Xóa')}</button>
                            </div>
                          ))}
                          <button onClick={() => setEditData({ ...editData, projects: [...(editData.projects || []), { projectName: '', roleDescription: '', technologyStack: '', projectSummary: '', duration: '' }] })}
                            className="mycv-add-btn"><Plus size={14} /> {t('mycv.add_project', 'Thêm dự án')}</button>
                        </div>
                      ) : (
                        <div className="mycv-project-list">
                          {(displayData?.projects || []).map((proj, idx) => (
                            <div key={idx} className="mycv-project-item">
                              {proj.roleDescription && (
                                <span className="mycv-project-role">{proj.roleDescription}</span>
                              )}
                              <h4>{proj.projectName}</h4>
                              {proj.projectSummary && <p>{proj.projectSummary}</p>}
                              {proj.technologyStack && (
                                <div className="mycv-project-tech">{proj.technologyStack}</div>
                              )}
                            </div>
                          ))}
                          {(!displayData?.projects || displayData.projects.length === 0) && (
                            <p className="mycv-empty-note">{t('mycv.no_projects', 'Chưa có dự án')}</p>
                          )}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Experience */}
                  <div className="mycv-card">
                    <div className="mycv-card-header">
                      <Briefcase size={18} />
                      <h3>{t('mycv.experience', 'Kinh nghiệm')}</h3>
                    </div>
                    <div className="mycv-card-body">
                      {isEditing ? (
                        <div className="mycv-edit-list">
                          {editData?.experience?.map((exp, idx) => (
                            <div key={idx} className="mycv-edit-block">
                              <input type="text" value={exp.position} placeholder={t('mycv.position', 'Vị trí')}
                                onChange={(e) => { const u = [...editData.experience]; u[idx] = { ...u[idx], position: e.target.value }; setEditData({ ...editData, experience: u }); }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <input type="text" value={exp.company} placeholder={t('mycv.company', 'Công ty')}
                                onChange={(e) => { const u = [...editData.experience]; u[idx] = { ...u[idx], company: e.target.value }; setEditData({ ...editData, experience: u }); }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <input type="text" value={exp.duration || ''} placeholder={t('mycv.duration', 'Thời gian')}
                                onChange={(e) => { const u = [...editData.experience]; u[idx] = { ...u[idx], duration: e.target.value }; setEditData({ ...editData, experience: u }); }}
                                className="mycv-edit-input mycv-edit-input--sm"
                              />
                              <textarea value={exp.description || ''} placeholder={t('mycv.exp_description', 'Mô tả công việc')}
                                onChange={(e) => { const u = [...editData.experience]; u[idx] = { ...u[idx], description: e.target.value }; setEditData({ ...editData, experience: u }); }}
                                className="mycv-edit-textarea" rows={2}
                              />
                              <button onClick={() => { const u = editData.experience.filter((_, i) => i !== idx); setEditData({ ...editData, experience: u }); }}
                                className="mycv-remove-block-btn"><X size={12} /> {t('mycv.remove', 'Xóa')}</button>
                            </div>
                          ))}
                          <button onClick={() => setEditData({ ...editData, experience: [...(editData.experience || []), { company: '', position: '', duration: '', description: '' }] })}
                            className="mycv-add-btn"><Plus size={14} /> {t('mycv.add_experience', 'Thêm kinh nghiệm')}</button>
                        </div>
                      ) : (
                        <div className="mycv-experience-list">
                          {(displayData?.experience || []).map((exp, idx) => (
                            <div key={idx} className="mycv-experience-item">
                              <div className="mycv-exp-header">
                                <div>
                                  <h4>{exp.position}</h4>
                                  <span className="mycv-exp-company">{exp.company}</span>
                                </div>
                                {exp.duration && (
                                  <span className="mycv-exp-duration">{exp.duration}</span>
                                )}
                              </div>
                              {exp.description && (
                                <div className="mycv-exp-desc">
                                  {exp.description.split('\n').filter(Boolean).map((line, i) => (
                                    <div key={i} className="mycv-exp-bullet">
                                      <span className="mycv-bullet-dot" />
                                      {line.replace(/^[-•]\s*/, '')}
                                    </div>
                                  ))}
                                </div>
                              )}
                            </div>
                          ))}
                          {(!displayData?.experience || displayData.experience.length === 0) && (
                            <p className="mycv-empty-note">{t('mycv.no_experience', 'Chưa có kinh nghiệm')}</p>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                {/* ---------- Action Alert Banner ---------- */}
                {isConfirmed && (
                  <div className="mycv-ready-banner">
                    <div className="mycv-ready-banner-left">
                      <div className="mycv-ready-banner-icon">
                        <Sparkles size={18} />
                      </div>
                      <div>
                        <h4>{t('mycv.ready_to_practice', 'Sẵn sàng để luyện tập?')}</h4>
                        <p>{t('mycv.practice_desc', 'AI đã cá nhân hóa bộ câu hỏi phỏng vấn dựa trên CV vừa tải lên.')}</p>
                      </div>
                    </div>
                    <button
                      type="button"
                      className="mycv-btn mycv-btn--primary mycv-btn--sm"
                        onClick={() => {
                          beginNewInterviewCampaign();
                          navigate(USER_ROUTES.INTERVIEW_MODE);
                        }}
                    >
                      {t('mycv.start_practice', 'Bắt đầu phỏng vấn ngay')}
                      <ChevronRight size={14} />
                    </button>
                  </div>
                )}
                
              </>
            )}
          </div>
        )}

        {/* ==================== FEEDBACK MODAL ==================== */}
        {showFeedbackModal && parsedData && (
          <div className="mycv-modal-overlay" onClick={() => setShowFeedbackModal(false)}>
            <div className="mycv-modal" onClick={(e) => e.stopPropagation()}>
              <div className="mycv-modal-header">
                <div className="mycv-modal-header-left">
                  <Sparkles size={20} className="mycv-pulse-icon" />
                  <h3>{t('mycv.modal_title', 'Đánh giá & Phản hồi CV từ AI')}</h3>
                </div>
                <button onClick={() => setShowFeedbackModal(false)} className="mycv-modal-close-btn">
                  {t('mycv.close', 'Đóng')}
                </button>
              </div>

              <div className="mycv-modal-tabs">
                {['overall', 'strengths', 'improvements'].map((tab) => (
                  <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`mycv-tab ${activeTab === tab ? 'mycv-tab--active' : ''}`}
                  >
                    {t(`mycv.tab_${tab}`, tab)}
                  </button>
                ))}
              </div>

              <div className="mycv-modal-body">
                {activeTab === 'overall' && (
                  <div className="mycv-modal-section">
                    <div className="mycv-modal-text">
                      <h4>{t('mycv.summary_title', 'Tóm tắt đánh giá:')}</h4>
                      <p>{parsedData.overallAssessment || t('mycv.mock_summary_desc', 'Đang tải tóm tắt đánh giá...')}</p>
                    </div>
                  </div>
                )}
                {activeTab === 'strengths' && (
                  <div className="mycv-modal-section">
                    {renderFeedbackList(parsedData.strengths, 'success', t)}
                  </div>
                )}
                {activeTab === 'improvements' && (
                  <div className="mycv-modal-section">
                    {renderFeedbackList(parsedData.weaknesses, 'warning', t)}
                  </div>
                )}
              </div>

              <div className="mycv-modal-footer">
                <button onClick={() => setShowFeedbackModal(false)} className="mycv-btn mycv-btn--primary mycv-btn--sm">
                  {t('mycv.got_it', 'Đã hiểu')}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* PDF Viewer Modal */}
        {showPdfModal && cvData?.filePath && (
          <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/70 backdrop-blur-sm p-4 md:p-8 animate-in fade-in">
            <div className="bg-white rounded-2xl shadow-xl w-full max-w-6xl h-full flex flex-col overflow-hidden relative">
              <div className="flex justify-between items-center px-6 py-4 border-b border-border bg-surface-1">
                <h3 className="font-bold text-text-primary text-lg flex items-center gap-2">
                  <FileText size={20} className="text-primary" />
                  {cvData.fileName}
                </h3>
                <button
                  onClick={() => setShowPdfModal(false)}
                  className="p-2 hover:bg-surface-2 rounded-full transition-colors"
                >
                  <X size={20} className="text-text-secondary" />
                </button>
              </div>
              <div className="flex-1 w-full bg-surface-2 relative">
                <iframe
                  src={`${API_BASE_URL}${cvData.filePath}`}
                  title={cvData.fileName}
                  className="w-full h-full border-0"
                />
              </div>
            </div>
          </div>
        )}
      </div >
    </UserLayout >
  );
}

export default MyCVPage;
