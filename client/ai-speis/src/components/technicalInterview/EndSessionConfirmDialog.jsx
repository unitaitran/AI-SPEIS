import React from 'react';
import { AlertTriangle, Loader2, X } from 'lucide-react';
import useAccessibleDialog from './useAccessibleDialog';

function EndSessionConfirmDialog({ action, isSubmitting, onConfirm, onCancel, t }) {
  const isOpen = Boolean(action);
  const dialogRef = useAccessibleDialog({
    isOpen,
    onClose: onCancel,
    closeDisabled: isSubmitting,
  });
  if (!isOpen) return null;

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
              {isCampaign ? t('activeSession.closeCampaignConfirmTitle') : t('activeSession.endConfirmTitle')}
            </h2>
          </div>
          <button type="button" onClick={onCancel} disabled={isSubmitting} aria-label={t('activeSession.back')}>
            <X size={20} aria-hidden="true" />
          </button>
        </header>
        <p id="end-session-confirm-description" className="setup-dialog__description">
          {isCampaign ? t('activeSession.closeCampaignConfirmDescription') : t('activeSession.endConfirmDescription')}
        </p>
        <p className="setup-dialog__warning">{t('activeSession.answerWarning')}</p>
        <div className="setup-dialog__actions setup-dialog__actions--confirm">
          <button type="button" className="setup-dialog__back" onClick={onCancel} disabled={isSubmitting}>
            {t('activeSession.keepSession')}
          </button>
          <button type="button" className="setup-dialog__danger" onClick={onConfirm} disabled={isSubmitting}>
            {isSubmitting && <Loader2 size={18} className="setup-spin" />}
            {isCampaign ? t('activeSession.confirmCloseCampaign') : t('activeSession.confirmEnd')}
          </button>
        </div>
      </section>
    </div>
  );
}

export default EndSessionConfirmDialog;
