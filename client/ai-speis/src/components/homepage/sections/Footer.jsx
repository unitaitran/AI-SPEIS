import React from 'react';
import {
  BarChart3,
  ChevronRight,
  CreditCard,
  FileText,
  Globe,
  HelpCircle,
  Layers,
  Mail,
  MapPin,
  PlayCircle,
  ShieldCheck,
  Sparkles,
  Zap
} from 'lucide-react';

function Footer({ t }) {
  return (
    <footer className="home-footer" role="contentinfo" aria-label="Footer">
      <div className="home-section-shell">
        {/* RESPONSIVE SAAS FOOTER GRID - CENTERED & COMPACT */}
        <div className="footer-saas-grid">
          {/* COL 1: BRAND LOGO */}
          <div className="footer-col footer-col--brand">
            <div className="footer-logo-wrapper">
              <img
                src="/logo_AI-SPEIS-removebg.png"
                alt="AI-SPEIS Logo"
                className="footer-logo-lg"
              />
              <div className="footer-logo-glow" />
            </div>
          </div>

          {/* COL 2: PRODUCT LINKS */}
          <nav className="footer-col" aria-label={t('footer.product', 'Sản phẩm')}>
            <h4 className="footer-col-title">
              <Sparkles size={16} className="text-primary-dark mr-2 inline-block" />
              <span>{t('footer.product', 'Sản phẩm')}</span>
            </h4>
            <ul className="footer-links-list">
              <li>
                <a href="#demo" className="footer-link">
                  <PlayCircle size={14} className="link-icon" />
                  <span>{t('nav.demo', 'Trải nghiệm')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
              <li>
                <a href="#features" className="footer-link">
                  <Zap size={14} className="link-icon" />
                  <span>{t('nav.features', 'Tính năng')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
              <li>
                <a href="#flow" className="footer-link">
                  <Layers size={14} className="link-icon" />
                  <span>{t('nav.flow', 'Quy trình')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
              <li>
                <a href="#comparison" className="footer-link">
                  <BarChart3 size={14} className="link-icon" />
                  <span>{t('nav.comparison', 'So sánh')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
              <li>
                <a href="#pricing" className="footer-link">
                  <CreditCard size={14} className="link-icon" />
                  <span>{t('nav.pricing', 'Bảng giá')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
            </ul>
          </nav>

          {/* COL 3: RESOURCES & LEGAL */}
          <nav className="footer-col" aria-label={t('footer.resources', 'Tài nguyên & Pháp lý')}>
            <h4 className="footer-col-title">
              <FileText size={16} className="text-primary-dark mr-2 inline-block" />
              <span>{t('footer.resources', 'Tài nguyên & Pháp lý')}</span>
            </h4>
            <ul className="footer-links-list">
              <li>
                <a href="#faq" className="footer-link">
                  <HelpCircle size={14} className="link-icon" />
                  <span>{t('nav.faq', 'Hỏi đáp')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
              <li>
                <a href="#privacy" className="footer-link">
                  <ShieldCheck size={14} className="link-icon" />
                  <span>{t('footer.privacy', 'Chính sách bảo mật')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
              <li>
                <a href="#terms" className="footer-link">
                  <FileText size={14} className="link-icon" />
                  <span>{t('footer.terms', 'Điều khoản dịch vụ')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
              <li>
                <a href="#contact" className="footer-link">
                  <Mail size={14} className="link-icon" />
                  <span>{t('footer.contact', 'Liên hệ hỗ trợ')}</span>
                  <ChevronRight size={12} className="arrow-hover" />
                </a>
              </li>
            </ul>
          </nav>

          {/* COL 4: CONTACT INFO */}
          <div className="footer-col">
            <h4 className="footer-col-title">
              <Globe size={16} className="text-primary-dark mr-2 inline-block" />
              <span>{t('footer.contactTitle', 'Liên hệ')}</span>
            </h4>

            <div className="footer-contact-items">
              <div className="contact-item">
                <Mail size={15} className="contact-icon" />
                <a href="mailto:support@ai-speis.com" className="contact-text-link">
                  {t('footer.email', 'support@ai-speis.com')}
                </a>
              </div>
              <div className="contact-item">
                <MapPin size={15} className="contact-icon" />
                <span className="contact-text">
                  {t('footer.location', 'Hà Nội, Việt Nam')}
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* BOTTOM SEPARATOR BAR - CENTERED */}
        <div className="footer-bottom-bar">
          <div className="footer-copyright">
            <span>© {new Date().getFullYear()} AI-SPEIS. {t('footer.rights', 'Tất cả các quyền được bảo lưu.')}</span>
          </div>
        </div>
      </div>
    </footer>
  );
}

export default Footer;
