import { BarChart3, Bot, ChevronRight, FileText, Trophy, Upload } from 'lucide-react';
import { USER_ROUTES } from '../../../routes/routePaths';

const heroInsights = [
  { label: 'Personalized', value: 'CV · target role · skill gaps' },
  { label: 'Practice mode', value: 'Mock interview, transcript, feedback' },
  { label: 'Motivation', value: 'Streaks, badges, progress timeline' },
];

function Hero({ heroCards = [], t }) {
  const cards = heroCards.length ? heroCards : heroInsights;

  return (
    <section className="home-hero" id="hero">
      <div className="home-hero__content">
        <span className="home-kicker">{t('hero.tag')}</span>
        <h1>{t('hero.title')}</h1>
        <p className="home-hero__text">{t('hero.text')}</p>

        <div className="home-actions">
          <a className="home-button home-button--primary" href={USER_ROUTES.INTERVIEW_MODE}>
            {t('buttons.startInterview')}
            <ChevronRight size={18} />
          </a>
          <a className="home-button home-button--secondary" href="#features">
            {t('buttons.howItWorks')}
          </a>
        </div>

        <div className="home-hero__insights" aria-label={t('aria.highlights')}>
          {cards.map((item, index) => {
            const iconMap = [Upload, Trophy, BarChart3];
            const Icon = iconMap[index % iconMap.length] || FileText;

            return (
              <div className="home-insight-card" key={item.label || item.value || index}>
                <div className="home-insight-card__icon">
                  <Icon size={16} />
                </div>
                <div>
                  <span>{item.label}</span>
                  <strong>{item.value}</strong>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <div className="home-hero__visual">
        <div className="home-hero__card">
          <div className="home-hero__card-top">
            <span className="home-status-dot" />
            <span>{t('mascot.status')}</span>
          </div>
          <img src="/mascot_AI-SPEIS-removebg.png" alt={t('mascot.alt', 'AI-SPEIS mascot')} className="home-mascot" />
          <div className="home-hero__card-note">
            <Bot size={18} />
            <span>{t('mascot.note')}</span>
          </div>
        </div>
      </div>
    </section>
  );
}

export default Hero;
