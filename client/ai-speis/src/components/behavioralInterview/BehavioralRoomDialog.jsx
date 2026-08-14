import React from 'react';
import { AlertTriangle, Loader2, X } from 'lucide-react';
import useAccessibleDialog from '../technicalInterview/useAccessibleDialog';

function BehavioralRoomDialog({ dialog, busy, onCancel, onConfirm, t, mode }) {
  const isOpen = Boolean(dialog);
  const dialogRef = useAccessibleDialog({ isOpen, onClose: onCancel, closeDisabled: busy });
  if (!isOpen) return null;

  const isEnd = dialog.type === 'end';
  const isReal = isEnd && (String(mode || '').toLowerCase().includes('mock') || String(mode || '').toLowerCase().includes('real'));

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
        <header>
          <span className="behavior-dialog__icon"><AlertTriangle size={24} /></span>
          <div>
            <h2 id="behavior-dialog-title">
              {isEnd
                ? (isReal ? t('endRealTitle', { defaultValue: 'Bạn muốn kết thúc như thế nào?' }) : t('endInterviewTitle'))
                : t('leaveInterviewTitle')}
            </h2>
            <p id="behavior-dialog-description">
              {isEnd
                ? (isReal ? t('endRealDescription', { defaultValue: 'Bạn có thể chọn kết thúc vòng phỏng vấn hiện tại hoặc kết thúc toàn bộ buổi phỏng vấn.' }) : t('endInterviewDescription'))
                : t('leaveInterviewDescription')}
            </p>
          </div>
          <button type="button" onClick={onCancel} disabled={busy} aria-label={t('closeDialog')}>
            <X size={20} />
          </button>
        </header>
        <div className="behavior-dialog__warning">
          {isEnd ? t('unansweredWarning') : t('draftWarning')}
        </div>
        <footer>
          <button type="button" onClick={onCancel} disabled={busy}>{t('keepInterviewing')}</button>
          {isEnd && isReal ? (
            <>
              <button type="button" className="behavior-dialog__confirm-sub" onClick={() => onConfirm('endRound')} disabled={busy}>
                {busy ? <Loader2 size={18} className="behavior-spin" /> : null}
                {t('endRoundOnly', { defaultValue: 'Kết thúc vòng này' })}
              </button>
              <button type="button" className="behavior-dialog__confirm" onClick={() => onConfirm('endAll')} disabled={busy}>
                {busy ? <Loader2 size={18} className="behavior-spin" /> : null}
                {t('endAllRounds', { defaultValue: 'Kết thúc toàn bộ buổi' })}
              </button>
            </>
          ) : (
            <button type="button" className="behavior-dialog__confirm" onClick={() => onConfirm(isEnd ? 'endRound' : 'leave')} disabled={busy}>
              {busy ? <Loader2 size={18} className="behavior-spin" /> : null}
              {isEnd ? t('confirmEnd') : t('confirmLeave')}
            </button>
          )}
        </footer>
      </section>
    </div>
  );
}

export default BehavioralRoomDialog;
