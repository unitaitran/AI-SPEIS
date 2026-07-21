const ACTIVE_INTERVIEW_CONTEXT_KEY = 'ai-speis:active-interview-context';
const INTERVIEW_SETUP_DRAFT_KEY = 'ai-speis:interview-setup-draft';

export function getInterviewSetupDraft() {
  const storedDraft = sessionStorage.getItem(INTERVIEW_SETUP_DRAFT_KEY);
  if (!storedDraft) return null;

  try {
    return JSON.parse(storedDraft);
  } catch {
    sessionStorage.removeItem(INTERVIEW_SETUP_DRAFT_KEY);
    return null;
  }
}

export function saveInterviewSetupDraft(draft) {
  sessionStorage.setItem(INTERVIEW_SETUP_DRAFT_KEY, JSON.stringify(draft));
  return draft;
}

export function clearInterviewSetupDraft() {
  sessionStorage.removeItem(INTERVIEW_SETUP_DRAFT_KEY);
}

export function getActiveInterviewContext() {
  const storedContext = sessionStorage.getItem(ACTIVE_INTERVIEW_CONTEXT_KEY);
  if (!storedContext) return null;

  try {
    const context = JSON.parse(storedContext);
    if (!context?.campaign?.interviewCampaignId) return null;
    return context;
  } catch {
    sessionStorage.removeItem(ACTIVE_INTERVIEW_CONTEXT_KEY);
    return null;
  }
}

export function saveActiveInterviewContext(context) {
  if (!context?.campaign?.interviewCampaignId) {
    throw new Error('Interview campaign context is invalid');
  }

  sessionStorage.setItem(ACTIVE_INTERVIEW_CONTEXT_KEY, JSON.stringify(context));
  return context;
}

export function clearActiveInterviewContext() {
  sessionStorage.removeItem(ACTIVE_INTERVIEW_CONTEXT_KEY);
}

export function beginNewInterviewCampaign() {
  const previousCampaignId = getActiveInterviewContext()?.campaign?.interviewCampaignId || null;
  clearActiveInterviewContext();
  clearInterviewSetupDraft();
  return saveInterviewSetupDraft({ previousCampaignId });
}

export function notifyInterviewQuotaChanged(quotaOrRemaining, maxInterviewQuota, planName) {
  const detail = typeof quotaOrRemaining === 'object' && quotaOrRemaining !== null
    ? {
      remainingInterviewQuota: quotaOrRemaining.remainingInterviewQuota,
      maxInterviewQuota: quotaOrRemaining.maxInterviewQuota,
      planName: quotaOrRemaining.planName,
    }
    : {
      remainingInterviewQuota: quotaOrRemaining,
      maxInterviewQuota,
      planName,
    };

  window.dispatchEvent(new CustomEvent('interview:quota-changed', {
    detail,
  }));
}

const ROUND_ORDER = Object.freeze({
  Behavior: 0,
  Technical: 1,
  Code: 2,
});

export function getNextPendingSession(campaign) {
  return [...(campaign?.sessions || [])]
    .filter((session) => session.status === 'Pending')
    .sort((left, right) => {
      const leftOrder = ROUND_ORDER[left.interviewRoundType] ?? Number.MAX_SAFE_INTEGER;
      const rightOrder = ROUND_ORDER[right.interviewRoundType] ?? Number.MAX_SAFE_INTEGER;
      return leftOrder - rightOrder;
    })[0] || null;
}

export function getNextOpenSession(campaign, currentSessionId) {
  const activeSession = (campaign?.sessions || []).find((session) => (
    session.status === 'Active'
    && String(session.interviewSessionId) !== String(currentSessionId)
  ));
  return activeSession || getNextPendingSession(campaign);
}
