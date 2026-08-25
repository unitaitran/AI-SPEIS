import { ArrowRight, Bot, Play, Sparkles, Star, Zap } from 'lucide-react';
import { USER_ROUTES } from '../../../routes/routePaths';
import { getStoredSession } from '../../../routes/auth';
import { beginNewInterviewCampaign } from '../../../utils/interviewContext';
import { navigate } from '../../../routes/navigation';

function Hero({ t }) {
  const session = getStoredSession();

  return (
    <section className="home-hero" id="hero">
      <div className="home-hero__content">
        <div className="home-kicker">
          <Sparkles size={14} className="mr-1.5" />
          <span>{t('hero.badge', '✨ Nền tảng Luyện Phỏng vấn AI Thế Hệ Mới')}</span>
        </div>

        <h1>{t('hero.title', 'Bứt Phá Phỏng Vấn IT Với AI Cá Nhân Hóa Theo CV & JD')}</h1>
        
        <p className="home-hero__text">
          {t('hero.text', 'Luyện phỏng vấn Hành vi, Kỹ thuật và Coding 24/7 trong môi trường an toàn. Nhận phản hồi chuyên sâu theo Rubric Doanh nghiệp ngay lập tức.')}
        </p>

        <div className="home-actions">
          <a
            className="home-button home-button--primary"
            href={session ? USER_ROUTES.INTERVIEW_MODE : '#login'}
            onClick={(e) => {
              e.preventDefault();
              if (session) {
                beginNewInterviewCampaign();
                navigate(USER_ROUTES.INTERVIEW_MODE);
              } else {
                navigate('#login');
              }
            }}
          >
            <span>{t('buttons.startInterview', 'Bắt đầu miễn phí')}</span>
            <ArrowRight size={18} />
          </a>

          <a className="home-button home-button--secondary" href="#demo">
            <Play size={16} />
            <span>{t('buttons.tryDemo', 'Dùng thử Simulator')}</span>
          </a>
        </div>

        {/* HERO QUICK PROOF STRIP */}
        <div className="hero-proof-strip">
          <div className="proof-users">
            <div className="avatar-group">
              <span className="user-avatar bg-primary">MT</span>
              <span className="user-avatar bg-info">TH</span>
              <span className="user-avatar bg-success">HN</span>
              <span className="user-avatar bg-warning">+15k</span>
            </div>
            <div className="proof-rating">
              <div className="stars flex">
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
              </div>
              <span className="proof-text font-semibold">4.9/5 từ 10,000+ sinh viên IT</span>
            </div>
          </div>
        </div>
      </div>

      {/* HERO VISUAL MOCKUP */}
      <div className="home-hero__visual">
        <div className="home-hero__card glow-card">
          <div className="home-hero__card-top">
            <span className="home-status-dot" />
            <span className="font-bold text-sm">{t('mascot.status', 'Phòng phỏng vấn AI sẵn sàng 24/7')}</span>
            <span className="badge-live-mini ml-auto">AI Active</span>
          </div>

          <div className="mascot-wrapper relative text-center">
            <img
              src="/mascot_AI-SPEIS-removebg.png"
              alt={t('mascot.alt', 'AI-SPEIS mascot')}
              className="home-mascot"
            />
            {/* FLOATING BADGES */}
            <div className="floating-badge badge-top-left">
              <Zap size={14} className="text-warning" />
              <span>CV Match: 94%</span>
            </div>
            <div className="floating-badge badge-bottom-right">
              <Bot size={14} className="text-primary-dark" />
              <span>Rubric Score: 9.2/10</span>
            </div>
          </div>

          <div className="home-hero__card-note">
            <Bot size={18} className="text-primary-dark flex-shrink-0" />
            <span>{t('mascot.note', 'AI sẽ phỏng vấn dựa trên CV, vị trí tuyển dụng và mức độ tự tin của bạn.')}</span>
          </div>
        </div>
      </div>
    </section>
  );
}

export default Hero;
