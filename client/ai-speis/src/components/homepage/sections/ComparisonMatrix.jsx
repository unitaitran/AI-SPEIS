import { Check, Sparkles } from 'lucide-react';

function ComparisonMatrix({ t }) {
  const rawHeaders = t('comparison.headers', { returnObjects: true });
  const headers = Array.isArray(rawHeaders) ? rawHeaders : ['Tiêu chí', 'Pramp / Peer-to-Peer', 'Mentor / Human Mock', 'AI-SPEIS Platform'];

  const defaultRows = [
    { feature: 'Thời gian & Tính sẵn sàng', p2p: 'Phụ thuộc lịch người khác', mentor: 'Phải đặt lịch trước nhiều ngày', aispeis: 'Luyện tức thì 24/7' },
    { feature: 'Cá nhân hóa theo CV & JD', p2p: 'Không (Đề cố định)', mentor: 'Tùy thuộc Mentor', aispeis: 'Cá nhân hóa 100% bằng AI' },
    { feature: 'Bộ câu hỏi & Môi trường', p2p: 'Chỉ có Coding hoặc Behavioral', mentor: 'Không cố định', aispeis: 'Trọn vẹn Behavioral + Tech + Coding' },
    { feature: 'Tiêu chuẩn đánh giá', p2p: 'Nhận xét tự do của bạn tập', mentor: 'Cảm tính từng cá nhân', aispeis: 'Rubric Tuyển dụng Doanh nghiệp' },
    { feature: 'Chi phí duy trì', p2p: 'Mất công phỏng vấn lại cho bạn', mentor: 'Rất đắt ($50 - $150/buổi)', aispeis: 'Miễn phí + Gói Pro tiết kiệm' }
  ];

  const rawRows = t('comparison.rows', { returnObjects: true });
  const rows = Array.isArray(rawRows) ? rawRows : defaultRows;

  return (
    <section className="home-section home-comparison-section" id="comparison">
      <div className="home-section-shell">
        <div className="home-section-heading text-center mx-auto">
          <span className="home-kicker">
            <Sparkles size={14} className="mr-1" />
            {t('comparison.badge', 'SO SÁNH VƯỢT TRỘI')}
          </span>
          <h2>{t('comparison.title', 'Vì sao AI-SPEIS là lựa chọn tối ưu cho bạn?')}</h2>
          <p>{t('comparison.subtitle', 'So sánh sự hiệu quả giữa AI-SPEIS và các phương pháp phỏng vấn truyền thống.')}</p>
        </div>

        {/* COMPARISON TABLE */}
        <div className="comparison-table-wrapper">
          <table className="comparison-table">
            <thead>
              <tr>
                <th className="col-feature">{headers[0]}</th>
                <th className="col-p2p">{headers[1]}</th>
                <th className="col-mentor">{headers[2]}</th>
                <th className="col-aispeis highlight-header">
                  <div className="aispeis-brand-head">
                    <Sparkles size={16} />
                    <span>{headers[3]}</span>
                  </div>
                </th>
              </tr>
            </thead>
            <tbody>
              {Array.isArray(rows) && rows.map((row, idx) => (
                <tr key={idx}>
                  <td className="col-feature font-semibold">{row.feature}</td>
                  <td className="col-p2p muted-cell">
                    <span className="cell-text">{row.p2p}</span>
                  </td>
                  <td className="col-mentor muted-cell">
                    <span className="cell-text">{row.mentor}</span>
                  </td>
                  <td className="col-aispeis highlight-cell font-bold">
                    <div className="flex items-center gap-2">
                      <Check size={18} className="text-success flex-shrink-0" />
                      <span>{row.aispeis}</span>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}

export default ComparisonMatrix;
