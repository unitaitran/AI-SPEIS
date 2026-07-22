import React, { useEffect, useMemo, useState } from 'react';
import {
  ArrowLeft,
  Award,
  BookOpenCheck,
  CheckCircle2,
  Lightbulb,
  RefreshCw,
  Target,
  TrendingUp,
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import { getActiveInterviewContext } from '../../utils/interviewContext';
import {
  getPerformanceBandLabel,
  getRoundLabel,
  scorePercentage,
} from '../../features/campaignResult/campaignResult';
import '../../styles/user/CampaignInterviewResult.css';

const COPY = {
  vi: {
    eyebrow: 'BÁO CÁO CUỐI CAMPAIGN',
    title: 'Kết quả phỏng vấn tổng hợp',
    subtitle: 'Một góc nhìn thống nhất từ toàn bộ các vòng bạn đã hoàn thành.',
    overall: 'Điểm tổng hợp',
    rounds: 'Kết quả từng vòng',
    dashboard: 'Năng lực tổng hợp',
    feedback: 'Feedback cuối cùng',
    strengths: 'Điểm mạnh nổi bật',
    improvements: 'Điểm cần cải thiện',
    recommendations: 'Ưu tiên tiếp theo',
    source: 'Nguồn',
    weight: 'Trọng số áp dụng',
    items: 'nội dung được chấm',
    back: 'Về trang chủ',
    newInterview: 'Luyện tập campaign mới',
    retry: 'Thử lại',
    loading: 'Đang tổng hợp kết quả campaign...',
    errorTitle: 'Chưa thể tải kết quả cuối cùng',
    campaignMissing: 'Không tìm thấy mã campaign.',
    codingDetails: 'Chi tiết bài Coding',
    passed: 'test case đạt',
  },
  en: {
    eyebrow: 'FINAL CAMPAIGN REPORT',
    title: 'Combined interview result',
    subtitle: 'One consistent view across every interview round you completed.',
    overall: 'Overall score',
    rounds: 'Round results',
    dashboard: 'Combined capabilities',
    feedback: 'Final feedback',
    strengths: 'Key strengths',
    improvements: 'Areas to improve',
    recommendations: 'Next priorities',
    source: 'Sources',
    weight: 'Applied weight',
    items: 'evaluated items',
    back: 'Back to dashboard',
    newInterview: 'Start another campaign',
    retry: 'Retry',
    loading: 'Combining campaign results...',
    errorTitle: 'The final result could not be loaded',
    campaignMissing: 'Campaign ID is missing.',
    codingDetails: 'Coding problem details',
    passed: 'test cases passed',
  },
};

function FeedbackList({ icon: Icon, title, items, tone }) {
  return (
    <article className={`campaign-feedback-card campaign-feedback-card--${tone}`}>
      <header><span><Icon size={19} /></span><h3>{title}</h3></header>
      <ul>{(items || []).map((item) => <li key={item}>{item}</li>)}</ul>
    </article>
  );
}

function CampaignInterviewResultPage({ campaignId }) {
  const storedCampaignId = useMemo(() => (
    getActiveInterviewContext()?.campaign?.interviewCampaignId || null
  ), []);
  const resolvedCampaignId = campaignId || storedCampaignId;
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const language = result?.language === 'en' ? 'en' : 'vi';
  const copy = COPY[language];

  const load = async () => {
    if (!resolvedCampaignId) {
      setError(new Error(copy.campaignMissing));
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    setError(null);
    try {
      setResult(await interviewSessionService.getCampaignResult(resolvedCampaignId));
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { load(); }, [resolvedCampaignId]); // eslint-disable-line react-hooks/exhaustive-deps

  if (isLoading || error) {
    return (
      <UserLayout>
        <section className="campaign-result-state" role={error ? 'alert' : 'status'}>
          {error ? <Target size={42} /> : <RefreshCw className="campaign-result-spin" size={42} />}
          <h1>{error ? copy.errorTitle : copy.loading}</h1>
          {error ? <p>{error.message}</p> : null}
          <div>
            {error ? <button type="button" onClick={load}><RefreshCw size={17} />{copy.retry}</button> : null}
            <button type="button" onClick={() => navigate(USER_ROUTES.DASHBOARD)}><ArrowLeft size={17} />{copy.back}</button>
          </div>
        </section>
      </UserLayout>
    );
  }

  const feedback = result?.feedback || {};
  return (
    <UserLayout>
      <main className="campaign-result-page" lang={language}>
        <header className="campaign-result-header">
          <div>
            <p>{copy.eyebrow}</p>
            <h1>{copy.title}</h1>
            <span>{copy.subtitle}</span>
          </div>
          <button type="button" onClick={() => navigate(USER_ROUTES.DASHBOARD)}>
            <ArrowLeft size={18} />{copy.back}
          </button>
        </header>

        <section className="campaign-result-hero">
          <div
            className="campaign-score-ring"
            style={{ '--campaign-score': `${scorePercentage(result.overallScore) * 3.6}deg` }}
          >
            <div><strong>{Number(result.overallScore).toFixed(2)}</strong><span>/10</span></div>
          </div>
          <div className="campaign-result-hero__copy">
            <span>{copy.overall}</span>
            <h2>{getPerformanceBandLabel(result.performanceBand, language)}</h2>
            <p>{feedback.executiveSummary}</p>
          </div>
          <div className="campaign-result-hero__meta">
            <CheckCircle2 size={20} />
            <div><strong>{result.rounds?.length || 0}</strong><span>{copy.rounds.toLowerCase()}</span></div>
          </div>
        </section>

        <section className="campaign-result-section">
          <div className="campaign-result-section__title"><Award size={21} /><h2>{copy.rounds}</h2></div>
          <div className="campaign-round-grid">
            {result.rounds?.map((round) => (
              <article className="campaign-round-card" key={round.interviewSessionId}>
                <header>
                  <div><span>{getRoundLabel(round.roundType, language)}</span><strong>{Number(round.score).toFixed(2)}<small>/10</small></strong></div>
                  <em>{getPerformanceBandLabel(round.performanceBand, language)}</em>
                </header>
                <div className="campaign-progress"><span style={{ width: `${scorePercentage(round.score)}%` }} /></div>
                <dl>
                  <div><dt>{copy.weight}</dt><dd>{Math.round(Number(round.appliedWeight) * 100)}%</dd></div>
                  <div><dt>{copy.items}</dt><dd>{round.evaluatedItemCount}</dd></div>
                </dl>
                {round.summary ? <p>{round.summary}</p> : null}
                {round.codingQuestions?.length ? (
                  <details>
                    <summary>{copy.codingDetails}</summary>
                    {round.codingQuestions.map((question) => (
                      <div className="campaign-coding-row" key={question.codingQuestionId}>
                        <span>{question.title}</span>
                        <strong>{question.passedTestCases}/{question.totalTestCases} {copy.passed}</strong>
                      </div>
                    ))}
                  </details>
                ) : null}
              </article>
            ))}
          </div>
        </section>

        <section className="campaign-result-section">
          <div className="campaign-result-section__title"><TrendingUp size={21} /><h2>{copy.dashboard}</h2></div>
          <div className="campaign-metric-grid">
            {result.dashboardMetrics?.map((metric) => (
              <article key={metric.code}>
                <div><span>{metric.name}</span><strong>{metric.score == null ? '—' : Number(metric.score).toFixed(2)}</strong></div>
                <div className="campaign-progress"><span style={{ width: `${scorePercentage(metric.score)}%` }} /></div>
                <p><b>{copy.source}:</b> {metric.sources?.join(' · ') || '—'}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="campaign-result-section">
          <div className="campaign-result-section__title"><BookOpenCheck size={21} /><h2>{copy.feedback}</h2></div>
          <div className="campaign-feedback-grid">
            <FeedbackList icon={CheckCircle2} title={copy.strengths} items={feedback.strengths} tone="positive" />
            <FeedbackList icon={Target} title={copy.improvements} items={feedback.areasForImprovement} tone="focus" />
            <FeedbackList icon={Lightbulb} title={copy.recommendations} items={feedback.recommendations} tone="next" />
          </div>
        </section>

        <footer className="campaign-result-footer">
          <button type="button" onClick={() => navigate(USER_ROUTES.INTERVIEW_MODE)}>{copy.newInterview}</button>
        </footer>
      </main>
    </UserLayout>
  );
}

export default CampaignInterviewResultPage;

