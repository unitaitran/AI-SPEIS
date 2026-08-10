import React from 'react';
import { useTranslation } from 'react-i18next';
import { Globe } from 'lucide-react';

const AuthCard = ({ children, footerText, mascotText }) => {
  const { i18n } = useTranslation();

  const toggleLanguage = () => {
    i18n.changeLanguage(i18n.language === 'vi' ? 'en' : 'vi');
  };

  return (
    <main className="w-full min-h-screen bg-background flex animate-pageEntrance relative overflow-x-hidden">
      {/* Language Switcher Button (Top Right / Bottom Left Responsive) */}
      <button
        type="button"
        onClick={toggleLanguage}
        className="fixed bottom-5 left-5 z-40 flex items-center gap-2 px-3.5 py-1.5 bg-surface border border-border rounded-full shadow-sm text-text-secondary hover:text-primary hover:border-border-strong transition-all text-xs font-semibold focus-ring select-none"
        aria-label="Đổi ngôn ngữ / Switch language"
      >
        <Globe size={15} />
        <span>{i18n.language === 'vi' ? 'VI / EN' : 'EN / VI'}</span>
      </button>

      {/* Left Column - Mascot & Visual Branding (Desktop only) */}
      <aside className="hidden lg:flex flex-1 flex-col items-center justify-center bg-gradient-to-br from-primary-xlight via-surface-muted to-secondary-xlight p-10 relative overflow-hidden border-r border-border select-none">
        {/* Decorative ambient background glows */}
        <div className="absolute -top-20 -left-20 w-96 h-96 bg-primary-light/40 rounded-full blur-3xl pointer-events-none" />
        <div className="absolute -bottom-20 -right-20 w-96 h-96 bg-secondary-light/40 rounded-full blur-3xl pointer-events-none" />

        {/* Mascot Container & Speech Bubble */}
        <div className="relative z-10 flex flex-col items-center max-w-sm text-center">
          <div className="bg-surface px-5 py-3.5 rounded-2xl rounded-br-none shadow-md border border-border mb-6 relative animate-slideDown">
            <p className="text-sm font-bold text-text-primary">
              {mascotText || 'Chào bạn! Cùng bắt đầu nhé ⭐'}
            </p>
            <div className="absolute -bottom-2.5 right-6 w-0 h-0 border-l-[8px] border-l-transparent border-t-[10px] border-t-surface border-r-[8px] border-r-transparent" />
          </div>

          <img 
            src="/mascot_AI-SPEIS-removebg.png" 
            alt="AI-SPEIS Mascot" 
            className="w-64 h-auto object-contain drop-shadow-xl hover:scale-105 transition-transform duration-300"
          />
        </div>
      </aside>

      {/* Right Column - Authentication Form Area */}
      <section className="w-full lg:flex-1 flex flex-col items-center justify-center p-6 md:p-12 overflow-y-auto">
        <div className="w-full max-w-[440px] flex flex-col items-center my-auto">
          {/* Logo Area */}
          <div className="mb-6 text-center">
            <img 
              src="/logo_AI-SPEIS-removebg.png" 
              alt="AI-SPEIS Logo" 
              className="h-16 w-auto object-contain mx-auto"
            />
          </div>

          {/* Form Content Wrapper */}
          <div className="w-full bg-surface p-6 sm:p-8 rounded-xl border border-border shadow-sm">
            {children}
          </div>

          {/* Footer Note */}
          {footerText && (
            <p className="mt-6 text-xs text-text-muted text-center leading-relaxed max-w-xs">
              {footerText}
            </p>
          )}
        </div>
      </section>
    </main>
  );
};

export default AuthCard;
