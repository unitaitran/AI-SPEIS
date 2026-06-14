import React from 'react';
import { ArrowRight, FileText } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';

function DashboardPage() {
  const stats = [
    { label: 'BUỔI PHỎNG VẤN ĐÃ LUYỆN', value: '12', unit: 'buổi' },
    { label: 'ĐIỂM TRUNG BÌNH', value: '4.5', unit: '/ 5' },
    { label: 'STREAK LUYỆN TẬP', value: '4', unit: 'ngày' },
    { label: 'QUOTA CÒN LẠI', value: '5', unit: 'lượt' },
  ];

  const suggestions = [
    {
      title: 'Mô tả một dự án khó khăn nhất bạn từng tham gia.',
      desc: 'Tập trung vào kỹ năng giải quyết vấn đề và leadership thể hiện trong dự án ReactJS.'
    },
    {
      title: 'Tại sao bạn lại chọn chuyển hướng sang lĩnh vực Data Science?',
      desc: 'Chuẩn bị câu chuyện chuyển đổi nghề nghiệp logic và thuyết phục.'
    },
    {
      title: 'Điểm yếu lớn nhất của bạn trong công việc là gì?',
      desc: 'Cách trả lời trung thực nhưng vẫn thể hiện sự cầu tiến và giải pháp khắc phục.'
    }
  ];

  // Mock data for the chart (0-10 scale)
  const skills = [
    { label: 'Tự tin', score: 4.2 },
    { label: 'Chuyên môn', score: 6.8 },
    { label: 'Giao tiếp', score: 5.3 },
    { label: 'Phản biện', score: 9.1 },
    { label: 'Ngoại ngữ', score: 7.5 }
  ];

  return (
    <UserLayout>
      <div className="space-y-8 pb-10">
        
        {/* Page Header */}
        <section>
          <h1 className="text-3xl font-bold text-text-primary tracking-tight mb-1">Dashboard</h1>
          <p className="text-base text-text-secondary">Good morning, User Name</p>
        </section>

        {/* Stats Row */}
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {stats.map((stat, idx) => (
            <div key={idx} className="bg-surface-2 p-5 rounded-xl border border-border shadow-sm flex flex-col justify-center">
              <span className="text-[11px] font-semibold text-text-secondary uppercase tracking-widest mb-3 line-clamp-1">
                {stat.label}
              </span>
              <div className="flex items-baseline">
                <span className="text-3xl font-bold text-text-primary mr-1.5">{stat.value}</span>
                <span className="text-sm text-text-secondary">{stat.unit}</span>
              </div>
            </div>
          ))}
        </section>

        {/* Content Row 1: CTA and Chart */}
        <section className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Black CTA Card */}
          <div className="lg:col-span-4 bg-text-primary text-white p-8 rounded-2xl flex flex-col justify-between min-h-[320px]">
            <div>
              <h2 className="text-3xl font-bold mb-4 leading-tight">Sẵn sàng<br/>luyện phỏng<br/>vấn?</h2>
              <p className="text-text-disabled text-sm mb-8 leading-relaxed">
                Bắt đầu mock interview dựa trên CV và vị trí bạn đang ứng tuyển. Hệ thống AI sẽ phân tích và đưa ra phản hồi chi tiết.
              </p>
            </div>
            <button className="bg-white text-text-primary hover:bg-surface-1 py-3 px-6 rounded-lg font-semibold text-sm flex items-center justify-between transition-colors w-full sm:w-auto self-start group">
              BẮT ĐẦU PHỎNG VẤN
              <ArrowRight size={18} className="ml-4 transform group-hover:translate-x-1 transition-transform" />
            </button>
          </div>

          {/* Skill Progress Chart */}
          <div className="lg:col-span-8 bg-surface-2 p-6 rounded-2xl border border-border shadow-sm flex flex-col">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-text-primary">Tiến độ kỹ năng</h3>
              <button className="text-xs font-semibold tracking-wider text-text-secondary hover:text-primary transition-colors border-b border-transparent hover:border-primary uppercase">
                XEM CHI TIẾT
              </button>
            </div>
            
            {/* Simple CSS Bar Chart implementation matching the mockup */}
            <div className="flex-1 min-h-[220px] flex items-end pt-4 relative">
              {/* Y-axis grid lines */}
              <div className="absolute inset-0 flex flex-col justify-between z-0 pb-8 pointer-events-none">
                <div className="w-full border-b border-dashed border-border flex items-end justify-start">
                  <span className="text-[10px] text-text-disabled -mb-2 -ml-4 bg-surface-2 pr-1">10</span>
                </div>
                <div className="w-full border-b border-dashed border-border flex items-end justify-start">
                  <span className="text-[10px] text-text-disabled -mb-2 -ml-4 bg-surface-2 pr-1">5</span>
                </div>
                <div className="w-full border-b border-solid border-border flex items-end justify-start">
                  <span className="text-[10px] text-text-disabled -mb-2 -ml-4 bg-surface-2 pr-1">0</span>
                </div>
              </div>
              
              {/* Bars */}
              <div className="w-full h-full flex justify-around items-end z-10 pb-8 pl-4">
                {skills.map((skill, idx) => (
                  <div key={idx} className="flex flex-col items-center w-1/6">
                    <div 
                      className="w-full bg-surface-3 border border-border/60 hover:bg-primary-light transition-colors rounded-t-sm"
                      style={{ height: `${(skill.score / 10) * 100}%` }}
                    ></div>
                  </div>
                ))}
              </div>

              {/* X-axis labels */}
              <div className="absolute bottom-0 left-4 right-0 flex justify-around">
                {skills.map((skill, idx) => (
                  <span key={idx} className="text-[11px] text-text-secondary w-1/6 text-center">
                    {skill.label}
                  </span>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* Suggestions Row */}
        <section>
          <h2 className="text-xl font-bold text-text-primary mb-4">Gợi ý luyện tập hôm nay</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {suggestions.map((item, idx) => (
              <div key={idx} className="bg-surface-2 rounded-xl border border-border shadow-sm flex flex-col group hover:border-primary-light transition-colors">
                <div className="p-5 flex-1">
                  <div className="inline-flex items-center space-x-1.5 px-2 py-1 bg-surface-1 border border-border rounded text-[10px] font-bold text-text-secondary uppercase tracking-wider mb-4">
                    <FileText size={12} />
                    <span>DỰA TRÊN CV CỦA BẠN</span>
                  </div>
                  <h3 className="text-base font-semibold text-text-primary mb-2 line-clamp-2">
                    {item.title}
                  </h3>
                  <p className="text-sm text-text-secondary line-clamp-3">
                    {item.desc}
                  </p>
                </div>
                <div className="border-t border-border px-5 py-4">
                  <button className="text-sm font-semibold text-text-primary flex items-center group-hover:text-primary-dark transition-colors">
                    LUYỆN TẬP NGAY
                    <ArrowRight size={16} className="ml-2 transform group-hover:translate-x-1 transition-transform" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        </section>

      </div>
    </UserLayout>
  );
}

export default DashboardPage;
