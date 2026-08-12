function isVietnamese(language) {
  return String(language || '').toLowerCase().startsWith('vi');
}

function roundName(roundType, language) {
  const names = isVietnamese(language)
    ? { BEHAVIORAL: 'Hành vi', TECHNICAL: 'Kỹ thuật', CODING: 'Lập trình', BEHAVIOR: 'Hành vi', CODE: 'Lập trình' }
    : { BEHAVIORAL: 'Behavioral', TECHNICAL: 'Technical', CODING: 'Coding', BEHAVIOR: 'Behavioral', CODE: 'Coding' };
  return names[String(roundType || '').toUpperCase()] || (isVietnamese(language) ? 'phỏng vấn' : 'interview');
}

function formatDate(value, language, includeTime = false) {
  if (!value) return '';
  const source = String(value);
  const date = new Date(/^\d{4}-\d{2}-\d{2}T.*(?:Z|[+-]\d{2}:?\d{2})$/i.test(source) ? source : `${source}Z`);
  if (Number.isNaN(date.getTime())) return '';
  return new Intl.DateTimeFormat(isVietnamese(language) ? 'vi-VN' : 'en-US', includeTime
    ? { dateStyle: 'medium', timeStyle: 'short' }
    : { day: 'numeric', month: 'short', year: 'numeric' }).format(date);
}

const copy = {
  INTERVIEW_SESSION_READY: {
    en: ['Your interview is ready', 'Your {{round}} Interview is ready to begin.', 'Start interview'],
    vi: ['Buổi phỏng vấn đã sẵn sàng', 'Vòng phỏng vấn {{round}} của bạn đã sẵn sàng để bắt đầu.', 'Bắt đầu phỏng vấn'],
  },
  INTERVIEW_SESSION_INTERRUPTED: {
    en: ['Interview session interrupted', 'Your interview is incomplete. You can continue from your last saved progress.', 'Resume interview'],
    vi: ['Phiên phỏng vấn bị gián đoạn', 'Buổi phỏng vấn chưa hoàn tất. Bạn có thể tiếp tục từ tiến trình đã lưu.', 'Tiếp tục phỏng vấn'],
  },
  INTERVIEW_SESSION_EXPIRED: {
    en: ['Interview session expired', 'Your interview session has expired and can no longer be resumed.', 'View interview status'],
    vi: ['Phiên phỏng vấn đã hết hạn', 'Phiên phỏng vấn đã hết hạn và không thể tiếp tục.', 'Xem trạng thái phỏng vấn'],
  },
  INTERVIEW_ROUND_COMPLETED: {
    en: ['{{round}} Interview completed', 'Your {{round}} Interview has been completed successfully.', 'View progress'],
    vi: ['Đã hoàn thành phỏng vấn {{round}}', 'Vòng phỏng vấn {{round}} của bạn đã hoàn thành thành công.', 'Xem tiến trình'],
  },
  ALL_INTERVIEW_ROUNDS_COMPLETED: {
    en: ['Interview completed', 'You have completed all required interview rounds{{pointsText}}', 'View interview summary'],
    vi: ['Đã hoàn thành phỏng vấn', 'Bạn đã hoàn thành tất cả các vòng phỏng vấn{{pointsText}}', 'Xem tổng kết phỏng vấn'],
  },
  INTERVIEW_FEEDBACK_READY: {
    en: ['Interview feedback available', 'Your interview result and feedback are now available.', 'View feedback'],
    vi: ['Đã có phản hồi phỏng vấn', 'Kết quả và phản hồi phỏng vấn của bạn hiện đã sẵn sàng.', 'Xem phản hồi'],
  },
  PROFILE_INFORMATION_REQUIRED: {
    en: ['Profile information required', 'Additional profile information is required before your interview can be prepared.', 'Update profile'],
    vi: ['Cần bổ sung thông tin hồ sơ', 'Cần bổ sung thông tin hồ sơ trước khi hệ thống có thể chuẩn bị phỏng vấn.', 'Cập nhật hồ sơ'],
  },
  CV_PROCESSING_FAILED: {
    en: ['CV processing failed', 'We could not process your CV. Please review the file and upload it again.', 'Upload CV again'],
    vi: ['Xử lý CV thất bại', 'Hệ thống không thể xử lý CV của bạn. Vui lòng kiểm tra và tải lại tệp.', 'Tải lại CV'],
  },
  JD_INFORMATION_REQUIRED: {
    en: ['Job description required', 'A valid job description is required before your interview can be prepared.', 'Add job description'],
    vi: ['Cần có mô tả công việc', 'Cần có mô tả công việc hợp lệ trước khi hệ thống có thể chuẩn bị phỏng vấn.', 'Thêm mô tả công việc'],
  },
  JD_PROCESSING_FAILED: {
    en: ['Job description processing failed', 'We could not process the job description. Please review it and try again.', 'Update job description'],
    vi: ['Xử lý mô tả công việc thất bại', 'Hệ thống không thể xử lý mô tả công việc. Vui lòng kiểm tra và thử lại.', 'Cập nhật mô tả công việc'],
  },
  SUBSCRIPTION_ACTIVATED: {
    en: ['Subscription activated', 'Your {{plan}} subscription is now active.', 'View subscription'],
    vi: ['Gói đăng ký đã được kích hoạt', 'Gói {{plan}} của bạn hiện đang hoạt động.', 'Xem gói đăng ký'],
  },
  SUBSCRIPTION_EXPIRING_SOON: {
    en: ['Subscription expiring soon', 'Your {{plan}} subscription will expire on {{expiryDate}}.', 'Renew subscription'],
    vi: ['Gói đăng ký sắp hết hạn', 'Gói {{plan}} của bạn sẽ hết hạn vào {{expiryDate}}.', 'Gia hạn gói đăng ký'],
  },
  SUBSCRIPTION_EXPIRED: {
    en: ['Subscription expired', 'Your subscription has expired. Some AI-SPEIS features may no longer be available.', 'View plans'],
    vi: ['Gói đăng ký đã hết hạn', 'Gói đăng ký của bạn đã hết hạn. Một số tính năng AI-SPEIS có thể không còn khả dụng.', 'Xem các gói'],
  },
  SUBSCRIPTION_PAYMENT_FAILED: {
    en: ['Subscription payment failed', 'We could not renew your subscription. Please review your payment information.', 'Review payment'],
    vi: ['Thanh toán gói đăng ký thất bại', 'Hệ thống không thể gia hạn gói. Vui lòng kiểm tra thông tin thanh toán.', 'Kiểm tra thanh toán'],
  },
  SUBSCRIPTION_CANCELLED: {
    en: ['Subscription cancelled', 'Your subscription will not renew automatically.', 'View subscription'],
    vi: ['Đã hủy gói đăng ký', 'Gói đăng ký của bạn sẽ không tự động gia hạn.', 'Xem gói đăng ký'],
  },
  SUBSCRIPTION_PLAN_CHANGED: {
    en: ['Subscription plan changed', 'Your subscription has been changed from {{oldPlan}} to {{plan}}.', 'View subscription'],
    vi: ['Đã thay đổi gói đăng ký', 'Gói đăng ký của bạn đã được đổi từ {{oldPlan}} sang {{plan}}.', 'Xem gói đăng ký'],
  },
  SUBSCRIPTION_USAGE_LIMIT_REACHED: {
    en: ['Subscription limit reached', 'You have reached the usage limit for your current subscription plan.', 'Upgrade plan'],
    vi: ['Đã đạt giới hạn sử dụng', 'Bạn đã sử dụng hết giới hạn của gói đăng ký hiện tại.', 'Nâng cấp gói'],
  },
  AI_EVALUATION_REQUIRES_REVIEW: {
    en: ['AI evaluation requires review', 'The interview evaluation for {{user}} contains results that require verification.', 'Review evaluation'],
    vi: ['Cần kiểm tra đánh giá AI', 'Đánh giá phỏng vấn của {{user}} có kết quả cần được xác minh.', 'Kiểm tra đánh giá'],
  },
  AI_EVALUATION_FAILED: {
    en: ['AI evaluation failed', 'The interview evaluation for {{user}} could not be completed.', 'Review evaluation issue'],
    vi: ['Đánh giá AI thất bại', 'Không thể hoàn tất đánh giá phỏng vấn của {{user}}.', 'Xem sự cố đánh giá'],
  },
  FINAL_FEEDBACK_FAILED: {
    en: ['Final feedback generation failed', 'Final feedback could not be generated for {{user}}\'s interview.', 'Retry feedback'],
    vi: ['Tạo phản hồi cuối cùng thất bại', 'Không thể tạo phản hồi cuối cùng cho buổi phỏng vấn của {{user}}.', 'Thử lại phản hồi'],
  },
  SYSTEM_SERVICE_UNAVAILABLE: {
    en: ['System service unavailable', 'A required AI-SPEIS service is unavailable and requires attention.', 'View system status'],
    vi: ['Dịch vụ hệ thống không khả dụng', 'Một dịch vụ cần thiết của AI-SPEIS hiện không khả dụng và cần được xử lý.', 'Xem trạng thái hệ thống'],
  },
  SUBSCRIPTION_PAYMENT_REQUIRES_REVIEW: {
    en: ['Subscription payment requires review', 'A subscription payment for {{user}} requires verification.', 'Review payment'],
    vi: ['Cần kiểm tra thanh toán gói', 'Một thanh toán gói đăng ký của {{user}} cần được xác minh.', 'Kiểm tra thanh toán'],
  },
  SUBSCRIPTION_ACTIVATION_FAILED: {
    en: ['Subscription activation failed', 'The subscription for {{user}} could not be activated after payment confirmation.', 'Review subscription'],
    vi: ['Kích hoạt gói đăng ký thất bại', 'Không thể kích hoạt gói của {{user}} sau khi thanh toán được xác nhận.', 'Kiểm tra gói đăng ký'],
  },
  SUBSCRIPTION_DATA_INCONSISTENT: {
    en: ['Subscription data inconsistent', 'A subscription record is inconsistent with the payment provider.', 'Review subscription'],
    vi: ['Dữ liệu gói đăng ký không nhất quán', 'Dữ liệu gói đăng ký không khớp với nhà cung cấp thanh toán.', 'Kiểm tra gói đăng ký'],
  },
  CV_UPLOADED: {
    en: ['CV uploaded', 'Your CV was uploaded successfully and is ready to be processed.', 'View CV'],
    vi: ['Đã tải CV lên', 'CV của bạn đã được tải lên thành công và sẵn sàng để xử lý.', 'Xem CV'],
  },
  CV_PROCESSING_COMPLETED: {
    en: ['CV processing completed', 'Your CV has been processed successfully. Please review and confirm the extracted information.', 'Review CV'],
    vi: ['Đã xử lý CV', 'CV của bạn đã được xử lý thành công. Vui lòng xem lại và xác nhận thông tin trích xuất.', 'Xem lại CV'],
  },
  JD_UPLOADED: {
    en: ['Job description uploaded', 'Your job description was uploaded successfully and is ready to be processed.', 'View job description'],
    vi: ['Đã tải mô tả công việc lên', 'Mô tả công việc của bạn đã được tải lên thành công và sẵn sàng để xử lý.', 'Xem mô tả công việc'],
  },
  JD_PROCESSING_COMPLETED: {
    en: ['Job description processing completed', 'Your job description has been processed successfully. Please review the extracted information.', 'Review job description'],
    vi: ['Đã xử lý mô tả công việc', 'Mô tả công việc của bạn đã được xử lý thành công. Vui lòng xem lại thông tin trích xuất.', 'Xem lại mô tả công việc'],
  },
  PROFILE_UPDATED: {
    en: ['Profile updated', 'Your personal information has been updated successfully.', 'View profile'],
    vi: ['Đã cập nhật hồ sơ', 'Thông tin cá nhân của bạn đã được cập nhật thành công.', 'Xem hồ sơ'],
  },
  SUBSCRIPTION_PAYMENT_SUCCEEDED: {
    en: ['Subscription payment received', 'A subscription payment for {{user}} was completed successfully.', 'Review payment'],
    vi: ['Đã nhận thanh toán gói đăng ký', 'Thanh toán gói đăng ký của {{user}} đã hoàn tất thành công.', 'Kiểm tra thanh toán'],
  },
  WEEKLY_SYSTEM_STATISTICS: {
    en: ['Weekly system statistics', 'Your weekly AI-SPEIS statistics summary is now available.', 'View dashboard'],
    vi: ['Thống kê hệ thống hằng tuần', 'Báo cáo thống kê AI-SPEIS hằng tuần đã sẵn sàng.', 'Xem dashboard'],
  },
};

function interpolate(template, values) {
  return template.replace(/{{(\w+)}}/g, (_, key) => values[key] || '');
}

export function getNotificationContent(notification, metadata, language) {
  const locale = isVietnamese(language) ? 'vi' : 'en';
  const definition = copy[notification?.type]?.[locale];
  if (!definition) return { title: notification?.title || (locale === 'vi' ? 'Thông báo' : 'Notification'), message: notification?.message || '', action: null };

  const points = metadata?.earnedPoints ?? metadata?.points;
  const pointsText = points
    ? (locale === 'vi' ? ` và nhận được +${points} điểm thưởng!` : ` and earned +${points} reward points!`)
    : '.';

  const values = {
    round: roundName(metadata?.roundType, language),
    plan: metadata?.planName || metadata?.newPlanName || (locale === 'vi' ? 'hiện tại' : 'current'),
    oldPlan: metadata?.oldPlanName || (locale === 'vi' ? 'gói trước' : 'previous plan'),
    expiryDate: formatDate(metadata?.expiryDate || notification?.expiresAt, language),
    user: metadata?.userName || (locale === 'vi' ? 'người dùng' : 'the user'),
    pointsText,
  };
  return { title: interpolate(definition[0], values), message: interpolate(definition[1], values), action: definition[2] };
}

export function getLocalizedStatus(status, language) {
  const values = isVietnamese(language)
    ? { COMPLETED: 'Đã hoàn tất', EXPIRED: 'Đã hết hạn', CANCELLED: 'Đã hủy' }
    : { COMPLETED: 'Completed', EXPIRED: 'Expired', CANCELLED: 'Cancelled' };
  return values[status] || null;
}

export function getLocalizedServiceName(service, language) {
  const en = { AI_MODEL: 'AI model', EXTERNAL_AI_API: 'External AI API', STT: 'Speech-to-text', TTS: 'Text-to-speech', RAG: 'Knowledge retrieval', CODING_JUDGE: 'Coding judge', BACKGROUND_JOB: 'Background job', NOTIFICATION_SERVICE: 'Notification service', PAYMENT_SERVICE: 'Payment service' };
  const vi = { AI_MODEL: 'Mô hình AI', EXTERNAL_AI_API: 'API AI bên ngoài', STT: 'Chuyển giọng nói thành văn bản', TTS: 'Chuyển văn bản thành giọng nói', RAG: 'Truy xuất tri thức', CODING_JUDGE: 'Bộ chấm mã nguồn', BACKGROUND_JOB: 'Tác vụ nền', NOTIFICATION_SERVICE: 'Dịch vụ thông báo', PAYMENT_SERVICE: 'Dịch vụ thanh toán' };
  return (isVietnamese(language) ? vi : en)[String(service || '').toUpperCase()] || null;
}

export function getLocalizedCategory(category, language) {
  const vi = { INTERVIEW: 'Phỏng vấn', FEEDBACK: 'Phản hồi', PROFILE: 'Hồ sơ', SUBSCRIPTION: 'Gói đăng ký', AI_EVALUATION: 'Đánh giá AI', SYSTEM: 'Hệ thống' };
  return isVietnamese(language) ? (vi[category] || category) : String(category || '').replace('_', ' ');
}

export function getNotificationUiCopy(language) {
  if (isVietnamese(language)) {
    return {
      center: 'Trung tâm thông báo', notifications: 'Thông báo', unread: 'thông báo chưa đọc', caughtUp: 'Bạn đã xem hết thông báo mới.', markAllRead: 'Đánh dấu tất cả đã đọc', show: 'Hiển thị', all: 'Tất cả', unreadFilter: 'Chưa đọc', category: 'Danh mục', allCategories: 'Tất cả danh mục', loadFailed: 'Không thể tải thông báo', retry: 'Thử lại', newUpdates: 'Các cập nhật mới sẽ xuất hiện ở đây.', noUser: 'Bạn chưa có thông báo nào.', noAdmin: 'Không có thông báo hệ thống cần xử lý.', reviewHint: 'Khi có nội dung cần xem xét, thông báo sẽ xuất hiện ở đây.', loading: 'Đang tải', loadMore: 'Tải thêm', viewAll: 'Xem tất cả thông báo', dialog: 'Thông báo',
    };
  }
  return {
    center: 'Notification Center', notifications: 'Notifications', unread: 'unread notification', caughtUp: 'You are all caught up.', markAllRead: 'Mark all as read', show: 'Show', all: 'All', unreadFilter: 'Unread', category: 'Category', allCategories: 'All categories', loadFailed: 'Notifications could not load', retry: 'Retry', newUpdates: 'New updates will appear here.', noUser: 'You have no notifications yet.', noAdmin: 'There are no system notifications requiring attention.', reviewHint: 'When there is something to review, it will appear here.', loading: 'Loading', loadMore: 'Load more', viewAll: 'View all notifications', dialog: 'Notifications',
  };
}
