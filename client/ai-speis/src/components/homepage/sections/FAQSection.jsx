import React from 'react';
import { HelpCircle, Sparkles } from 'lucide-react';

function FAQSection({ t }) {
  const defaultFaqs = [
    {
      q: 'AI-SPEIS có thực sự cá nhân hóa theo CV của tôi không?',
      a: 'Có! AI-SPEIS sử dụng các mô hình LLM tiên tiến để trích xuất từng kỹ năng, dự án và kinh nghiệm trong CV PDF của bạn, từ đó đặt các câu hỏi phỏng vấn xoáy sâu vào hồ sơ của riêng bạn.'
    },
    {
      q: 'Tôi có cần cài đặt phần mềm gì không?',
      a: 'Không cần. AI-SPEIS chạy 100% trên trình duyệt web. Bạn chỉ cần micro và kết nối internet để bắt đầu phỏng vấn.'
    },
    {
      q: 'Coding Sandbox hỗ trợ những ngôn ngữ lập trình nào?',
      a: 'Hệ thống hỗ trợ C, C++, Java, C#, Python và JavaScript/Node.js qua hạ tầng biên dịch Judge0 Sandbox cực nhanh.'
    },
    {
      q: 'Tôi có thể hủy gói Pro bất cứ lúc nào không?',
      a: 'Hoàn toàn có thể. Bạn có thể quản lý và hủy gia hạn gói bất cứ lúc nào trong trang Cài đặt tài khoản.'
    }
  ];

  const rawFaqs = t('faq.items', { returnObjects: true });
  const faqItems = Array.isArray(rawFaqs) ? rawFaqs : defaultFaqs;

  return (
    <section className="home-section home-faq-section" id="faq">
      <div className="home-section-shell">
        <div className="home-section-heading text-center mx-auto">
          <span className="home-kicker">
            <Sparkles size={14} className="mr-1" />
            {t('faq.badge', 'CÂU HỎI THƯỜNG GẶP')}
          </span>
          <h2>{t('faq.title', 'Mọi thắc mắc của bạn đều có lời giải')}</h2>
        </div>

        <div className="faq-accordion-list">
          {Array.isArray(faqItems) && faqItems.map((item, index) => (
            <details className="home-faq-item" key={index}>
              <summary className="faq-question">
                <HelpCircle size={18} className="text-primary-dark mr-2 inline-block flex-shrink-0" />
                <span>{item.q}</span>
              </summary>
              <div className="faq-answer">
                <p>{item.a}</p>
              </div>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

export default FAQSection;
