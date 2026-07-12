import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ArrowRight, FileText, CalendarDays, TrendingUp, Zap, Target } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';

function DashboardPage() {
  const { t } = useTranslation('dashboard');
  const [user, setUser] = useState(null);

  useEffect(() => {
    // Try to load user from localStorage
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        setUser(JSON.parse(userStr));
      } catch (e) { }
    }
  }, []);

  const stats = [
    { label: t('stats.interviews', 'BUỔI PHỎNG VẤN ĐÃ LUYỆN'), value: '12', unit: t('stats.unit_session', 'buổi'), icon: CalendarDays, color: 'text-blue-500', bg: 'bg-blue-50' },
    { label: t('stats.avg_score', 'ĐIỂM TRUNG BÌNH'), value: '4.5', unit: '/ 5', icon: Target, color: 'text-primary-dark', bg: 'bg-primary-xlight' },
    { label: t('stats.streak', 'STREAK LUYỆN TẬP'), value: '4', unit: t('stats.unit_day', 'ngày'), icon: Zap, color: 'text-yellow-500', bg: 'bg-yellow-50' },
    { label: t('stats.quota', 'QUOTA CÒN LẠI'), value: '5', unit: t('stats.unit_times', 'lượt'), icon: TrendingUp, color: 'text-green-500', bg: 'bg-green-50' },
  ];

  const suggestions = [
    {
      title: t('suggestions.item_1.title', 'Mô tả một dự án khó khăn nhất bạn từng tham gia.'),
      desc: t('suggestions.item_1.desc', 'Tập trung vào kỹ năng giải quyết vấn đề và leadership thể hiện trong dự án ReactJS.')
    },
    {
      title: t('suggestions.item_2.title', 'Tại sao bạn lại chọn chuyển hướng sang lĩnh vực Data Science?'),
      desc: t('suggestions.item_2.desc', 'Chuẩn bị câu chuyện chuyển đổi nghề nghiệp logic và thuyết phục.')
    },
    {
      title: t('suggestions.item_3.title', 'Điểm yếu lớn nhất của bạn trong công việc là gì?'),
      desc: t('suggestions.item_3.desc', 'Cách trả lời trung thực nhưng vẫn thể hiện sự cầu tiến và giải pháp khắc phục.')
    }
  ];

  // Mock data for the chart (0-10 scale)
  const skills = [
    { label: t('chart.skill_confidence', 'Tự tin'), score: 4.2 },
    { label: t('chart.skill_expertise', 'Chuyên môn'), score: 6.8 },
    { label: t('chart.skill_communication', 'Giao tiếp'), score: 5.3 },
    { label: t('chart.skill_critical', 'Phản biện'), score: 9.1 },
    { label: t('chart.skill_language', 'Ngoại ngữ'), score: 7.5 }
  ];

  return (
    <UserLayout>
      <div className="space-y-8 pb-10 animate-pageEntrance">

        {/* Page Header */}
        <section>
          <h1 className="text-3xl font-bold text-text-primary tracking-tight mb-1">Dashboard</h1>
          <p className="text-base text-text-secondary">
            {t('greeting_prefix', 'Chào buổi sáng,')} <span className="font-semibold text-primary-dark">{user ? user.fullName : 'User'}</span> 👋
          </p>
        </section>

        {/* Stats Row */}
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {stats.map((stat, idx) => (
            <div key={idx} className="bg-surface-2 p-5 rounded-xl border border-border shadow-sm flex flex-col justify-center relative overflow-hidden group hover:border-primary-light hover:shadow-md hover:-translate-y-1 transition-all duration-300">
              <div className={`absolute top-0 right-0 w-16 h-16 -mr-4 -mt-4 rounded-full opacity-20 transition-transform duration-500 group-hover:scale-[1.8] ${stat.bg}`}></div>
              <div className="flex items-center justify-between mb-3">
                <span className="text-[11px] font-bold text-text-secondary uppercase tracking-widest line-clamp-1">
                  {stat.label}
                </span>
                <div className={`p-1.5 rounded-lg ${stat.bg} ${stat.color}`}>
                  <stat.icon size={16} />
                </div>
              </div>
              <div className="flex items-baseline relative z-10">
                <span className="text-3xl font-bold text-text-primary mr-1.5">{stat.value}</span>
                <span className="text-sm font-medium text-text-secondary">{stat.unit}</span>
              </div>
            </div>
          ))}
        </section>

        {/* Content Row 1: CTA and Chart */}
        <section className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Vibrant CTA Card */}
          <div className="lg:col-span-4 bg-gradient-to-br from-primary to-[#4A90E2] text-white p-8 rounded-2xl flex flex-col justify-between min-h-[320px] shadow-lg relative overflow-hidden">
            {/* Decorative circles */}
            <div className="absolute top-0 right-0 -mt-10 -mr-10 w-40 h-40 bg-white opacity-10 rounded-full blur-2xl"></div>
            <div className="absolute bottom-0 left-0 -mb-10 -ml-10 w-40 h-40 bg-black opacity-10 rounded-full blur-2xl"></div>

            <div className="relative z-10">
              <h2 className="text-3xl font-bold mb-4 leading-tight drop-shadow-sm">
                {t('banner.title', 'Sẵn sàng\nluyện phỏng\nvấn?').split('\n').map((line, idx) => (
                  <React.Fragment key={idx}>
                    {line}
                    {idx < t('banner.title', 'Sẵn sàng\nluyện phỏng\nvấn?').split('\n').length - 1 && <br />}
                  </React.Fragment>
                ))}
              </h2>
              <p className="text-white/80 text-sm mb-8 leading-relaxed font-medium">
                {t('banner.desc', 'Bắt đầu mock interview dựa trên CV và vị trí bạn đang ứng tuyển. Hệ thống AI sẽ phân tích và đưa ra phản hồi chi tiết.')}
              </p>
            </div>
            <button
              className="relative z-10 bg-white text-primary-dark hover:bg-primary-xlight py-3 px-6 rounded-lg font-bold text-sm flex items-center justify-between shadow-md hover:shadow-lg hover:-translate-y-1 transition-all duration-300 w-full sm:w-auto self-start group cursor-pointer"
              onClick={() => navigate(USER_ROUTES.INTERVIEW_SETUP)}
            >
              {t('banner.button', 'BẮT ĐẦU PHỎNG VẤN')}
              <ArrowRight size={18} className="ml-4 transform group-hover:translate-x-1 transition-transform" />
            </button>
          </div>

          {/* Skill Progress Chart */}
          <div className="lg:col-span-8 bg-surface-2 p-6 rounded-2xl border border-border shadow-sm flex flex-col">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl font-bold text-text-primary">{t('chart.title', 'Tiến độ kỹ năng')}</h3>
              <button className="text-xs font-semibold tracking-wider text-text-secondary hover:text-primary transition-colors border-b border-transparent hover:border-primary uppercase cursor-pointer">
                {t('chart.view_details', 'XEM CHI TIẾT')}
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
                  <div key={idx} className="flex flex-col items-center w-1/6 relative group">
                    {/* Tooltip */}
                    <div className="absolute bottom-full mb-2 opacity-0 group-hover:opacity-100 transition-opacity bg-text-primary text-white text-[10px] py-1 px-2 rounded pointer-events-none">
                      {skill.score}
                    </div>
                    <div
                      className="w-full bg-gradient-to-t from-primary-light to-primary hover:from-primary hover:to-primary-dark transition-all rounded-t-md shadow-sm"
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
          <h2 className="text-xl font-bold text-text-primary mb-4">{t('suggestions.title', 'Gợi ý luyện tập hôm nay')}</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {suggestions.map((item, idx) => (
              <div key={idx} className="bg-surface-2 rounded-xl border border-border shadow-sm flex flex-col group hover:border-primary-light hover:shadow-md hover:-translate-y-1 transition-all duration-300">
                <div className="p-5 flex-1">
                  <div className="inline-flex items-center space-x-1.5 px-2 py-1 bg-surface-1 border border-border rounded text-[10px] font-bold text-text-secondary uppercase tracking-wider mb-4">
                    <FileText size={12} />
                    <span>{t('suggestions.based_on_cv', 'DỰA TRÊN CV CỦA BẠN')}</span>
                  </div>
                  <h3 className="text-base font-semibold text-text-primary mb-2 line-clamp-2">
                    {item.title}
                  </h3>
                  <p className="text-sm text-text-secondary line-clamp-3">
                    {item.desc}
                  </p>
                </div>
                <div className="border-t border-border px-5 py-4">
                  <button className="text-sm font-semibold text-text-primary flex items-center group-hover:text-primary-dark transition-colors cursor-pointer">
                    {t('suggestions.practice_now', 'LUYỆN TẬP NGAY')}
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
