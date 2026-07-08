import { useTranslation } from 'react-i18next';
import '../../i18n';
import '../../styles/user/HomePage.css';
import Navbar from './sections/Navbar';
import Hero from './sections/Hero';
import TrustedSection from './sections/TrustedSection';
import FeaturesSection from './sections/FeaturesSection';
import WorkflowSection from './sections/WorkflowSection';
import TestimonialsSection from './sections/TestimonialsSection';
import PricingSection from './sections/PricingSection';
import FAQSection from './sections/FAQSection';
import CTASection from './sections/CTASection';
import Footer from './sections/Footer';

function HomePage({ currentHash, onToggleLanguage }) {
  const { t, i18n } = useTranslation('homepage');

  const readArray = (key) => {
    const value = t(key, { returnObjects: true });
    return Array.isArray(value) ? value : [];
  };

  const heroCards = readArray('hero.cards');
  const featureCards = readArray('sections.features.cards');
  const flowSteps = readArray('sections.flow.steps');
  const testimonials = readArray('sections.testimonials.items');
  const faqItems = readArray('sections.faq.items');

  return (
    <div className="home-page-shell">
      <Navbar currentHash={currentHash} onToggleLanguage={onToggleLanguage} t={t} i18n={i18n} />

      <main className="home-page-content">
        <Hero heroCards={heroCards} t={t} />
        <TrustedSection t={t} />
        <FeaturesSection featureCards={featureCards} t={t} />
        <WorkflowSection flowSteps={flowSteps} t={t} />
        <TestimonialsSection testimonials={testimonials} t={t} />
        <PricingSection t={t} />
        <FAQSection faqItems={faqItems} t={t} />
        <CTASection t={t} />
      </main>

      <Footer t={t} />
    </div>
  );
}

export default HomePage;
