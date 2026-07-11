import React from 'react';
import {
  AlertCircle,
  Briefcase,
  CheckCircle2,
  CircleAlert,
  FileText,
  Loader2,
  Plus,
  RefreshCw,
  Sparkles,
  Upload,
  X,
} from 'lucide-react';
import { useCvJdFastCheck } from './useCvJdFastCheck';
import FastCheckResult from './FastCheckResult';
import './FastCheckPanel.css';

const STATUS_INT_MAP = {
  0: 'Pending',
  1: 'Processing',
  2: 'ConfirmationRequired',
  3: 'Confirmed',
  4: 'Failed',
  5: 'AnalysisFailed',
  6: 'Archived',
};

const STATUS_LABELS = {
  Pending: 'Chờ phân tích',
  Processing: 'Đang phân tích',
  ConfirmationRequired: 'Đã trích xuất',
  Confirmed: 'Đã xác nhận',
  Failed: 'Tải lên thất bại',
  AnalysisFailed: 'Phân tích thất bại',
  Archived: 'Đã lưu trữ',
};

const PHASE_CONTENT = {
  'uploading-cv': ['Đang tải CV lên máy chủ', 'Tệp PDF đang được gửi an toàn để chuẩn bị phân tích.'],
  'parsing-cv': ['AI đang đọc CV', 'Đang trích xuất kỹ năng, kinh nghiệm và thông tin liên quan.'],
  'parsing-jd': ['AI đang đọc Job Description', 'Đang nhận diện yêu cầu bắt buộc và kỹ năng ưu tiên.'],
  matching: ['Đang đối chiếu CV với JD', 'Backend đang tạo kết quả Fast Check từ dữ liệu đã trích xuất.'],
};

const normalizeStatus = (status) => (
  typeof status === 'number' ? STATUS_INT_MAP[status] || String(status) : String(status || '')
);

const getStatusLabel = (status) => STATUS_LABELS[normalizeStatus(status)] || 'Chưa xác định';

const formatFileSize = (bytes) => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '';
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
};

function FastCheckPanel({
  currentCv,
  jds,
  loadingSources,
  onAddJd,
  onCvUploaded,
  onSourcesChanged,
}) {
  const {
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
  } = useCvJdFastCheck({ currentCv, jds, onCvUploaded, onSourcesChanged });

  const hasCvInput = Boolean(pendingCvFile || activeCv?.cvFileId);
  const canSubmit = hasCvInput && Boolean(selectedJdId) && !cvFileError && !isBusy && !loadingSources;
  const loadingContent = PHASE_CONTENT[phase];

  return (
    <section id="cv-jd-fast-check" className="fast-check" aria-labelledby="fast-check-title">
      <div className="fast-check__header">
        <div className="fast-check__header-icon" aria-hidden="true">
          <Sparkles size={24} />
        </div>
        <div>
          <p className="fast-check__eyebrow">KIỂM TRA NHANH TRƯỚC PHỎNG VẤN</p>
          <h2 id="fast-check-title">CV-JD Fast Check</h2>
          <p>Chọn CV và Job Description để AI đánh giá mức độ phù hợp dựa trên kết quả từ backend.</p>
        </div>
      </div>

      <div className="fast-check__input-grid">
        <div className={`fast-check__input-card ${cvFileError ? 'fast-check__input-card--error' : ''}`}>
          <div className="fast-check__input-heading">
            <div className="fast-check__step">1</div>
            <div>
              <h3>Curriculum Vitae</h3>
              <p>PDF từ 1 KB đến 5 MB</p>
            </div>
          </div>

          {pendingCvFile ? (
            <div className="fast-check__file-row">
              <div className="fast-check__file-icon"><FileText size={20} /></div>
              <div className="fast-check__file-copy">
                <strong title={pendingCvFile.name}>{pendingCvFile.name}</strong>
                <span>{formatFileSize(pendingCvFile.size)} · Sẵn sàng tải lên</span>
              </div>
              <button
                type="button"
                className="fast-check__icon-button"
                onClick={clearPendingCvFile}
                disabled={isBusy}
                aria-label="Bỏ tệp CV đã chọn"
              >
                <X size={18} />
              </button>
            </div>
          ) : activeCv ? (
            <div className="fast-check__file-row">
              <div className="fast-check__file-icon fast-check__file-icon--success"><CheckCircle2 size={20} /></div>
              <div className="fast-check__file-copy">
                <strong title={activeCv.fileName}>{activeCv.fileName}</strong>
                <span>CV hiện tại · {getStatusLabel(activeCv.status)}</span>
              </div>
            </div>
          ) : (
            <div className="fast-check__empty-input">
              <FileText size={24} />
              <span>Bạn chưa có CV để Fast Check.</span>
            </div>
          )}

          <label className={`fast-check__secondary-button ${isBusy ? 'fast-check__secondary-button--disabled' : ''}`}>
            <Upload size={16} />
            {hasCvInput ? 'Chọn CV khác' : 'Chọn CV PDF'}
            <input
              type="file"
              accept="application/pdf,.pdf"
              disabled={isBusy}
              onChange={(event) => {
                selectCvFile(event.target.files?.[0] || null);
                event.target.value = '';
              }}
              hidden
            />
          </label>
          {cvFileError && (
            <div className="fast-check__field-error">
              <CircleAlert size={14} />
              <span>{cvFileError}</span>
              <button type="button" onClick={clearPendingCvFile}>Bỏ qua</button>
            </div>
          )}
        </div>

        <div className="fast-check__input-card">
          <div className="fast-check__input-heading">
            <div className="fast-check__step">2</div>
            <div>
              <h3>Job Description</h3>
              <p>Chọn JD từ flow quản lý hiện có</p>
            </div>
          </div>

          <label className="fast-check__select-label" htmlFor="fast-check-jd">Job Description</label>
          <div className="fast-check__select-wrap">
            <Briefcase size={18} aria-hidden="true" />
            <select
              id="fast-check-jd"
              value={selectedJdId}
              onChange={(event) => setSelectedJdId(event.target.value)}
              disabled={isBusy || loadingSources}
            >
              <option value="">{loadingSources ? 'Đang tải danh sách JD...' : 'Chọn một Job Description'}</option>
              {jds.map((jd) => (
                <option key={jd.jdFileId} value={jd.jdFileId}>
                  {jd.fileName || 'JD nhập bằng văn bản'} · {getStatusLabel(jd.status)}
                </option>
              ))}
            </select>
          </div>

          {selectedJd ? (
            <div className="fast-check__selection-note">
              <CheckCircle2 size={16} />
              <span>
                <strong>{selectedJd.fileName || 'JD nhập bằng văn bản'}</strong>
                Backend sẽ tự phân tích JD này trước nếu chưa sẵn sàng.
              </span>
            </div>
          ) : (
            <p className="fast-check__helper">Cần chọn JD trước khi có thể thực hiện Fast Check.</p>
          )}

          <button
            type="button"
            className="fast-check__secondary-button"
            onClick={onAddJd}
            disabled={isBusy || jds.length >= 5}
          >
            <Plus size={16} /> {jds.length >= 5 ? 'Đã đạt giới hạn 5 JD' : 'Thêm JD mới'}
          </button>
        </div>
      </div>

      <div className="fast-check__action-row">
        <div className="fast-check__privacy-note">
          <CheckCircle2 size={16} />
          Chỉ sử dụng dữ liệu CV/JD thuộc tài khoản đang đăng nhập.
        </div>
        <button type="button" className="fast-check__submit" onClick={submit} disabled={!canSubmit}>
          {isBusy ? <Loader2 size={18} className="fast-check__spinner" /> : <Sparkles size={18} />}
          {isBusy ? 'Đang xử lý...' : 'Fast Check'}
        </button>
      </div>

      {!hasCvInput && <p className="fast-check__required-hint">Vui lòng cung cấp CV để bật nút Fast Check.</p>}

      {loadingContent && (
        <div className="fast-check__loading" role="status" aria-live="polite">
          <div className="fast-check__loading-icon"><Loader2 size={26} /></div>
          <div>
            <strong>{loadingContent[0]}</strong>
            <p>{loadingContent[1]}</p>
          </div>
          <div className="fast-check__loading-track" aria-hidden="true"><span /></div>
        </div>
      )}

      {error && !isBusy && (
        <div className="fast-check__error" role="alert">
          <AlertCircle size={20} />
          <div>
            <strong>Chưa thể hoàn tất Fast Check</strong>
            <p>{error}</p>
          </div>
          <button type="button" onClick={submit} disabled={!canSubmit}>
            <RefreshCw size={15} /> Thử lại
          </button>
        </div>
      )}

      {result && <FastCheckResult result={result} />}
    </section>
  );
}

export default FastCheckPanel;
