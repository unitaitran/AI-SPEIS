import React from 'react';
import { Sparkles, Star } from 'lucide-react';

function TestimonialsSection({ t }) {
  const defaultTestimonials = [
    {
      name: 'Nguyễn Minh Tuấn',
      role: 'Software Engineer @ Tech Global',
      avatar: 'MT',
      text: 'AI-SPEIS giúp mình khắc phục hoàn toàn tâm lý run khi phỏng vấn Tiếng Anh. Nhận xét Rubric cực kỳ sát với câu hỏi thực tế của nhà tuyển dụng!'
    },
    {
      name: 'Trần Thu Hà',
      role: 'Frontend Developer @ FPT Software',
      avatar: 'TH',
      text: 'Tính năng bóc tách CV rồi hỏi sâu vào kiến thức React làm mình bất ngờ. Vừa phỏng vấn vừa code trực tiếp trên Judge0 cực mượt!'
    },
    {
      name: 'Lê Hoàng Nam',
      role: 'Junior Backend Dev',
      avatar: 'HN',
      text: 'Nhờ luyện chuỗi 7 ngày trên AI-SPEIS mà mình đã đỗ offer thực tập mơ ước. Cảm ơn đội ngũ phát triển rất nhiều!'
    }
  ];

  const rawTestimonials = t('testimonials.items', { returnObjects: true });
  const testimonials = Array.isArray(rawTestimonials) ? rawTestimonials : defaultTestimonials;

  return (
    <section className="home-section home-testimonials-section" id="testimonials">
      <div className="home-section-shell">
        <div className="home-section-heading text-center mx-auto">
          <span className="home-kicker">
            <Sparkles size={14} className="mr-1" />
            {t('testimonials.badge', 'ĐÁNH GIÁ TỪ ỨNG VIÊN')}
          </span>
          <h2>{t('testimonials.title', 'Hơn 10,000+ sinh viên và lập trình viên đã thành công')}</h2>
        </div>

        <div className="testimonials-grid-3">
          {testimonials.map((item, idx) => (
            <div className="testimonial-card" key={idx}>
              <div className="testimonial-stars flex mb-3">
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
                <Star size={14} className="fill-warning text-warning" />
              </div>
              <p className="testimonial-quote">"{item.text}"</p>
              <div className="testimonial-author">
                <div className="author-avatar">{item.avatar}</div>
                <div>
                  <h4 className="font-bold text-sm">{item.name}</h4>
                  <span className="text-xs text-secondary">{item.role}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

export default TestimonialsSection;
