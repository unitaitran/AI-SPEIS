import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Upload,
  FileText,
  CheckCircle2,
  AlertCircle,
  Loader2,
  Eye,
  Plus,
  Award,
  Briefcase,
  FolderGit2,
  Trash2,
  ChevronRight,
  Info,
  Sparkles
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { ENDPOINTS } from '../../config/api';

function MyCVPage() {
  const { t } = useTranslation('dashboard');
  const [cvUploaded, setCvUploaded] = useState(false);
  const [fileName, setFileName] = useState('');
  const [cvFileId, setCvFileId] = useState(null);
  const [uploadDate, setUploadDate] = useState('');
  const [isLoading, setIsLoading] = useState(true);

  const [uploadProgress, setUploadProgress] = useState(0);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadStep, setUploadStep] = useState('');
  const [remainingEvaluations, setRemainingEvaluations] = useState(4);
  const [showFeedbackModal, setShowFeedbackModal] = useState(false);
  const [activeTab, setActiveTab] = useState('overall'); // overall, strengths, improvements

  // Mock extracted CV data
  const cvData = {
    uploadDate: '16/06/2026',
    skills: ['ReactJS', 'TypeScript', 'Tailwind CSS', 'Git', 'Redux', 'Next.js', 'REST API', 'UI/UX Design'],
    projects: [
      {
        name: 'E-commerce Platform',
        role: 'Frontend Developer',
        desc: 'Xây dựng trang web thương mại điện tử tích hợp thanh toán, quản lý giỏ hàng.'
      },
      {
        name: 'Portfolio Website',
        role: 'Personal Project',
        desc: 'Trang giới thiệu bản thân thiết kế theo phong cách tối giản, sử dụng Next.js và Tailwind.'
      },
      {
        name: 'Task Management App',
        role: 'Team Lead / Frontend',
        desc: 'Ứng dụng quản lý công việc nhóm thời gian thực với drag-and-drop.'
      }
    ],
    experience: [
      {
        role: 'Frontend Developer Intern',
        company: 'Company X',
        duration: '06/2025 - 09/2025',
        bullets: ['Học hỏi quy trình làm việc Agile/Scrum.', 'Tối ưu hóa UI/UX và cải thiện tốc độ tải trang thêm 20%.']
      },
      {
        role: 'Freelance Web Developer',
        company: 'Tự do',
        duration: '2024 - Hiện tại',
        bullets: ['Thiết kế và lập trình landing page cho các doanh nghiệp vừa và nhỏ.']
      }
    ]
  };

  useEffect(() => {
    const fetchMyCV = async () => {
      setIsLoading(true);
      try {
        const token = localStorage.getItem('token');
        if (!token) {
          setIsLoading(false);
          return;
        }

        const response = await fetch(ENDPOINTS.CV_GET_MY, {
          headers: {
            'Authorization': `Bearer ${token}`
          }
        });

        if (response.ok) {
          const data = await response.json();
          setCvUploaded(true);
          setFileName(data.fileName);
          setCvFileId(data.cvFileId);
          const date = new Date(data.uploadedAt);
          const formattedDate = date.toLocaleDateString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit'
          });
          setUploadDate(formattedDate);
        } else if (response.status === 404) {
          setCvUploaded(false);
          setFileName('');
          setCvFileId(null);
        }
      } catch (error) {
        console.error('Lỗi khi tải thông tin CV:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchMyCV();
  }, []);

  const handleFileUpload = async (e) => {
    const file = e.target.files[0];
    if (file) {
      if (!file.name.toLowerCase().endsWith('.pdf')) {
        alert(t('mycv.error_pdf_only', 'Chỉ hỗ trợ tệp tin định dạng PDF'));
        return;
      }

      setIsUploading(true);
      setUploadProgress(0);
      setUploadStep(t('mycv.step_preparing', 'Đang chuẩn bị tệp tin...'));

      let apiFinished = false;
      let apiResponseData = null;
      let apiError = null;

      // Start the upload in the background
      (async () => {
        try {
          const token = localStorage.getItem('token');
          const formData = new FormData();
          formData.append('file', file);

          const response = await fetch(ENDPOINTS.CV_UPLOAD, {
            method: 'POST',
            headers: {
              'Authorization': `Bearer ${token}`
            },
            body: formData
          });

          if (!response.ok) {
            const errData = await response.json().catch(() => ({}));
            throw new Error(errData.message || 'Lỗi tải lên CV');
          }

          const data = await response.json();
          apiResponseData = data;
        } catch (error) {
          apiError = error.message || 'Không thể tải lên file CV. Vui lòng thử lại.';
        } finally {
          apiFinished = true;
        }
      })();

      const interval = setInterval(() => {
        setUploadProgress((prev) => {
          let next = prev;
          if (prev < 90) {
            next = prev + 5;
          } else if (apiFinished) {
            next = prev + 10;
          }

          if (next >= 100) {
            clearInterval(interval);
            setTimeout(() => {
              setIsUploading(false);
              if (apiError) {
                alert(apiError);
                setCvUploaded(false);
              } else if (apiResponseData) {
                setCvUploaded(true);
                setFileName(apiResponseData.fileName);
                setCvFileId(apiResponseData.cvFileId);
                const date = new Date(apiResponseData.uploadedAt);
                const formattedDate = date.toLocaleDateString('vi-VN', {
                  year: 'numeric',
                  month: '2-digit',
                  day: '2-digit'
                });
                setUploadDate(formattedDate);
                setRemainingEvaluations((prevEval) => Math.max(0, prevEval - 1));
              }
            }, 600);
            return 100;
          }

          // Update helper step texts
          if (next === 20) setUploadStep(t('mycv.step_uploading', 'Đang tải tệp tin lên máy chủ...'));
          if (next === 40) setUploadStep(t('mycv.step_analyzing', 'Đang phân tích cấu trúc CV...'));
          if (next === 60) setUploadStep(t('mycv.step_extracting_skills', 'AI đang trích xuất thông tin kỹ năng...'));
          if (next === 80) setUploadStep(t('mycv.step_matching_exp', 'Đang đối chiếu dự án và kinh nghiệm...'));
          if (next === 95) setUploadStep(t('mycv.step_completed', 'Hoàn tất phân tích!'));

          return next;
        });
      }, 150);
    }
  };

  const handleRemoveCV = async () => {
    if (!cvFileId) {
      alert(t('mycv.error_find_cv', 'Không tìm thấy thông tin tệp CV cần xóa.'));
      return;
    }
    if (window.confirm(t('mycv.confirm_delete', 'Bạn có chắc chắn muốn xóa CV này?'))) {
      try {
        const token = localStorage.getItem('token');
        const response = await fetch(ENDPOINTS.CV_DELETE(cvFileId), {
          method: 'DELETE',
          headers: {
            'Authorization': `Bearer ${token}`
          }
        });

        if (response.ok) {
          setCvUploaded(false);
          setFileName('');
          setCvFileId(null);
          setUploadDate('');
        } else {
          const errData = await response.json().catch(() => ({}));
          alert(errData.message || t('mycv.error_remove', 'Lỗi khi xóa CV trên máy chủ.'));
        }
      } catch (error) {
        console.error('Lỗi khi kết nối xóa CV:', error);
        alert(t('mycv.error_remove_connect', 'Lỗi kết nối khi xóa CV. Vui lòng thử lại.'));
      }
    }
  };

  if (isLoading) {
    return (
      <UserLayout>
        <div className="flex flex-col items-center justify-center min-h-[400px] space-y-4">
          <Loader2 size={36} className="text-primary animate-spin" />
          <p className="text-sm text-text-secondary">{t('mycv.loading', 'Đang tải thông tin CV của bạn...')}</p>
        </div>
      </UserLayout>
    );
  }

  return (
    <UserLayout>
      <div className="space-y-8 pb-12">
        {/* Page Header */}
        <section className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-3xl font-bold text-text-primary tracking-tight mb-1">{t('mycv.title', 'CV của tôi')}</h1>
            <p className="text-base text-text-secondary">
              {t('mycv.subtitle', 'Quản lý CV để AI phân tích kỹ năng, dự án và tạo câu hỏi phỏng vấn cá nhân hóa.')}
            </p>
          </div>
        </section>

        {isUploading ? (
          /* Loading / Analyzing State */
          <div className="bg-surface-2 border border-primary-light rounded-2xl p-10 text-center shadow-sm max-w-2xl mx-auto animate-pulse-slight">
            <div className="flex justify-center mb-6 relative">
              <Loader2 size={48} className="text-primary animate-spin" />
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2">
                <Sparkles size={20} className="text-primary-dark animate-pulse" />
              </div>
            </div>
            <h3 className="text-xl font-bold text-text-primary mb-2">{t('mycv.analyzing_title', 'AI đang phân tích CV của bạn')}</h3>
            <p className="text-sm text-text-secondary mb-6">{uploadStep}</p>

            {/* Progress Bar */}
            <div className="w-full bg-surface-3 rounded-full h-3.5 mb-2 overflow-hidden border border-border">
              <div
                className="bg-gradient-to-r from-primary to-[#4A90E2] h-full rounded-full transition-all duration-150"
                style={{ width: `${uploadProgress}%` }}
              ></div>
            </div>
            <div className="text-xs font-semibold text-primary-dark">{uploadProgress}%</div>
          </div>
        ) : !cvUploaded ? (
          /* Empty / Upload CV State with split columns layout */
          <div className="max-w-5xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-8 items-center">
            {/* Left Column: Mascot */}
            <div className="flex flex-col items-center justify-center p-6 text-center space-y-6">
              {/* Speech Bubble */}
              <div className="relative bg-white border border-border rounded-2xl px-6 py-4 shadow-sm max-w-sm mb-2">
                <p className="text-base font-semibold text-text-primary">
                  {t('mycv.mascot_say', 'Hãy tải lên CV của bạn!')}
                </p>
                {/* Speech Bubble Arrow */}
                <div className="absolute bottom-[-8px] left-1/2 -translate-x-1/2 w-4 h-4 bg-white border-r border-b border-border rotate-45"></div>
              </div>
              
              {/* Mascot Image Wrapper */}
              <div className="w-56 h-56 md:w-64 md:h-64 flex items-center justify-center bg-gradient-to-tr from-primary-xlight to-white rounded-full p-4 border border-primary-light shadow-[0_8px_30px_rgb(111,182,232,0.15)] transition-transform hover:scale-105 duration-300">
                <img
                  src="/teaching_mascot.jpg"
                  alt="Teaching Mascot"
                  className="w-full h-full object-contain"
                />
              </div>
            </div>

            {/* Right Column: Upload Box and Badge */}
            <div className="space-y-6">
              <div className="bg-surface-2 border border-dashed border-border-strong hover:border-primary rounded-2xl p-10 text-center shadow-sm transition-all duration-300 relative group flex flex-col items-center justify-center min-h-[300px]">
                <div className="w-16 h-16 bg-primary-xlight rounded-full flex items-center justify-center mb-5 group-hover:scale-110 transition-transform duration-300">
                  <Upload size={28} className="text-primary-dark" />
                </div>
                <h3 className="text-lg font-bold text-text-primary mb-2">
                  {t('mycv.upload_title', 'Tải lên CV của bạn')}
                </h3>
                <p className="text-sm text-text-secondary max-w-md mb-6 leading-relaxed">
                  {t('mycv.upload_desc', 'Bạn chưa tải lên CV của mình. Hãy tải lên CV để AI có thể phân tích kỹ năng, trích xuất thông tin dự án và cá nhân hóa câu hỏi phỏng vấn tối ưu nhất cho bạn.')}
                </p>

                {/* Upload Input & Button */}
                <label className="cursor-pointer bg-primary hover:bg-primary-dark text-white text-sm font-semibold py-3 px-6 rounded-xl transition-all shadow-md hover:shadow-lg inline-flex items-center gap-2">
                  <Plus size={18} />
                  {t('mycv.select_file', 'Chọn tệp tin CV (PDF)')}
                  <input
                    type="file"
                    className="hidden"
                    accept=".pdf"
                    onChange={handleFileUpload}
                  />
                </label>

                <p className="text-xs text-text-disabled mt-3">
                  {t('mycv.file_hint', 'Hỗ trợ định dạng PDF tối đa 5MB')}
                </p>
              </div>

              {/* Remaining evaluations badge */}
              <div className="flex justify-center">
                <div className="inline-flex items-center bg-primary-xlight px-4 py-2 rounded-xl border border-primary-light text-sm font-bold text-primary-dark shadow-[0_2px_8px_rgba(111,182,232,0.08)]">
                  <Info size={16} className="mr-2 shrink-0" />
                  {t('mycv.remaining_evaluations', 'Số lượt đánh giá CV còn lại: {{count}} lượt', { count: remainingEvaluations })}
                </div>
              </div>
            </div>
          </div>
        ) : (
          /* Active CV State */
          <div className="space-y-6">
            {/* Current CV Info Card */}
            <div className="bg-surface-2 border border-border rounded-2xl p-6 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-6 relative overflow-hidden">
              {/* Decorative side accent */}
              <div className="absolute left-0 top-0 bottom-0 w-1.5 bg-primary"></div>

              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-primary-xlight rounded-xl flex items-center justify-center shrink-0">
                  <FileText size={24} className="text-primary-dark" />
                </div>
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-base font-bold text-text-primary break-all">{fileName}</h3>
                    <span className="inline-flex items-center px-2 py-0.5 rounded-full bg-success-light text-success text-[10px] font-bold border border-success/20">
                      <CheckCircle2 size={10} className="mr-1" /> {t('mycv.analyzed', 'Đã phân tích')}
                    </span>
                  </div>
                  <p className="text-xs text-text-secondary">{t('mycv.uploaded_on', 'Ngày tải lên: {{date}}', { date: uploadDate || cvData.uploadDate })}</p>

                  {/* Quick info list */}
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 pt-2 text-xs font-medium text-text-secondary">
                    <span className="flex items-center gap-1">
                      <Award size={14} className="text-primary-dark" />
                      {t('mycv.skills_detected', '{{count}} kỹ năng phát hiện', { count: cvData.skills.length })}
                    </span>
                    <span className="flex items-center gap-1">
                      <FolderGit2 size={14} className="text-primary-dark" />
                      {t('mycv.projects_detected', '{{count}} project phát hiện', { count: cvData.projects.length })}
                    </span>
                  </div>
                </div>
              </div>

              {/* Actions */}
              <div className="flex items-center flex-wrap gap-3 self-start md:self-auto shrink-0">
                <button
                  onClick={() => setShowFeedbackModal(true)}
                  className="bg-black hover:bg-gray-800 text-white text-xs font-semibold py-2.5 px-4 rounded-xl transition-all flex items-center gap-1.5 shadow-sm"
                >
                  <Eye size={14} />
                  {t('mycv.view_feedback', 'Xem feedback CV')}
                </button>

                <label className="cursor-pointer bg-white hover:bg-surface-3 text-text-primary border border-border text-xs font-semibold py-2.5 px-4 rounded-xl transition-all flex items-center gap-1.5 shadow-sm">
                  <Upload size={14} />
                  {t('mycv.upload_new', 'Tải CV mới')}
                  <input
                    type="file"
                    className="hidden"
                    accept=".pdf"
                    onChange={handleFileUpload}
                  />
                </label>

                <button
                  onClick={handleRemoveCV}
                  className="p-2.5 text-text-secondary hover:text-error hover:bg-error-light rounded-xl border border-transparent hover:border-error/20 transition-all"
                  title={t('mycv.delete_cv', 'Xóa CV')}
                >
                  <Trash2 size={16} />
                </button>
              </div>
            </div>

            {/* Extracted Details Title */}
            <div className="flex items-center gap-2 pt-2">
              <Sparkles size={20} className="text-primary-dark" />
              <h2 className="text-lg font-bold text-text-primary">{t('mycv.extracted_info', 'Thông tin AI trích xuất')}</h2>
            </div>

            {/* Metadata Grid */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Skills Column */}
              <div className="bg-surface-2 border border-border rounded-2xl shadow-sm flex flex-col overflow-hidden">
                <div className="px-5 py-4 border-b border-border bg-surface-1 flex items-center gap-2 shrink-0">
                  <Award size={18} className="text-primary-dark" />
                  <h3 className="text-sm font-bold text-text-primary uppercase tracking-wider">{t('mycv.skills', 'Kỹ năng')}</h3>
                </div>
                <div className="p-5 flex-1 overflow-y-auto max-h-[350px]">
                  <div className="flex flex-wrap gap-2">
                    {cvData.skills.map((skill, idx) => (
                      <span
                        key={idx}
                        className="inline-flex items-center px-3 py-1.5 rounded-lg bg-primary-xlight text-primary-dark border border-primary-light text-xs font-semibold hover:bg-primary hover:text-white hover:border-primary transition-all duration-200 cursor-default"
                      >
                        {skill}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              {/* Projects Column */}
              <div className="bg-surface-2 border border-border rounded-2xl shadow-sm flex flex-col overflow-hidden">
                <div className="px-5 py-4 border-b border-border bg-surface-1 flex items-center gap-2 shrink-0">
                  <FolderGit2 size={18} className="text-primary-dark" />
                  <h3 className="text-sm font-bold text-text-primary uppercase tracking-wider">{t('mycv.projects', 'Dự án')}</h3>
                </div>
                <div className="p-5 flex-1 overflow-y-auto max-h-[350px] space-y-4">
                  {cvData.projects.map((proj, idx) => (
                    <div key={idx} className="p-3 bg-surface-1 border border-border rounded-xl hover:border-primary-light hover:shadow-[0_2px_8px_rgba(111,182,232,0.05)] transition-all">
                      <div className="text-xs font-bold text-primary-dark uppercase mb-0.5">{proj.role}</div>
                      <h4 className="text-sm font-bold text-text-primary mb-1">{proj.name}</h4>
                      <p className="text-xs text-text-secondary leading-relaxed">{proj.desc}</p>
                    </div>
                  ))}
                </div>
              </div>

              {/* Experience Column */}
              <div className="bg-surface-2 border border-border rounded-2xl shadow-sm flex flex-col overflow-hidden">
                <div className="px-5 py-4 border-b border-border bg-surface-1 flex items-center gap-2 shrink-0">
                  <Briefcase size={18} className="text-primary-dark" />
                  <h3 className="text-sm font-bold text-text-primary uppercase tracking-wider">{t('mycv.experience', 'Kinh nghiệm')}</h3>
                </div>
                <div className="p-5 flex-1 overflow-y-auto max-h-[350px] space-y-4">
                  {cvData.experience.map((exp, idx) => (
                    <div key={idx} className="p-3 bg-surface-1 border border-border rounded-xl hover:border-primary-light hover:shadow-[0_2px_8px_rgba(111,182,232,0.05)] transition-all">
                      <div className="flex justify-between items-start mb-1.5">
                        <div>
                          <h4 className="text-sm font-bold text-text-primary leading-tight">{exp.role}</h4>
                          <span className="text-xs font-semibold text-text-secondary">{exp.company}</span>
                        </div>
                        <span className="text-[10px] font-bold text-text-disabled shrink-0 bg-white px-2 py-0.5 rounded border border-border">{exp.duration}</span>
                      </div>
                      <ul className="list-disc list-inside space-y-1">
                        {exp.bullets.map((b, bIdx) => (
                          <li key={bIdx} className="text-xs text-text-secondary leading-relaxed list-none relative pl-3">
                            <span className="absolute left-0 top-[6px] w-1.5 h-1.5 bg-primary rounded-full"></span>
                            {b}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Action Alert Banner */}
            <div className="bg-gradient-to-r from-primary-xlight to-white border border-primary-light rounded-2xl p-5 shadow-sm flex flex-col sm:flex-row items-center justify-between gap-4">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-primary rounded-xl flex items-center justify-center text-white shrink-0">
                  <Sparkles size={18} />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-text-primary">{t('mycv.ready_to_practice', 'Sẵn sàng để luyện tập?')}</h4>
                  <p className="text-xs text-text-secondary mt-0.5">{t('mycv.practice_desc', 'AI đã cá nhân hóa bộ câu hỏi phỏng vấn dựa trên CV vừa tải lên.')}</p>
                </div>
              </div>
              <a
                href="#dashboard"
                className="bg-primary hover:bg-primary-dark text-white text-xs font-bold py-2.5 px-5 rounded-xl transition-all flex items-center gap-1.5 shadow-sm hover:shadow-md self-start sm:self-auto"
              >
                {t('mycv.start_practice', 'Bắt đầu phỏng vấn ngay')}
                <ChevronRight size={14} />
              </a>
            </div>
          </div>
        )}
      </div>

      {/* AI Feedback Modal / Drawer */}
      {showFeedbackModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4 animate-fade-in">
          <div className="bg-white w-full max-w-2xl rounded-2xl shadow-xl border border-border flex flex-col max-h-[85vh] overflow-hidden animate-in zoom-in-95 duration-200">
            {/* Modal Header */}
            <div className="px-6 py-4 border-b border-border flex items-center justify-between bg-surface-1">
              <div className="flex items-center gap-2">
                <Sparkles size={20} className="text-primary-dark animate-pulse" />
                <h3 className="text-base font-bold text-text-primary">{t('mycv.modal_title', 'Đánh giá & Phản hồi CV từ AI')}</h3>
              </div>
              <button
                onClick={() => setShowFeedbackModal(false)}
                className="p-1.5 hover:bg-surface-3 text-text-secondary rounded-lg transition-colors text-sm font-bold"
              >
                {t('mycv.close', 'Đóng')}
              </button>
            </div>

            {/* Tabs */}
            <div className="flex border-b border-border px-6 bg-surface-1">
              <button
                onClick={() => setActiveTab('overall')}
                className={`py-3 text-xs font-bold uppercase tracking-wider border-b-2 px-3 transition-colors ${activeTab === 'overall'
                    ? 'border-primary text-primary-dark'
                    : 'border-transparent text-text-secondary hover:text-text-primary'
                  }`}
              >
                {t('mycv.tab_overall', 'Đánh giá chung')}
              </button>
              <button
                onClick={() => setActiveTab('strengths')}
                className={`py-3 text-xs font-bold uppercase tracking-wider border-b-2 px-3 transition-colors ${activeTab === 'strengths'
                    ? 'border-primary text-primary-dark'
                    : 'border-transparent text-text-secondary hover:text-text-primary'
                  }`}
              >
                {t('mycv.tab_strengths', 'Điểm mạnh')}
              </button>
              <button
                onClick={() => setActiveTab('improvements')}
                className={`py-3 text-xs font-bold uppercase tracking-wider border-b-2 px-3 transition-colors ${activeTab === 'improvements'
                    ? 'border-primary text-primary-dark'
                    : 'border-transparent text-text-secondary hover:text-text-primary'
                  }`}
              >
                {t('mycv.tab_improvements', 'Cần cải thiện')}
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-6 overflow-y-auto space-y-4 flex-1">
              {activeTab === 'overall' && (
                <div className="space-y-4">
                  <div className="flex items-center gap-3 p-4 bg-primary-xlight border border-primary-light rounded-xl">
                    <div className="text-2xl font-black text-primary-dark">8.5<span className="text-xs text-text-secondary">/10</span></div>
                    <div className="h-8 w-px bg-primary-light"></div>
                    <div>
                      <div className="text-xs font-bold text-primary-dark uppercase">{t('mycv.score_title', 'Điểm đánh giá CV')}</div>
                      <p className="text-xs text-text-secondary mt-0.5">{t('mycv.mock_score_desc', 'CV của bạn có cấu trúc tốt, rõ ràng và đầy đủ thông tin cốt lõi.')}</p>
                    </div>
                  </div>

                  <div className="space-y-2">
                    <h4 className="text-sm font-bold text-text-primary">{t('mycv.summary_title', 'Tóm tắt đánh giá:')}</h4>
                    <p className="text-xs text-text-secondary leading-relaxed">
                      {t('mycv.mock_summary_desc', 'CV được định dạng theo cấu trúc chuẩn. Các kỹ năng kỹ thuật được trình bày mạch lạc, khớp với yêu cầu của vị trí Frontend Developer. Các dự án được liệt kê chi tiết tuy nhiên cần có thêm các chỉ số định lượng (ví dụ: tăng 20% performance, giảm thời gian load trang...) để làm nổi bật tác động của bạn.')}
                    </p>
                  </div>
                </div>
              )}

              {activeTab === 'strengths' && (
                <div className="space-y-3">
                  <div className="flex items-start gap-2.5 p-3 bg-success-light/30 border border-success/20 rounded-xl">
                    <CheckCircle2 size={16} className="text-success mt-0.5 shrink-0" />
                    <div>
                      <h4 className="text-xs font-bold text-text-primary">{t('mycv.mock_strength_1_title', 'Công nghệ hiện đại & phù hợp')}</h4>
                      <p className="text-xs text-text-secondary mt-1">{t('mycv.mock_strength_1_desc', 'Sử dụng stack phổ biến (React, Next.js, Redux, TS) đáp ứng rất tốt nhu cầu tuyển dụng Frontend hiện nay.')}</p>
                    </div>
                  </div>
                  <div className="flex items-start gap-2.5 p-3 bg-success-light/30 border border-success/20 rounded-xl">
                    <CheckCircle2 size={16} className="text-success mt-0.5 shrink-0" />
                    <div>
                      <h4 className="text-xs font-bold text-text-primary">{t('mycv.mock_strength_2_title', 'Bố cục rõ ràng, chuyên nghiệp')}</h4>
                      <p className="text-xs text-text-secondary mt-1">{t('mycv.mock_strength_2_desc', 'Các phần thông tin liên hệ, học vấn, kỹ năng, dự án được phân chia rành mạch, dễ đọc lướt (scannable).')}</p>
                    </div>
                  </div>
                </div>
              )}

              {activeTab === 'improvements' && (
                <div className="space-y-3">
                  <div className="flex items-start gap-2.5 p-3 bg-warning-light/30 border border-warning/20 rounded-xl">
                    <AlertCircle size={16} className="text-warning mt-0.5 shrink-0" />
                    <div>
                      <h4 className="text-xs font-bold text-text-primary">{t('mycv.mock_improvement_1_title', 'Thiếu số liệu định lượng (Quantifiable Results)')}</h4>
                      <p className="text-xs text-text-secondary mt-1">{t('mycv.mock_improvement_1_desc', 'Nên cụ thể hóa kết quả đạt được. Thay vì ghi "tối ưu hóa UI/UX", hãy viết "tối ưu UI/UX giúp cải thiện 25% tỷ lệ giữ chân người dùng".')}</p>
                    </div>
                  </div>
                  <div className="flex items-start gap-2.5 p-3 bg-warning-light/30 border border-warning/20 rounded-xl">
                    <AlertCircle size={16} className="text-warning mt-0.5 shrink-0" />
                    <div>
                      <h4 className="text-xs font-bold text-text-primary">{t('mycv.mock_improvement_2_title', 'Bổ sung link sản phẩm / Github')}</h4>
                      <p className="text-xs text-text-secondary mt-1">{t('mycv.mock_improvement_2_desc', 'Các dự án nên có đường dẫn Github hoặc link demo trực tiếp để nhà tuyển dụng dễ dàng đánh giá mã nguồn thực tế.')}</p>
                    </div>
                  </div>
                </div>
              )}
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-4 border-t border-border bg-surface-1 flex justify-end">
              <button
                onClick={() => setShowFeedbackModal(false)}
                className="bg-primary hover:bg-primary-dark text-white text-xs font-bold py-2 px-4 rounded-xl transition-all shadow-sm"
              >
                {t('mycv.got_it', 'Đã hiểu')}
              </button>
            </div>
          </div>
        </div>
      )}
    </UserLayout>
  );
}

export default MyCVPage;

