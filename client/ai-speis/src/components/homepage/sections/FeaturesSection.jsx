import { BarChart3, Bot, FileText, Trophy, Upload, Users } from 'lucide-react';

const featureIcons = [Upload, Bot, FileText, BarChart3, Trophy, Users];

function FeaturesSection({ featureCards = [], t }) {
  return (
    <section className="home-section" id="features">
      <div className="home-section-shell">
        <div className="home-section-heading">
          <span className="home-kicker">{t('sections.features.kicker')}</span>
          <h2>{t('sections.features.title')}</h2>
          <p>{t('sections.features.text')}</p>
        </div>

        <div className="home-feature-grid">
          {featureCards.map((card, index) => {
            const Icon = featureIcons[index] || Upload;
            return (
              <article key={card.title || index} className="home-card home-card--feature">
                <div className="home-card__icon">
                  <Icon size={20} />
                </div>
                <h3>{card.title}</h3>
                <p>{card.description}</p>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}

export default FeaturesSection;
