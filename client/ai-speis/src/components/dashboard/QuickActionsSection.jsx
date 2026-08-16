import React from 'react';
import { useTranslation } from 'react-i18next';
import { Clock, Target, FileText, ArrowRight } from 'lucide-react';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';

import { beginNewInterviewCampaign } from '../../utils/interviewContext';

/**
 * QuickActionsSection Component
 * Hiển thị 3 card hoạt động chính của người dùng trên Dashboard:
 * 1. Tổng quan lần phỏng vấn gần nhất
 * 2. Luyện tập theo kỹ năng CV
 * 3. Danh sách JD đã tải lên
 */
function QuickActionsSection({ latestInterview }) {
  const { t } = useTranslation('dashboard');

  const hasInterview = Boolean(latestInterview);

  // Dữ liệu lần phỏng vấn gần nhất (lấy theo User ID từ CSDL)
  const lastInterviewData = {
    title: latestInterview?.jobTitle || t('quick_actions.card_1.default_title', 'Phỏng vấn gần nhất'),
    score: hasInterview ? (latestInterview.score ? `${latestInterview.score}/10` : 'Chưa chấm') : '—',
    date: latestInterview?.date || '—',
    path: latestInterview?.campaignId
      ? `/user/interview/campaign-result/${latestInterview.campaignId}`
      : USER_ROUTES.INTERVIEW_HISTORY,
  };

  const cards = [
    {
      id: 'latest-interview',
      badge: t('quick_actions.card_1.badge', 'Gần đây nhất'),
      icon: Clock,
      iconBg: 'bg-blue-50 text-blue-600 dark:bg-blue-950/40 dark:text-blue-400',
      title: lastInterviewData.title,
      subtitle: hasInterview
        ? t('quick_actions.card_1.subtitle', 'Tổng quan lần phỏng vấn gần nhất')
        : t('quick_actions.card_1.no_interview_subtitle', 'Chưa có dữ liệu phỏng vấn'),
      details: [
        { label: t('quick_actions.card_1.score_label', 'Điểm tổng'), value: lastInterviewData.score, isHighlight: hasInterview },
        { label: t('quick_actions.card_1.date_label', 'Ngày phỏng vấn'), value: lastInterviewData.date, isHighlight: false },
      ],
      description: null,
      actionText: hasInterview
        ? t('quick_actions.card_1.action', 'Xem chi tiết')
        : t('quick_actions.card_1.start_action', 'Tạo phỏng vấn'),
      onClick: () => {
        if (hasInterview) {
          navigate(lastInterviewData.path);
        } else {
          beginNewInterviewCampaign();
          navigate(USER_ROUTES.INTERVIEW_MODE);
        }
      },
    },
    {
      id: 'cv-practice',
      badge: t('quick_actions.card_2.badge', 'Khuyên dùng'),
      icon: Target,
      iconBg: 'bg-amber-50 text-amber-600 dark:bg-amber-950/40 dark:text-amber-400',
      title: t('quick_actions.card_2.title', 'Câu hỏi từ CV'),
      subtitle: t('quick_actions.card_2.subtitle', 'Luyện tập theo kỹ năng'),
      details: null,
      description: t(
        'quick_actions.card_2.desc',
        'Luyện tập các câu hỏi phỏng vấn được AI tạo ra dựa trên các kỹ năng (skills) trong CV của bạn.'
      ),
      actionText: t('quick_actions.card_2.action', 'Luyện tập ngay'),
      onClick: () => navigate(USER_ROUTES.QUESTIONS),
    },
    {
      id: 'jd-management',
      badge: t('quick_actions.card_3.badge', 'Quản lý'),
      icon: FileText,
      iconBg: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-950/40 dark:text-emerald-400',
      title: t('quick_actions.card_3.title', 'Quản lý JD'),
      subtitle: t('quick_actions.card_3.subtitle', 'Danh sách vị trí ứng tuyển'),
      details: null,
      description: t(
        'quick_actions.card_3.desc',
        'Xem lại và quản lý danh sách các Job Description mà bạn đã tải lên hệ thống.'
      ),
      actionText: t('quick_actions.card_3.action', 'Xem danh sách'),
      onClick: () => navigate(USER_ROUTES.CV),
    },
  ];

  return (
    <section className="space-y-4">
      {/* Tiêu đề khu vực */}
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-bold text-text-primary tracking-tight">
          {t('quick_actions.section_title', 'Hoạt động của bạn')}
        </h2>
      </div>

      {/* Grid 3 cột trên Desktop (grid-cols-3), 1 cột trên Mobile (grid-cols-1) */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {cards.map((card) => (
          <div
            key={card.id}
            onClick={card.onClick}
            className="bg-surface-2 p-6 rounded-2xl border border-border shadow-sm flex flex-col justify-between group hover:border-primary/60 hover:shadow-lg hover:-translate-y-1 transition-all duration-300 cursor-pointer relative overflow-hidden"
          >
            {/* Hiệu ứng quầng sáng nhẹ góc card khi hover */}
            <div className="absolute top-0 right-0 w-24 h-24 -mr-6 -mt-6 rounded-full bg-primary/5 opacity-50 group-hover:scale-150 transition-transform duration-500 pointer-events-none" />

            <div>
              {/* Header card: Icon & Badge */}
              <div className="flex items-center justify-between mb-4">
                <div className={`p-3 rounded-xl ${card.iconBg} shadow-xs group-hover:scale-110 transition-transform duration-300`}>
                  <card.icon size={22} />
                </div>
                {card.badge && (
                  <span className="text-[11px] font-semibold px-2.5 py-1 rounded-full bg-surface-1 text-text-secondary border border-border">
                    {card.badge}
                  </span>
                )}
              </div>

              {/* Tiêu đề & phụ đề */}
              <h3 className="text-lg font-semibold text-text-primary group-hover:text-primary transition-colors line-clamp-1">
                {card.title}
              </h3>
              {card.subtitle && (
                <p className="text-xs text-text-secondary font-medium mt-0.5 mb-3">
                  {card.subtitle}
                </p>
              )}

              {/* Chi tiết cho Card 1 (Key-Value) */}
              {card.details && (
                <div className="bg-background/80 p-3 rounded-xl border border-border/60 space-y-2 mb-4">
                  {card.details.map((detail, idx) => (
                    <div key={idx} className="flex items-center justify-between text-xs">
                      <span className="text-text-secondary font-medium">{detail.label}:</span>
                      <span className={`font-bold ${detail.isHighlight ? 'text-primary-dark text-sm' : 'text-text-primary'}`}>
                        {detail.value}
                      </span>
                    </div>
                  ))}
                </div>
              )}

              {/* Mô tả cho Card 2 & Card 3 */}
              {card.description && (
                <p className="text-xs text-text-secondary leading-relaxed mb-4 line-clamp-3">
                  {card.description}
                </p>
              )}
            </div>

            {/* Nút hành động ở đáy card */}
            <div className="pt-3 border-t border-border/50 flex items-center justify-between text-xs font-semibold text-primary group-hover:text-primary-dark">
              <span>{card.actionText}</span>
              <ArrowRight size={16} className="transform group-hover:translate-x-1.5 transition-transform duration-300" />
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

export default QuickActionsSection;
