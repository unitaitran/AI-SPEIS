import React, { useState, useEffect, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Globe, X, Edit2, Save } from 'lucide-react';
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis,
  CartesianGrid, Tooltip as RechartsTooltip, Cell, ResponsiveContainer
} from 'recharts';

import './CVAnalysisDashboard.css';
// Local mock data (translations contain mockData)
import viData from "../../locales/vi/dashboard.json";
import enData from "../../locales/en/dashboard.json";

const Loading = ({label='Loading...'}) => (
  <div className="cv-loading" role="status" aria-live="polite">{label}</div>
);
const Empty = ({label='No data'}) => (
  <div className="cv-empty">{label}</div>
);
const ErrorBox = ({msg}) => (
  <div className="cv-error">{msg}</div>
);

const CVAnalysisDashboard = () => {
  const { t, i18n } = useTranslation('dashboard');
  const modalRef = useRef(null);

  const [isEditing, setIsEditing] = useState(false);
  const [selectedSkill, setSelectedSkill] = useState(null);

  const [cvData, setCvData] = useState(null);
  const [feedback, setFeedback] = useState(null);
  const [skillData, setSkillData] = useState([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [chartHeight, setChartHeight] = useState(300);

  // normalize incoming skill data
  const normalizedSkillData = useMemo(() => (
    (skillData || []).map((s, idx) => ({
      key: s.key || s.name || `skill-${idx}`,
      name: s.fullName || s.name || s.key || `Skill ${idx + 1}`,
      value: Number(s.value ?? s.score ?? 0)
    }))
  ), [skillData]);

  const getCurrentMockData = () => {
    const raw = i18n?.language === 'vi' ? viData : enData;
    return raw?.mockData || raw || {};
  };

  useEffect(() => {
    setLoading(true);
    setError(null);
    const fetchData = () => {
      try {
        const currentData = getCurrentMockData();
        setCvData(currentData.cvData || {});

        const rawFeedback = currentData.aiFeedback || {};
        const strengths = rawFeedback.strengths
          ? (Array.isArray(rawFeedback.strengths) ? rawFeedback.strengths : String(rawFeedback.strengths).split(',').map(s=>s.trim()).filter(Boolean))
          : [];
        const weaknesses = rawFeedback.weaknesses
          ? (Array.isArray(rawFeedback.weaknesses) ? rawFeedback.weaknesses : String(rawFeedback.weaknesses).split(',').map(s=>s.trim()).filter(Boolean))
          : [];
        const advice = rawFeedback.actionableAdvice || rawFeedback.advice || '';
        setFeedback({ strengths, weaknesses, advice });

        setSkillData(currentData.overviewChart || currentData.overview || []);
        setLoading(false);
      } catch (err) {
        setError('Failed to load dashboard data');
        setLoading(false);
      }
    };

    const timer = setTimeout(fetchData, 200);
    return () => clearTimeout(timer);
  }, [i18n.language]);

  // responsive chart height
  useEffect(() => {
    const onResize = () => setChartHeight(window.innerWidth < 640 ? 220 : window.innerWidth < 1024 ? 320 : 420);
    onResize();
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, []);

  // modal: close on ESC and minimal focus handling
  useEffect(() => {
    const onKey = (e) => {
      if (e.key === 'Escape') setSelectedSkill(null);
    };
    if (selectedSkill) document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [selectedSkill]);

  const toggleLanguage = () => {
    const newLang = i18n.language === 'vi' ? 'en' : 'vi';
    i18n.changeLanguage(newLang);
    setSelectedSkill(null);
  };

  const handleEditCV = () => setIsEditing(prev => !prev);
  const handleCVChange = (field, value) => setCvData(prev => ({ ...prev, [field]: value }));

  const getTimeGreeting = () => {
    const hour = new Date().getHours();
    if (hour < 12) return t('morningGreeting', 'Good morning');
    if (hour < 18) return t('afternoonGreeting', 'Good afternoon');
    return t('eveningGreeting', 'Good evening');
  };

  // modal data
  let historyLineData = [];
  let subSkillsList = [];
  if (selectedSkill) {
    const currentData = getCurrentMockData();
    if (Array.isArray(currentData.timelineValues)) {
      historyLineData = currentData.timelineValues.map(item => ({ session: item.session || item.label || 'Session', score: Number(item.score ?? item.value ?? 0) }));
    } else if (currentData.timelineLabels && Array.isArray(currentData.timelineLabels)) {
      const raw = currentData.timelineValues || [];
      historyLineData = (currentData.timelineLabels || []).map((lab, i) => ({ session: lab, score: Number(raw[i]?.score ?? raw[i] ?? 0) }));
    }
    subSkillsList = currentData.subSkills || [];
  }

  return (
    <div className="cv-page min-h-screen p-4 md:p-10 font-sans">
      <div className="cv-lang">
        <button
          type="button"
          className="language-button"
          onClick={toggleLanguage}
          aria-label={t('aria.languageSwitch', 'Toggle language')}
        >
          <Globe size={16} />
          <span className="ml-2 lang-text">{i18n.language === 'vi' ? 'VI / EN' : 'EN / VI'}</span>
        </button>
      </div>

      <header className="mb-8">
        <h1 className="title">{t('dashboard', 'Dashboard')}</h1>
        <p className="subtitle">{getTimeGreeting()}, {t('studentName', 'Learner')}</p>
      </header>

      {/* CV Confirmation */}
      <section className="cv-card mb-8">
        <div className="cv-card-header">
          <h2 className="card-title">{t('cvConfirmation', 'CV Confirmation')}</h2>
          <button onClick={handleEditCV} className="btn-edit">
            {isEditing ? <><Save size={16} /> <span className="ml-2">{t('save','Save')}</span></> : <><Edit2 size={16} /> <span className="ml-2">{t('edit','Edit')}</span></>}
          </button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
          {loading ? (
            <Loading label={t('loadingCV','Loading CV Data...')} />
          ) : cvData && Object.keys(cvData).length > 0 ? (
            Object.entries(cvData).map(([key, value]) => (
              <div key={key}>
                <label className="field-label">{t(key, key)}</label>
                {isEditing ? (
                  <textarea value={value} onChange={(e)=>handleCVChange(key, e.target.value)} rows={3} className="field-input" />
                ) : (
                  <p className="field-value">{value}</p>
                )}
              </div>
            ))
          ) : (
            <Empty label={t('noData','No CV data')} />
          )}
        </div>
      </section>

      {/* AI Feedback */}
      <section className="cv-card mb-8 card-surface">
        <div className="card-header-row">
          <h2 className="card-title">{t('aiAnalysis','AI Analysis')}</h2>
        </div>

        {loading ? <Loading label={t('loadingAI','Loading AI Feedback...')} /> : error ? <ErrorBox msg={error} /> : feedback ? (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
            <div className="feedback-card">
              <h3 className="feedback-title">{t('strengths','Strengths')}</h3>
              {feedback.strengths.length ? <ul className="list-disc list-inside mt-2">{feedback.strengths.map((s,i)=><li key={i}>{s}</li>)}</ul> : <Empty label={t('noData','No strengths')} />}
            </div>
            <div className="feedback-card">
              <h3 className="feedback-title">{t('weaknesses','Weaknesses')}</h3>
              {feedback.weaknesses.length ? <ul className="list-disc list-inside mt-2">{feedback.weaknesses.map((s,i)=><li key={i}>{s}</li>)}</ul> : <Empty label={t('noData','No weaknesses')} />}
            </div>
            <div className="feedback-card">
              <h3 className="feedback-title">{t('actionableAdvice','Actionable Advice')}</h3>
              <p className="mt-2 text-sm leading-relaxed">{feedback.advice || t('noData','No advice')}</p>
            </div>
          </div>
        ) : <Empty label={t('noData','No AI feedback')} />}
      </section>

      {/* Skill Progress */}
      <section className="cv-card mb-12">
        <div className="cv-card-header mb-4">
          <h2 className="card-title">{t('skillProgress','Skill Progress')}</h2>
          <span className="badge">{t('clickBarPrompt','Click bar for details')}</span>
        </div>

        {loading ? <Loading label={t('loadingChart','Loading Chart...')} /> : error ? <ErrorBox msg={error} /> : (normalizedSkillData && normalizedSkillData.length) ? (
          <div style={{width: '100%', height: chartHeight}}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={normalizedSkillData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#EAF3FA" />
                <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fontSize: 12, fill: '#5F7285' }} />
                <YAxis domain={[0, 100]} axisLine={false} tickLine={false} tick={{ fill: '#5F7285' }} />
                <RechartsTooltip contentStyle={{ border: '1px solid #D7E3EC' }} />
                <Bar dataKey="value" onClick={(d)=>setSelectedSkill(d?.name)}>
                  {normalizedSkillData.map((entry, idx) => (
                    <Cell key={idx} cursor="pointer" fill={entry.value >= 80 ? '#3F7FAE' : entry.value >= 60 ? '#6FB6E8' : '#B9DCF5'} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : <Empty label={t('noData','No skill data')} />}
      </section>

      {/* Skill Modal */}
      {selectedSkill && (
        <div className="cv-modal" role="dialog" aria-modal="true" aria-label={`${selectedSkill} details`} onClick={(e)=>{ if(e.target.classList.contains('cv-modal')) setSelectedSkill(null); }}>
          <div className="cv-modal-content" ref={modalRef}>
            <div className="cv-modal-header">
              <h3 className="modal-title">{selectedSkill}</h3>
              <button aria-label="Close" onClick={()=>setSelectedSkill(null)} className="btn-close"><X /></button>
            </div>
            <div className="cv-modal-body grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div className="modal-card">
                <h4 className="font-bold mb-2">{t('growthTimeline','Growth Timeline')}</h4>
                {historyLineData.length ? (
                  <div style={{height: 220}}>
                    <ResponsiveContainer width="100%" height="100%">
                      <LineChart data={historyLineData}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#EAF3FA" />
                        <XAxis dataKey="session" />
                        <YAxis domain={[0,100]} />
                        <RechartsTooltip />
                        <Line type="monotone" dataKey="score" stroke="#3F7FAE" dot={{r:5}} />
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                ) : <Empty label={t('noData','No timeline data')} />}
              </div>

              <div className="modal-card">
                <h4 className="font-bold mb-2">{t('subSkillMatrix','Sub-skill Analysis')}</h4>
                {subSkillsList.length ? (
                  <table className="w-full text-left">
                    <thead className="modal-table-head"><tr><th className="p-2">{t('metric','Metric')}</th><th className="p-2 text-right">{t('score','Score')}</th></tr></thead>
                    <tbody>{subSkillsList.map((s,i)=>(<tr key={i}><td className="p-2">{s.name}</td><td className="p-2 text-right font-bold">{s.score}/100</td></tr>))}</tbody>
                  </table>
                ) : <Empty label={t('noData','No sub-skills')} />}
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
};

export default CVAnalysisDashboard;