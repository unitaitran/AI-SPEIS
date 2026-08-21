import React from 'react';
import { AlertTriangle, Loader2, X } from 'lucide-react';
import useAccessibleDialog from '../technicalInterview/useAccessibleDialog';

function BehavioralRoomDialog({ dialog, busy, onCancel, onConfirm, t, mode, roundType }) {
  const isOpen = Boolean(dialog);
  const dialogRef = useAccessibleDialog({ isOpen, onClose: onCancel, closeDisabled: busy });
  if (!isOpen) return null;

  const isEnd = dialog?.type === 'end';
  const isReal = isEnd && (
    String(mode || '').toLowerCase().includes('mock') ||
    String(mode || '').toLowerCase().includes('real')
  );

  const getTitle = () => {
    if (!isEnd) {
      return t ? t('leaveInterviewTitle', { defaultValue: 'Rời khỏi phòng phỏng vấn?' }) : 'Rời khỏi phòng phỏng vấn?';
    }
    if (isReal) {
      return t ? t('endRealTitle', { defaultValue: 'Bạn muốn kết thúc như thế nào?' }) : 'Bạn muốn kết thúc như thế nào?';
    }
    if (roundType === 'Coding') {
      return 'Kết thúc bài phỏng vấn Coding?';
    }
    if (roundType === 'Technical') {
      return t ? t('endInterviewTitle', { defaultValue: 'Kết thúc phỏng vấn Technical?' }) : 'Kết thúc phỏng vấn Technical?';
    }
    if (roundType === 'Behavior') {
      return t ? t('endInterviewTitle', { defaultValue: 'Kết thúc vòng phỏng vấn hành vi?' }) : 'Kết thúc vòng phỏng vấn hành vi?';
    }
    return t ? t('endInterviewTitle', { defaultValue: 'Kết thúc phỏng vấn?' }) : 'Kết thúc phỏng vấn?';
  };

  const getDescription = () => {
    if (!isEnd) {
      return t
        ? t('leaveInterviewDescription', { defaultValue: 'Bạn có thể tiếp tục phiên đang hoạt động từ luồng phỏng vấn.' })
        : 'Bạn có thể tiếp tục phiên đang hoạt động sau.';
    }
    if (isReal) {
      return t
        ? t('endRealDescription', { defaultValue: 'Bạn có thể chọn chỉ kết thúc vòng hiện tại hoặc kết thúc toàn bộ buổi phỏng vấn.' })
        : 'Bạn có thể chọn chỉ kết thúc vòng hiện tại hoặc kết thúc toàn bộ buổi phỏng vấn.';
    }
    return t
      ? t('endInterviewDescription', { defaultValue: 'Hệ thống sẽ hoàn tất vòng và tổng hợp kết quả đánh giá hiện có.' })
      : 'Hệ thống sẽ hoàn tất vòng và tổng hợp kết quả đánh giá hiện có.';
  };

  const getWarning = () => {
    if (isEnd) {
      return t
        ? t('unansweredWarning', { defaultValue: 'Các câu chưa trả lời có thể ảnh hưởng đến kết quả cuối.' })
        : 'Các câu chưa trả lời có thể ảnh hưởng đến kết quả cuối.';
    }
    return t
      ? t('draftWarning', { defaultValue: 'Bản ghi âm hoặc bản nháp chưa gửi sẽ không được lưu lại.' })
      : 'Bản ghi âm hoặc bản nháp chưa gửi sẽ không được lưu lại.';
  };

  return (
    <div className="behavior-dialog-backdrop" role="presentation">
      <section
        ref={dialogRef}
        className="behavior-dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="behavior-dialog-title"
        aria-describedby="behavior-dialog-description"
        tabIndex={-1}
      >
        <button
          type="button"
          className="behavior-dialog__close"
          onClick={onCancel}
          disabled={busy}
          aria-label={t ? t('closeDialog', { defaultValue: 'Đóng' }) : 'Đóng'}
        >
          <X size={18} />
        </button>

        <div className="behavior-dialog__mascot-wrapper">
          <img
            src="/confuse.png"
            alt="AI-SPEIS Assistant Mascot"
            className="behavior-dialog__mascot-img"
          />
        </div>

        <div className="behavior-dialog__content">
          <h2 id="behavior-dialog-title">{getTitle()}</h2>
          <p id="behavior-dialog-description">{getDescription()}</p>
        </div>

        <div className="behavior-dialog__warning">
          <AlertTriangle size={18} className="behavior-dialog__warning-icon" />
          <span>{getWarning()}</span>
        </div>

        <footer className={`behavior-dialog__footer ${isEnd && isReal ? 'behavior-dialog__footer--real' : ''}`}>
          <button
            type="button"
            className="behavior-dialog__btn-cancel"
            onClick={onCancel}
            disabled={busy}
          >
            {t ? t('keepInterviewing', { defaultValue: 'Tiếp tục phỏng vấn' }) : 'Tiếp tục phỏng vấn'}
          </button>

          {isEnd && isReal ? (
            <>
              <button
                type="button"
                className="behavior-dialog__btn-round"
                onClick={() => onConfirm('endRound')}
                disabled={busy}
              >
                {busy ? <Loader2 size={16} className="behavior-spin" /> : null}
                {t ? t('endRoundOnly', { defaultValue: 'Kết thúc vòng này' }) : 'Kết thúc vòng này'}
              </button>
              <button
                type="button"
                className="behavior-dialog__btn-danger"
                onClick={() => onConfirm('endAll')}
                disabled={busy}
              >
                {busy ? <Loader2 size={16} className="behavior-spin" /> : null}
                {t ? t('endAllRounds', { defaultValue: 'Kết thúc toàn bộ buổi' }) : 'Kết thúc toàn bộ buổi'}
              </button>
            </>
          ) : (
            <button
              type="button"
              className="behavior-dialog__btn-danger"
              onClick={() => onConfirm(isEnd ? 'endRound' : 'leave')}
              disabled={busy}
            >
              {busy ? <Loader2 size={16} className="behavior-spin" /> : null}
              {isEnd
                ? (t ? t('confirmEnd', { defaultValue: 'Kết thúc phỏng vấn' }) : 'Kết thúc phỏng vấn')
                : (t ? t('confirmLeave', { defaultValue: 'Rời khỏi phòng' }) : 'Rời khỏi phòng')}
            </button>
          )}
        </footer>
      </section>
    </div>
  );
}

export default BehavioralRoomDialog;
