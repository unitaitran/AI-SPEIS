import React, { useEffect, useState } from 'react';
import { ArrowRight, Check, Sparkles, Star } from 'lucide-react';
import { USER_ROUTES } from '../../../routes/routePaths';
import { API_BASE_URL } from '../../../config/api';

function formatPriceVnd(amount, isEn) {
  if (!amount || amount === 0) return isEn ? '0 VND' : '0 VNĐ';
  return `${amount.toLocaleString(isEn ? 'en-US' : 'vi-VN')} ${isEn ? 'VND' : 'VNĐ'}`;
}

function PricingSection({ t, i18n }) {
  const [plans, setPlans] = useState([]);
  const isEn = i18n?.language?.startsWith('en');

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/subscription-plans`)
      .then((res) => (res.ok ? res.json() : []))
      .then((data) => setPlans(data))
      .catch(() => setPlans([]));
  }, []);

  // Parse prices from DB plans if available
  const freePlan = plans.find((p) => p.isFree || p.code === 'FREE');
  const premiumPlan = plans.find((p) => !p.isFree && (p.code === 'PREMIUM' || p.planId === 2));

  const monthlyPriceObj = premiumPlan?.prices?.find((p) => p.billingCycle === 1 || p.billingCycle === 'Monthly');
  const yearlyPriceObj = premiumPlan?.prices?.find((p) => p.billingCycle === 2 || p.billingCycle === 'Yearly');

  const monthlyAmount = monthlyPriceObj ? monthlyPriceObj.amount : 59000;
  const yearlyAmount = yearlyPriceObj ? yearlyPriceObj.amount : 599000;

  // Calculate percentage discount for yearly vs monthly x12
  let yearlyDiscountPercent = 15;
  if (monthlyAmount > 0 && yearlyAmount > 0) {
    const fullYearCost = monthlyAmount * 12;
    if (fullYearCost > yearlyAmount) {
      yearlyDiscountPercent = Math.round(((fullYearCost - yearlyAmount) / fullYearCost) * 100);
    }
  }

  const freePriceDisplay = freePlan ? formatPriceVnd(0, isEn) : (isEn ? '0 VND' : '0 VNĐ');
  const monthlyPriceDisplay = monthlyPriceObj ? formatPriceVnd(monthlyAmount, isEn) : (isEn ? '59,000 VND' : '59.000 VNĐ');
  const yearlyPriceDisplay = yearlyPriceObj ? formatPriceVnd(yearlyAmount, isEn) : (isEn ? '599,000 VND' : '599.000 VNĐ');

  // Dynamic feature arrays for en/vi translation support
  const rawFreeFeatures = t('pricing.free.features', { returnObjects: true });
  const freeFeatures = Array.isArray(rawFreeFeatures) ? rawFreeFeatures : [
    '3 lượt phỏng vấn dùng thử miễn phí',
    'Phân tích CV cơ bản',
    'Đánh giá kỹ năng tổng quan',
    'Hỗ trợ 2 ngôn ngữ (Vi/En)'
  ];

  const rawMonthlyFeatures = t('pricing.proMonthly.features', { returnObjects: true });
  const monthlyFeatures = Array.isArray(rawMonthlyFeatures) ? rawMonthlyFeatures : [
    '15 lượt phỏng vấn mỗi chu kỳ 30 ngày',
    'Phân tích & Đánh giá chuyên sâu',
    'Làm mới 15 lượt sau mỗi 30 ngày',
    'Phỏng vấn Voice AI & Judge0 Sandbox',
    'Báo cáo Rubric Doanh nghiệp chi tiết'
  ];

  const rawYearlyFeatures = t('pricing.proYearly.features', { returnObjects: true });
  const yearlyFeatures = Array.isArray(rawYearlyFeatures) ? rawYearlyFeatures : [
    '15 lượt phỏng vấn mỗi chu kỳ 30 ngày',
    'Làm mới 15 lượt ưu tiên mỗi tháng',
    'Báo cáo kỹ năng nâng cao',
    'Phỏng vấn Voice AI & Judge0 Sandbox',
    'Khấu hao -15% so với gói Tháng x12',
    'Lưu lịch sử & Xuất báo cáo PDF'
  ];

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

        {/* PRICING CARDS GRID FROM DB WITH i18n SUPPORT */}
        <div className="pricing-cards-grid">
          {/* CARD 1: GÓI CƠ BẢN (FREE) */}
          <div className="home-pricing-card">
            <div className="pricing-header">
              <h3>{t('pricing.free.title', 'Gói Cơ Bản (Free)')}</h3>
              <p className="pricing-desc">{t('pricing.free.desc', 'Trải nghiệm cơ bản cho người mới bắt đầu.')}</p>
              <div className="pricing-price">
                <span className="amount">{freePriceDisplay}</span>
              </div>
            </div>
            <ul className="pricing-features-list">
              {freeFeatures.map((feat, idx) => (
                <li key={idx}>
                  <Check size={16} className="text-success flex-shrink-0" />
                  <span>{feat}</span>
                </li>
              ))}
            </ul>
            <a href={USER_ROUTES.PACKAGES} className="home-button home-button--secondary home-button--full">
              {t('pricing.free.cta', 'Bắt đầu miễn phí')}
            </a>
          </div>

          {/* CARD 2: PREMIUM 1 THÁNG */}
          <div className="home-pricing-card">
            <div className="pricing-header">
              <h3>{t('pricing.proMonthly.title', 'Premium 1 Tháng')}</h3>
              <p className="pricing-desc">{t('pricing.proMonthly.desc', 'Luyện tập linh hoạt theo tháng.')}</p>
              <div className="pricing-price">
                <span className="amount">{monthlyPriceDisplay}</span>
                <span className="period">{t('pricing.monthly', '/ tháng')}</span>
              </div>
            </div>
            <ul className="pricing-features-list">
              {monthlyFeatures.map((feat, idx) => (
                <li key={idx}>
                  <Check size={16} className="text-success flex-shrink-0" />
                  <span>{feat}</span>
                </li>
              ))}
            </ul>
            <a href={`${USER_ROUTES.PACKAGES}?purchase=true`} className="home-button home-button--secondary home-button--full">
              {t('pricing.proMonthly.cta', 'Đăng ký gói Tháng')}
            </a>
          </div>

          {/* CARD 3: PREMIUM 1 NĂM (FEATURED CARD - POPULAR BADGE WITH DISCOUNT) */}
          <div className="home-pricing-card home-pricing-card--featured">
            <div className="pricing-popular-badge">
              <Star size={12} className="fill-current mr-1" />
              <span>{t('pricing.popularBadge', `TIẾT KIỆM NHẤT (-${yearlyDiscountPercent}%)`)}</span>
            </div>
            <div className="pricing-header">
              <h3>{t('pricing.proYearly.title', 'Premium 1 Năm')}</h3>
              <p className="pricing-desc">{t('pricing.proYearly.desc', 'Lựa chọn tiết kiệm nhất cho mùa tuyển dụng.')}</p>
              <div className="pricing-price">
                <span className="amount">{yearlyPriceDisplay}</span>
                <span className="period">{t('pricing.yearly', '/ năm')}</span>
              </div>
              <span className="pricing-discount-tag">
                {t('pricing.yearlyDiscount', `Tiết kiệm -${yearlyDiscountPercent}% so với gói tháng x12`)}
              </span>
            </div>
            <ul className="pricing-features-list">
              {yearlyFeatures.map((feat, idx) => (
                <li key={idx}>
                  <Check size={16} className="text-success flex-shrink-0" />
                  <span>{feat}</span>
                </li>
              ))}
            </ul>
            <a href={`${USER_ROUTES.PACKAGES}?purchase=true`} className="home-button home-button--primary home-button--full">
              <span>{t('pricing.proYearly.cta', 'Đăng ký gói 1 Năm')}</span>
              <ArrowRight size={16} />
            </a>
          </div>
        </div>
      </div>
    </section>
  );
}

export default PricingSection;
