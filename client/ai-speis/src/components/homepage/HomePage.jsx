import React from 'react';
import { useTranslation } from 'react-i18next';
import '../../styles/user/HomePage.css';
import Navbar from './sections/Navbar';
import Hero from './sections/Hero';
import MetricsStrip from './sections/MetricsStrip';
import InteractiveSimulator from './sections/InteractiveSimulator';
import BentoFeatures from './sections/BentoFeatures';
import WorkflowSection from './sections/WorkflowSection';
import ComparisonMatrix from './sections/ComparisonMatrix';
import PricingSection from './sections/PricingSection';
import TestimonialsSection from './sections/TestimonialsSection';
import FAQSection from './sections/FAQSection';
import CTASection from './sections/CTASection';
import Footer from './sections/Footer';

function HomePage({ currentHash = '', onToggleLanguage, t: propT, i18n: propI18n }) {
  const { t: hookT, i18n: hookI18n } = useTranslation('homepage');
  const t = propT || hookT;
  const i18n = propI18n || hookI18n;
  return (
    <div className="home-page-shell">
      <Navbar currentHash={currentHash} onToggleLanguage={onToggleLanguage} t={t} i18n={i18n} />

      <main className="home-page-content">
        <Hero t={t} />
        <MetricsStrip t={t} />
        <InteractiveSimulator t={t} />
        <BentoFeatures t={t} />
        <WorkflowSection t={t} />
        <ComparisonMatrix t={t} />
        <PricingSection t={t} i18n={i18n} />
        <TestimonialsSection t={t} />
        <FAQSection t={t} />
        <CTASection t={t} />
      </main>

      <Footer t={t} />
    </div>
  );
}

export default HomePage;
