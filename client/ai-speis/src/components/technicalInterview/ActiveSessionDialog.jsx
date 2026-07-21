import React from 'react';
import { Loader2, LogOut, PlayCircle, X } from 'lucide-react';
import useAccessibleDialog from './useAccessibleDialog';

const getDisplayedSession = (campaign, preferredSessionId) => (
  campaign?.sessions?.find((session) => String(session.interviewSessionId) === String(preferredSessionId))
  || campaign?.sessions?.find((session) => session.status === 'Active')
  || campaign?.sessions?.find((session) => session.status === 'Pending')
  || null
);

function ActiveSessionDialog({
  conflict,
  language = 'vi',
  busyAction,
  suspended = false,
  onResume,
  onEndSession,
  onCloseCampaign,
  onBack,
  t,
}) {
  const isOpen = Boolean(conflict);
  const dialogRef = useAccessibleDialog({
    isOpen: isOpen && !suspended,
    onClose: onBack,
    closeDisabled: Boolean(busyAction),
  });
  if (!isOpen) return null;

  const campaign = conflict.campaign;
  const session = getDisplayedSession(campaign, conflict.sessionId);
  const locale = language === 'en' ? 'en-US' : 'vi-VN';
  const formatDate = (value) => value
    ? new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
    : t('activeSession.notAvailable');
  const canResume = conflict.canResume !== false && Boolean(session);
  const canEnd = conflict.canEnd ?? session?.status === 'Active';
  const canCloseCampaign = conflict.canCloseCampaign !== false;
  const interviewType = session?.interviewRoundType
    ? t(`rounds.${session.interviewRoundType}`, { defaultValue: session.interviewRoundType })
    : t('common.unknown');
  const rawStatus = session?.status || campaign?.status;
  const displayedStatus = rawStatus
    ? t(`activeSession.statuses.${rawStatus}`, { defaultValue: rawStatus })
    : t('common.unknown');

  return (
    <div className="setup-dialog-backdrop" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget && !busyAction) onBack();
    }}>
      <section
        ref={dialogRef}
        className="setup-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="active-session-dialog-title"
        aria-describedby="active-session-dialog-description"
        aria-hidden={suspended || undefined}
        tabIndex={-1}
      >
        <header className="setup-dialog__header">
          <div>
            <span>{t('activeSession.eyebrow')}</span>
            <h2 id="active-session-dialog-title">{t('activeSession.title')}</h2>
          </div>
          <button type="button" onClick={onBack} disabled={Boolean(busyAction)} aria-label={t('activeSession.back')}>
            <X size={20} aria-hidden="true" />
          </button>
        </header>

        <p id="active-session-dialog-description" className="setup-dialog__description">
          {t('activeSession.description')}
        </p>

        <dl className="setup-dialog__details">
          <div><dt>{t('activeSession.campaign')}</dt><dd>#{campaign?.interviewCampaignId}</dd></div>
          <div><dt>{t('activeSession.interviewType')}</dt><dd>{interviewType}</dd></div>
          <div><dt>{t('activeSession.status')}</dt><dd>{displayedStatus}</dd></div>
          <div><dt>{t('activeSession.startedAt')}</dt><dd>{formatDate(campaign?.startedAt || campaign?.createdAt)}</dd></div>
          <div><dt>{t('activeSession.completedQuestions')}</dt><dd>{session?.completedQuestionCount ?? conflict.completedQuestionCount ?? 0}</dd></div>
          <div><dt>{t('activeSession.updatedAt')}</dt><dd>{formatDate(session?.updatedAt || campaign?.updatedAt)}</dd></div>
        </dl>

        <div className="setup-dialog__actions">
          {canResume && (
            <button type="button" className="setup-dialog__primary" onClick={onResume} disabled={Boolean(busyAction)}>
              {busyAction === 'resume' ? <Loader2 size={18} className="setup-spin" /> : <PlayCircle size={18} />}
              {t('activeSession.resume')}
            </button>
          )}
          {canEnd && (
            <button type="button" className="setup-dialog__danger-outline" onClick={onEndSession} disabled={Boolean(busyAction)}>
              <LogOut size={18} />
              {t('activeSession.endSession')}
            </button>
          )}
          {canCloseCampaign && (
            <button type="button" className="setup-dialog__danger" onClick={onCloseCampaign} disabled={Boolean(busyAction)}>
              {t('activeSession.closeCampaign')}
            </button>
          )}
          <button type="button" className="setup-dialog__back" onClick={onBack} disabled={Boolean(busyAction)}>
            {t('activeSession.back')}
          </button>
        </div>
      </section>
    </div>
  );
}

export default ActiveSessionDialog;
