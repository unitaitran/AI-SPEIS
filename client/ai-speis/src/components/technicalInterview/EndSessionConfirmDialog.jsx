import React from 'react';
import { AlertTriangle, Loader2, X } from 'lucide-react';
import useAccessibleDialog from './useAccessibleDialog';

function EndSessionConfirmDialog({ action, isOpen: propIsOpen, isSubmitting, onConfirm, onCancel, onClose, t }) {
  const isOpen = Boolean(action || propIsOpen);
  const handleClose = onCancel || onClose || (() => {});
  const dialogRef = useAccessibleDialog({
    isOpen,
    onClose: handleClose,
    closeDisabled: isSubmitting,
  });
  if (!isOpen) return null;

  const translate = t || ((key) => {
    if (key === 'activeSession.endConfirmTitle') return 'Xác nhận kết thúc phiên phỏng vấn';
    if (key === 'activeSession.endConfirmDescription') return 'Bạn có chắc chắn muốn kết thúc sớm phiên phỏng vấn này không?';
    if (key === 'activeSession.answerWarning') return 'Lưu ý: Các câu trả lời chưa hoàn thành sẽ không được tính điểm.';
    if (key === 'activeSession.keepSession') return 'Tiếp tục làm';
    if (key === 'activeSession.confirmEnd') return 'Xác nhận kết thúc';
    if (key === 'activeSession.back') return 'Đóng';
    return key;
  });

  const isCampaign = action === 'campaign';
  return (
    <div className="setup-dialog-backdrop setup-dialog-backdrop--confirm" role="presentation">
      <section
        ref={dialogRef}
        className="setup-dialog setup-dialog--confirm"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="end-session-confirm-title"
        aria-describedby="end-session-confirm-description"
        tabIndex={-1}
      >
        <header className="setup-dialog__header">
          <div className="setup-dialog__confirm-title">
            <AlertTriangle size={24} aria-hidden="true" />
            <h2 id="end-session-confirm-title">
              {isCampaign ? translate('activeSession.closeCampaignConfirmTitle') : translate('activeSession.endConfirmTitle')}
            </h2>
          </div>
          <button type="button" onClick={handleClose} disabled={isSubmitting} aria-label={translate('activeSession.back')}>
            <X size={20} aria-hidden="true" />
          </button>
        </header>
        <p id="end-session-confirm-description" className="setup-dialog__description">
          {isCampaign ? translate('activeSession.closeCampaignConfirmDescription') : translate('activeSession.endConfirmDescription')}
        </p>
        <p className="setup-dialog__warning">{translate('activeSession.answerWarning')}</p>
        <div className="setup-dialog__actions setup-dialog__actions--confirm">
          <button type="button" className="setup-dialog__back" onClick={handleClose} disabled={isSubmitting}>
            {translate('activeSession.keepSession')}
          </button>
          <button type="button" className="setup-dialog__danger" onClick={onConfirm} disabled={isSubmitting}>
            {isSubmitting && <Loader2 size={18} className="setup-spin" />}
            {isCampaign ? translate('activeSession.confirmCloseCampaign') : translate('activeSession.confirmEnd')}
          </button>
        </div>
      </section>
    </div>
  );
}

export default EndSessionConfirmDialog;
