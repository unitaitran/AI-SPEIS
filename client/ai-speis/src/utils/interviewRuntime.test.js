import {
  getRoundOrder,
  getNextOpenSession,
  resolveNextInterviewStage,
  InterviewStage,
  QuestionPreparationState,
  BackgroundGenerationState,
} from './interviewContext';

describe('Interview Runtime State Machine & Transition Tests (7 Cases)', () => {
  const sampleCampaign = {
    interviewCampaignId: 99,
    status: 'Active',
    sessions: [
      { interviewSessionId: 101, interviewRoundType: 'Behavior', status: 'Completed' },
      { interviewSessionId: 102, interviewRoundType: 'Technical', status: 'Pending', preparationState: QuestionPreparationState.READY },
      { interviewSessionId: 103, interviewRoundType: 'Code', status: 'Pending' },
    ],
  };

  test('Case 1: Behavior Complete + Technical Ready -> Transition to Technical (NOT Coding)', () => {
    const result = resolveNextInterviewStage({
      campaign: sampleCampaign,
      currentSessionId: 101,
      technicalPrepState: QuestionPreparationState.READY,
    });

    expect(result.nextStage).toBe(InterviewStage.TECHNICAL);
    expect(result.targetSessionId).toBe(102);
    expect(result.showLoadingSpinner).toBe(false);
  });

  test('Case 2: Behavior Complete + Technical Preparing -> Show Spinner then Transition to Technical', () => {
    const preparingCampaign = {
      ...sampleCampaign,
      sessions: [
        { interviewSessionId: 101, interviewRoundType: 'Behavior', status: 'Completed' },
        { interviewSessionId: 102, interviewRoundType: 'Technical', status: 'Pending', preparationState: QuestionPreparationState.PREPARING },
        { interviewSessionId: 103, interviewRoundType: 'Code', status: 'Pending' },
      ],
    };

    const result = resolveNextInterviewStage({
      campaign: preparingCampaign,
      currentSessionId: 101,
      technicalPrepState: QuestionPreparationState.PREPARING,
    });

    expect(result.nextStage).toBe(InterviewStage.TECHNICAL);
    expect(result.targetSessionId).toBe(102);
    expect(result.showLoadingSpinner).toBe(true);
  });

  test('Case 3: Behavior Complete + Coding Cached -> Transition to Technical (Coding NEVER bypasses Technical)', () => {
    const codingCachedCampaign = {
      ...sampleCampaign,
      sessions: [
        { interviewSessionId: 101, interviewRoundType: 'Behavior', status: 'Completed' },
        { interviewSessionId: 102, interviewRoundType: 'Technical', status: 'Pending', preparationState: QuestionPreparationState.READY },
        { interviewSessionId: 103, interviewRoundType: 'Code', status: 'Pending', isPreloaded: true },
      ],
    };

    const result = resolveNextInterviewStage({
      campaign: codingCachedCampaign,
      currentSessionId: 101,
      technicalPrepState: QuestionPreparationState.READY,
    });

    expect(result.nextStage).toBe(InterviewStage.TECHNICAL);
    expect(result.targetSessionId).toBe(102);
    expect(result.nextStage).not.toBe(InterviewStage.CODING);
  });

  test('Case 4: Technical Complete -> Transition to Coding', () => {
    const techCompletedCampaign = {
      ...sampleCampaign,
      sessions: [
        { interviewSessionId: 101, interviewRoundType: 'Behavior', status: 'Completed' },
        { interviewSessionId: 102, interviewRoundType: 'Technical', status: 'Completed' },
        { interviewSessionId: 103, interviewRoundType: 'Code', status: 'Pending' },
      ],
    };

    const result = resolveNextInterviewStage({
      campaign: techCompletedCampaign,
      currentSessionId: 102,
    });

    expect(result.nextStage).toBe(InterviewStage.CODING);
    expect(result.targetSessionId).toBe(103);
  });

  test('Case 5: Coding Complete -> Transition to Final Report', () => {
    const codingCompletedCampaign = {
      ...sampleCampaign,
      sessions: [
        { interviewSessionId: 101, interviewRoundType: 'Behavior', status: 'Completed' },
        { interviewSessionId: 102, interviewRoundType: 'Technical', status: 'Completed' },
        { interviewSessionId: 103, interviewRoundType: 'Code', status: 'Completed' },
      ],
    };

    const result = resolveNextInterviewStage({
      campaign: codingCompletedCampaign,
      currentSessionId: 103,
    });

    expect(result.nextStage).toBe(InterviewStage.FINAL);
    expect(result.targetSessionId).toBeNull();
  });

  test('Case 6: Background Technical Fast Completion -> MUST NOT skip Technical', () => {
    const fastGenCampaign = {
      ...sampleCampaign,
      sessions: [
        { interviewSessionId: 101, interviewRoundType: 'Behavior', status: 'Completed' },
        { interviewSessionId: 102, interviewRoundType: 'Technical', status: 'Pending', preparationState: QuestionPreparationState.READY },
        { interviewSessionId: 103, interviewRoundType: 'Code', status: 'Pending' },
      ],
    };

    const result = resolveNextInterviewStage({
      campaign: fastGenCampaign,
      currentSessionId: 101,
      backgroundState: BackgroundGenerationState.COMPLETED,
    });

    expect(result.nextStage).toBe(InterviewStage.TECHNICAL);
    expect(result.targetSessionId).toBe(102);
  });

  test('Case 7: Background Technical Fail -> Fallback Retry -> Technical', () => {
    const failedGenCampaign = {
      ...sampleCampaign,
      sessions: [
        { interviewSessionId: 101, interviewRoundType: 'Behavior', status: 'Completed' },
        { interviewSessionId: 102, interviewRoundType: 'Technical', status: 'Pending', preparationState: QuestionPreparationState.FAILED },
        { interviewSessionId: 103, interviewRoundType: 'Code', status: 'Pending' },
      ],
    };

    const result = resolveNextInterviewStage({
      campaign: failedGenCampaign,
      currentSessionId: 101,
      technicalPrepState: QuestionPreparationState.FAILED,
    });

    expect(result.nextStage).toBe(InterviewStage.TECHNICAL);
    expect(result.targetSessionId).toBe(102);
    expect(result.requiresFallbackSyncInit).toBe(true);
  });
});
