import { BarChart3, Code2, FileCheck, LineChart, Mic, Sparkles } from 'lucide-react';

function BentoFeatures({ t }) {
  return (
    <section className="home-section home-bento-section" id="features">
      <div className="home-section-shell">
        <div className="home-section-heading text-center mx-auto">
          <span className="home-kicker">
            <Sparkles size={14} className="mr-1" />
            {t('bento.badge', 'TÍNH NĂNG VƯỢT TRỘI')}
          </span>
          <h2>{t('bento.title', 'Hệ thống luyện phỏng vấn toàn diện nhất cho IT Candidates')}</h2>
          <p>{t('bento.subtitle', 'Kết hợp giữa Trí tuệ nhân tạo thế hệ mới và Rubric chuẩn tuyển dụng thực tế.')}</p>
        </div>

        {/* BENTO GRID LAYOUT */}
        <div className="bento-grid">
          {/* CARD 1: FEATURED CV PARSER (SPAN 2 COLUMNS) */}
          <div className="bento-card bento-card--large">
            <div className="bento-card__badge">
              <FileCheck size={14} />
              <span>{t('bento.card1.tag', 'Cá nhân hóa 100%')}</span>
            </div>
            <h3>{t('bento.card1.title', 'Phân tích CV & JD Bằng AI Thế Hệ Mới')}</h3>
            <p>{t('bento.card1.desc', 'Không dùng câu hỏi chung chung. AI quét CV và mô tả công việc (JD) để sinh câu hỏi sát với vị trí tuyển dụng thực tế của bạn.')}</p>
            <div className="bento-preview-box bento-preview--cv">
              <div className="cv-tag-row">
                <span className="cv-pill font-mono">PDF Parser</span>
                <span className="cv-pill font-mono">JD Skill Matcher</span>
                <span className="cv-pill font-mono">Gap Detector</span>
              </div>
            </div>
          </div>

          {/* CARD 2: VOICE AI INTERVIEW */}
          <div className="bento-card">
            <div className="bento-card__badge">
              <Mic size={14} />
              <span>{t('bento.card2.tag', 'Voice STT & TTS')}</span>
            </div>
            <h3>{t('bento.card2.title', 'Giao Tiếp Giọng Nói Tự Nhiên 24/7')}</h3>
            <p>{t('bento.card2.desc', 'Tự do chọn ngôn ngữ Tiếng Việt hoặc Tiếng Anh. AI nghe, hiểu và phản hồi giọng nói tự nhiên như phỏng vấn viên thật.')}</p>
            <div className="bento-preview-box bento-preview--voice">
              <div className="voice-spectrum-mini">
                <span className="bar" />
                <span className="bar" />
                <span className="bar" />
                <span className="bar" />
              </div>
            </div>
          </div>

          {/* CARD 3: RUBRIC SCORING */}
          <div className="bento-card">
            <div className="bento-card__badge">
              <BarChart3 size={14} />
              <span>{t('bento.card3.tag', 'Rubric Doanh Nghiệp')}</span>
            </div>
            <h3>{t('bento.card3.title', 'Chấm Điểm & Phân Tích Đa Chiều')}</h3>
            <p>{t('bento.card3.desc', 'Đánh giá chi tiết 5 tiêu chí: Độ chính xác kiến thức, Khả năng giao tiếp, Cấu trúc câu trả lời, Sự tự tin và Xử lý tình huống.')}</p>
            <div className="bento-preview-box bento-preview--rubric">
              <div className="rubric-bar-mini">
                <span>Domain Knowledge: 8.5/10</span>
                <span>Communication: 9.0/10</span>
              </div>
            </div>
          </div>

          {/* CARD 4: JUDGE0 CODING SANDBOX */}
          <div className="bento-card">
            <div className="bento-card__badge">
              <Code2 size={14} />
              <span>{t('bento.card4.tag', 'Judge0 Sandbox')}</span>
            </div>
            <h3>{t('bento.card4.title', 'Môi Trường Coding Thực Hành Đa Ngôn Ngữ')}</h3>
            <p>{t('bento.card4.desc', 'Tích hợp Monaco Editor và Sandbox Judge0 chấm điểm ngay lập tức cho C, C++, Java, C#, Python, JavaScript.')}</p>
            <div className="bento-preview-box bento-preview--code">
              <code className="text-xs font-mono text-primary-dark">GCC • g++ • javac • mono • node • python3</code>
            </div>
          </div>

          {/* CARD 5: GROWTH ANALYTICS */}
          <div className="bento-card bento-card--wide">
            <div className="bento-card__badge">
              <LineChart size={14} />
              <span>{t('bento.card5.tag', 'Báo cáo Tiến độ')}</span>
            </div>
            <h3>{t('bento.card5.title', 'Theo Dõi Chuỗi Ngày & Sự Cải Thiện')}</h3>
            <p>{t('bento.card5.desc', 'Biểu đồ trực quan hóa tiến trình nâng cao điểm số qua từng tuần phỏng vấn, giúp bạn luôn duy trì động lực.')}</p>
            <div className="bento-preview-box bento-preview--streak">
              <div className="streak-pills">
                <span className="streak-badge">🔥 7-Day Practice Streak</span>
                <span className="streak-badge">🏆 Level: Senior Candidate</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

export default BentoFeatures;
