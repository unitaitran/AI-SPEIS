import React from 'react';
import { AlertTriangle, Loader2, X } from 'lucide-react';
import useAccessibleDialog from '../technicalInterview/useAccessibleDialog';

function BehavioralRoomDialog({ dialog, busy, onCancel, onConfirm, t }) {
  const isOpen = Boolean(dialog);
  const dialogRef = useAccessibleDialog({ isOpen, onClose: onCancel, closeDisabled: busy });
  if (!isOpen) return null;

  const isEnd = dialog.type === 'end';
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
            <h2 id="behavior-dialog-title">{isEnd ? t('endInterviewTitle') : t('leaveInterviewTitle')}</h2>
            <p id="behavior-dialog-description">
              {isEnd ? t('endInterviewDescription') : t('leaveInterviewDescription')}
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
          <button type="button" className="behavior-dialog__confirm" onClick={onConfirm} disabled={busy}>
            {busy ? <Loader2 size={18} className="behavior-spin" /> : null}
            {isEnd ? t('confirmEnd') : t('confirmLeave')}
          </button>
        </footer>
      </section>
    </div>
  );
}

export default BehavioralRoomDialog;
