import React from 'react';
import { ArrowRight, Bot, CheckCircle2, FileUp, PlayCircle, Sparkles, Trophy } from 'lucide-react';

function WorkflowSection({ t }) {
  const steps = [
    {
      icon: FileUp,
      num: '01',
      title: t('flow.steps.0.title', 'Tải CV & Nhập vị trí ứng tuyển'),
      desc: t('flow.steps.0.desc', 'Tải file CV PDF của bạn và nhập mô tả công việc (JD) của vị trí mơ ước.')
    },
    {
      icon: Bot,
      num: '02',
      title: t('flow.steps.1.title', 'AI Phân tích & Sinh Bộ Câu Hỏi'),
      desc: t('flow.steps.1.desc', 'AI bóc tách kỹ năng, phát hiện khoảng trống và tạo kịch bản phỏng vấn bám sát.')
    },
    {
      icon: PlayCircle,
      num: '03',
      title: t('flow.steps.2.title', 'Thực chiến Phỏng vấn Nói & Code'),
      desc: t('flow.steps.2.desc', 'Trả lời bằng giọng nói qua Mic hoặc gõ mã giải thuật trực tiếp trên màn hình.')
    },
    {
      icon: Trophy,
      num: '04',
      title: t('flow.steps.3.title', 'Nhận Phản hồi Rubric & Lộ trình'),
      desc: t('flow.steps.3.desc', 'Đọc nhận xét chi tiết, biết rõ lỗi cần sửa và theo dõi sự tiến bộ hàng ngày.')
    }
  ];

  return (
    <section className="home-section home-workflow-section" id="flow">
      <div className="home-section-shell">
        <div className="home-section-heading text-center mx-auto">
          <span className="home-kicker">
            <Sparkles size={14} className="mr-1" />
            {t('flow.badge', 'QUY TRÌNH 4 BƯỚC')}
          </span>
          <h2>{t('flow.title', 'Chỉ 4 bước đơn giản để sẵn sàng cho phỏng vấn thực tế')}</h2>
          <p>{t('flow.subtitle', 'Thiết kế tối giản giúp bạn tập trung luyện tập không rào cản.')}</p>
        </div>

        {/* WORKFLOW CARDS GRID */}
        <div className="workflow-grid-4">
          {steps.map((step, idx) => {
            const Icon = step.icon;
            return (
              <div className="workflow-card" key={idx}>
                <div className="workflow-card__top">
                  <span className="workflow-step-num">{step.num}</span>
                  <div className="workflow-icon-box">
                    <Icon size={22} />
                  </div>
                </div>
                <h3>{step.title}</h3>
                <p>{step.desc}</p>
                {idx < steps.length - 1 && (
                  <div className="workflow-arrow-connector">
                    <ArrowRight size={18} />
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}

export default WorkflowSection;
