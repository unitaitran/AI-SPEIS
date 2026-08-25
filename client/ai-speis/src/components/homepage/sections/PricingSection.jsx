import React, { useEffect, useMemo, useState } from 'react';
import { ArrowRight, Check, Sparkles, Star } from 'lucide-react';
import { USER_ROUTES } from '../../../routes/routePaths';
import { API_BASE_URL } from '../../../config/api';
import { navigate } from '../../../routes/navigation';

function formatPriceVnd(amount, isEn) {
  if (!amount || amount === 0) return isEn ? '0 VND' : '0 VNĐ';
  return `${amount.toLocaleString(isEn ? 'en-US' : 'vi-VN')} ${isEn ? 'VND' : 'VNĐ'}`;
}

function PricingSection({ t, i18n }) {
  const [plans, setPlans] = useState([]);
  const isEn = (i18n?.language || '').toLowerCase().startsWith('en');

  useEffect(() => {
    let isMounted = true;
    fetch(`${API_BASE_URL}/api/subscription-plans`)
      .then((res) => (res.ok ? res.json() : []))
      .then((data) => {
        if (isMounted) {
          setPlans(Array.isArray(data) ? data : []);
        }
      })
      .catch(() => {
        if (isMounted) {
          setPlans([]);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const displayPackages = useMemo(() => {
    if (!plans || plans.length === 0) {
      return [
        {
          id: 'free',
          isFree: true,
          title: isEn ? 'Basic Tier (Free)' : t('pricing.free.title', 'Gói Cơ Bản (Free)'),
          desc: isEn ? 'Basic practice experience for beginners.' : t('pricing.free.desc', 'Trải nghiệm cơ bản cho người mới bắt đầu.'),
          priceDisplay: formatPriceVnd(0, isEn),
          period: '',
          features: isEn ? [
            '3 free trial mock sessions',
            'Basic CV analysis',
            'General skill assessment',
            'Dual language support (En/Vi)'
          ] : [
            '3 lượt phỏng vấn dùng thử miễn phí',
            'Phân tích CV cơ bản',
            'Đánh giá kỹ năng tổng quan',
            'Hỗ trợ 2 ngôn ngữ (Vi/En)'
          ],
          ctaText: isEn ? 'Get Started Free' : t('pricing.free.cta', 'Bắt đầu miễn phí'),
          ctaLink: USER_ROUTES.PACKAGES,
          isFeatured: false,
        },
        {
          id: 'monthly',
          isFree: false,
          title: isEn ? 'Premium 1 Month' : t('pricing.proMonthly.title', 'Premium 1 Tháng'),
          desc: isEn ? 'Flexible month-by-month practice.' : t('pricing.proMonthly.desc', 'Luyện tập linh hoạt theo tháng.'),
          priceDisplay: formatPriceVnd(60000, isEn),
          period: isEn ? '/ month' : t('pricing.monthly', '/ tháng'),
          features: isEn ? [
            '15 mock sessions per 30-day cycle',
            'Deep AI analysis & evaluation',
            'Refresh 15 sessions every 30 days',
            'Voice AI & Judge0 Sandbox',
            'Detailed Enterprise Rubric report'
          ] : [
            '15 lượt phỏng vấn mỗi chu kỳ 30 ngày',
            'Phân tích & Đánh giá chuyên sâu',
            'Làm mới 15 lượt sau mỗi 30 ngày',
            'Phỏng vấn Voice AI & Judge0 Sandbox',
            'Báo cáo Rubric Doanh nghiệp chi tiết'
          ],
          ctaText: isEn ? 'Subscribe 1 Month' : t('pricing.proMonthly.cta', 'Đăng ký gói Tháng'),
          ctaLink: `${USER_ROUTES.PACKAGES}?purchase=true`,
          isFeatured: false,
        },
        {
          id: 'yearly',
          isFree: false,
          title: isEn ? 'Premium 1 Year' : t('pricing.proYearly.title', 'Premium 1 Năm'),
          desc: isEn ? 'Most cost-effective choice for recruitment season.' : t('pricing.proYearly.desc', 'Lựa chọn tiết kiệm nhất cho mùa tuyển dụng.'),
          priceDisplay: formatPriceVnd(699000, isEn),
          period: isEn ? '/ year' : t('pricing.yearly', '/ năm'),
          discountPercent: 15,
          features: isEn ? [
            '15 mock sessions per 30-day cycle',
            'Priority monthly 15-quota refresh',
            'Advanced skill performance report',
            'Voice AI & Judge0 Sandbox',
            '-15% amortized savings vs 12x monthly',
            'History saving & PDF export'
          ] : [
            '15 lượt phỏng vấn mỗi chu kỳ 30 ngày',
            'Làm mới 15 lượt ưu tiên mỗi tháng',
            'Báo cáo kỹ năng nâng cao',
            'Phỏng vấn Voice AI & Judge0 Sandbox',
            'Khấu hao -15% so với gói Tháng x12',
            'Lưu lịch sử & Xuất báo cáo PDF'
          ],
          ctaText: isEn ? 'Subscribe 1 Year' : t('pricing.proYearly.cta', 'Đăng ký gói 1 Năm'),
          ctaLink: `${USER_ROUTES.PACKAGES}?purchase=true`,
          isFeatured: true,
        }
      ];
    }

    const sortedPlans = [...plans].sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));

    return sortedPlans.flatMap((plan) => {
      const quotaText = plan.isFree
        ? (isEn ? `${plan.interviewQuota} free trial mock sessions` : `${plan.interviewQuota} lượt phỏng vấn miễn phí`)
        : (isEn ? `${plan.interviewQuota} mock sessions per 30-day cycle` : `${plan.interviewQuota} lượt phỏng vấn mỗi chu kỳ 30 ngày`);

      const aiTierText = plan.aiTier === 'STANDARD'
        ? (isEn ? 'Basic CV analysis' : 'Phân tích CV cơ bản')
        : (isEn ? 'Deep AI analysis & evaluation' : 'Phân tích & Đánh giá chuyên sâu với AI');

      const analyticsText = plan.advancedAnalyticsEnabled
        ? (isEn ? 'Detailed Enterprise Rubric report' : 'Báo cáo Rubric Doanh nghiệp chi tiết')
        : (isEn ? 'General skill assessment' : 'Đánh giá kỹ năng tổng quan');

      const langSupport = isEn ? 'Dual language support (En/Vi)' : 'Hỗ trợ 2 ngôn ngữ (Vi/En)';

      const baseFeatures = [
        quotaText,
        aiTierText,
        analyticsText,
        langSupport,
        ...(plan.isFree ? [] : [
          isEn ? 'Voice AI & Judge0 Sandbox' : 'Phỏng vấn Voice AI & Judge0 Sandbox',
          isEn ? 'History saving & PDF export' : 'Lưu lịch sử & Xuất báo cáo PDF'
        ])
      ];

      if (plan.isFree || !plan.prices || plan.prices.length === 0) {
        return [{
          id: `plan-${plan.planId}`,
          planId: plan.planId,
          isFree: true,
          title: plan.name || (isEn ? 'Basic Tier (Free)' : 'Gói Cơ Bản (Free)'),
          desc: plan.description || (isEn ? 'Basic practice experience for beginners.' : 'Trải nghiệm cơ bản cho người mới bắt đầu.'),
          priceDisplay: formatPriceVnd(0, isEn),
          period: '',
          features: baseFeatures,
          ctaText: isEn ? 'Get Started Free' : 'Bắt đầu miễn phí',
          ctaLink: USER_ROUTES.PACKAGES,
          isFeatured: false,
        }];
      }

      const monthlyPrice = plan.prices.find((p) => p.billingCycle === 1 || p.billingCycle === 'Monthly');

      return (plan.prices || []).map((price) => {
        const isYearly = price.billingCycle === 2 || price.billingCycle === 'Yearly';
        const cycleName = isYearly ? (isEn ? '1 Year' : '1 Năm') : (isEn ? '1 Month' : '1 Tháng');
        const periodText = isYearly ? (isEn ? '/ year' : '/ năm') : (isEn ? '/ month' : '/ tháng');

        let discountPercent = null;
        if (isYearly && monthlyPrice && monthlyPrice.amount > 0) {
          const fullYearCost = monthlyPrice.amount * 12;
          if (fullYearCost > price.amount) {
            discountPercent = Math.round(((fullYearCost - price.amount) / fullYearCost) * 100);
          }
        }

        const isFeatured = Boolean(plan.isPopular || isYearly);

        const customizedFeatures = isYearly && discountPercent ? [
          ...baseFeatures.slice(0, 4),
          isEn ? `-${discountPercent}% amortized savings vs 12x monthly` : `Khấu hao -${discountPercent}% so với gói Tháng x12`,
          ...baseFeatures.slice(4)
        ] : baseFeatures;

        return {
          id: `price-${price.priceId}`,
          planId: plan.planId,
          priceId: price.priceId,
          isFree: false,
          title: `${plan.name} ${cycleName}`,
          desc: isYearly
            ? (isEn ? 'Most cost-effective choice for recruitment season.' : 'Lựa chọn tiết kiệm nhất cho mùa tuyển dụng.')
            : (plan.description || (isEn ? 'Flexible month-by-month practice.' : 'Luyện tập linh hoạt theo từng tháng.')),
          priceDisplay: formatPriceVnd(price.amount, isEn),
          period: periodText,
          discountPercent: discountPercent,
          features: customizedFeatures,
          ctaText: isYearly ? (isEn ? 'Subscribe 1 Year' : 'Đăng ký gói 1 Năm') : (isEn ? 'Subscribe 1 Month' : 'Đăng ký gói Tháng'),
          ctaLink: `${USER_ROUTES.PACKAGES}?purchase=true&priceId=${price.priceId}`,
          isFeatured: isFeatured,
        };
      });
    });
  }, [plans, isEn, t]);

  return (
    <section className="home-section home-pricing-section" id="pricing">
      <div className="home-section-shell">
        <div className="home-section-heading text-center mx-auto">
          <span className="home-kicker">
            <Sparkles size={14} className="mr-1" />
            {t('pricing.badge', 'BẢNG GIÁ MINH BẠCH')}
          </span>
          <h2>{t('pricing.title', 'Chọn gói phù hợp với mục tiêu của bạn')}</h2>
          <p>{t('pricing.subtitle', 'Đầu tư nhỏ cho cơ hội sự nghiệp lớn.')}</p>
        </div>

        {/* PRICING CARDS GRID DYNAMICALLY FETCHED FROM DB */}
        <div className="pricing-cards-grid">
          {displayPackages.map((pkg) => (
            <div
              key={pkg.id}
              className={`home-pricing-card ${pkg.isFeatured ? 'home-pricing-card--featured' : ''}`}
            >
              {pkg.isFeatured && (
                <div className="pricing-popular-badge">
                  <Star size={12} className="fill-current mr-1" />
                  <span>
                    {isEn ? `BEST VALUE (-${pkg.discountPercent || 15}%)` : `TIẾT KIỆM NHẤT (-${pkg.discountPercent || 15}%)`}
                  </span>
                </div>
              )}

              <div className="pricing-header">
                <h3>{pkg.title}</h3>
                <p className="pricing-desc">{pkg.desc}</p>
                <div className="pricing-price">
                  <span className="amount">{pkg.priceDisplay}</span>
                  {pkg.period && <span className="period">{pkg.period}</span>}
                </div>
                {pkg.discountPercent && (
                  <span className="pricing-discount-tag">
                    {isEn
                      ? `Save -${pkg.discountPercent}% vs monthly x12`
                      : `Tiết kiệm -${pkg.discountPercent}% so với gói tháng x12`}
                  </span>
                )}
              </div>

              <ul className="pricing-features-list">
                {pkg.features.map((feat, idx) => (
                  <li key={idx}>
                    <Check size={16} className="text-success flex-shrink-0" />
                    <span>{feat}</span>
                  </li>
                ))}
              </ul>

              <a
                href={pkg.ctaLink}
                onClick={(e) => {
                  e.preventDefault();
                  navigate(pkg.ctaLink);
                }}
                className={`home-button ${pkg.isFeatured ? 'home-button--primary' : 'home-button--secondary'} home-button--full`}
              >
                <span>{pkg.ctaText}</span>
                {pkg.isFeatured && <ArrowRight size={16} />}
              </a>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

export default PricingSection;
