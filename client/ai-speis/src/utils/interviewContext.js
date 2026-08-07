const ACTIVE_INTERVIEW_CONTEXT_KEY = 'ai-speis:active-interview-context';
const INTERVIEW_SETUP_DRAFT_KEY = 'ai-speis:interview-setup-draft';

export function normalizeInterviewLanguage(language, fallback = 'vi') {
  if (typeof language !== 'string') return fallback;
  const normalized = language.trim().toLowerCase();
  if (normalized === 'en' || normalized.startsWith('en-')) return 'en';
  if (normalized === 'vi' || normalized.startsWith('vi-')) return 'vi';
  return fallback;
}

export function resolveInterviewLanguage(...sources) {
  const language = sources.find((source) => (
    typeof source === 'string' && /^(en|vi)(-|$)/i.test(source.trim())
  ));
  return normalizeInterviewLanguage(language);
}

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

export function getRoundOrder(roundType) {
  if (!roundType || typeof roundType !== 'string') return Number.MAX_SAFE_INTEGER;
  const normalized = roundType.trim().toLowerCase();
  if (normalized.includes('behav')) return 0;
  if (normalized.includes('tech')) return 1;
  if (normalized.includes('code') || normalized.includes('coding')) return 2;
  return Number.MAX_SAFE_INTEGER;
}

export function getNextPendingSession(campaign) {
  return [...(campaign?.sessions || [])]
    .filter((session) => session.status === 'Pending')
    .sort((left, right) => getRoundOrder(left.interviewRoundType) - getRoundOrder(right.interviewRoundType))[0] || null;
}

export function getNextOpenSession(campaign, currentSessionId) {
  const currentSession = (campaign?.sessions || []).find((s) => (
    String(s.interviewSessionId) === String(currentSessionId)
  ));
  const currentOrder = currentSession ? getRoundOrder(currentSession.interviewRoundType) : -1;

  const openSessions = [...(campaign?.sessions || [])]
    .filter((session) => {
      const isCurrent = String(session.interviewSessionId) === String(currentSessionId);
      const isDone = session.status === 'Completed' || session.status === 'Cancelled';
      const roundOrder = getRoundOrder(session.interviewRoundType);
      return !isCurrent && !isDone && roundOrder > currentOrder && roundOrder < Number.MAX_SAFE_INTEGER;
    })
    .sort((left, right) => getRoundOrder(left.interviewRoundType) - getRoundOrder(right.interviewRoundType));

  return openSessions[0] || null;
}
