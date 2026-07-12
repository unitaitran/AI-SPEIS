const ACTIVE_INTERVIEW_CONTEXT_KEY = 'ai-speis:active-interview-context';

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
