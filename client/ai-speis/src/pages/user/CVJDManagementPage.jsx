import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Upload,
  FileText,
  Briefcase,
  Trash2,
  Eye,
  Plus,
  Clock,
  CheckCircle2,
  AlertCircle,
  X,
  Type
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import cvService from '../../services/CVService';
import '../../styles/user/MyCVPage.css';

function CVJDManagementPage() {
  const { t } = useTranslation('dashboard');
  const [activeTab, setActiveTab] = useState('cv'); // 'cv' or 'jd'
  
  // CV State
  const [cvs, setCvs] = useState([]);
  const [isLoadingCvs, setIsLoadingCvs] = useState(true);

  // JD Mock State
  const [mockJDs, setMockJDs] = useState([
    {
      id: 1,
      fileName: 'Senior_Frontend_Developer_JD.pdf',
      uploadedAt: new Date().toISOString(),
      company: 'Tech Corp',
      type: 'pdf'
    }
  ]);
  const [showJDModal, setShowJDModal] = useState(false);
  const [jdUploadType, setJdUploadType] = useState('file'); // 'file' or 'text'
  const [jdText, setJdText] = useState('');

  useEffect(() => {
    fetchCVHistory();
  }, []);

  const fetchCVHistory = async () => {
    setIsLoadingCvs(true);
    try {
      const result = await cvService.getMyCVHistory(1, 20); // Get first 20 CVs
      if (result && result.items) {
        setCvs(result.items);
      }
    } catch (err) {
      console.error('Lỗi khi lấy lịch sử CV:', err);
    } finally {
      setIsLoadingCvs(false);
    }
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    return date.toLocaleDateString('vi-VN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
  };

  // Convert numeric status enum back to string mapping if needed
  const STATUS_INT_MAP = {
    0: 'Pending',
    1: 'Processing',
    2: 'ConfirmationRequired',
    3: 'Confirmed',
    4: 'Failed',
    5: 'AnalysisFailed',
    6: 'Archived',
  };

  const getStatusString = (status) => {
    if (typeof status === 'number') return STATUS_INT_MAP[status] || 'Unknown';
    return status;
  };

  const getStatusBadge = (rawStatus) => {
    const status = getStatusString(rawStatus);
    if (status === 'Confirmed') return <span className="mycv-badge mycv-badge--success"><CheckCircle2 size={10} /> Đã xác nhận</span>;
    if (status === 'Archived') return <span className="mycv-badge mycv-badge--default"><Clock size={10} /> Đã lưu trữ</span>;
    if (status === 'ConfirmationRequired') return <span className="mycv-badge mycv-badge--info"><AlertCircle size={10} /> Cần xác nhận</span>;
    if (status === 'Pending' || status === 'Processing') return <span className="mycv-badge mycv-badge--warning"><Clock size={10} /> Đang xử lý</span>;
    return <span className="mycv-badge mycv-badge--error"><AlertCircle size={10} /> {status}</span>;
  };

  const handleCVClick = (id) => {
    navigate(USER_ROUTES.CV_DETAIL);
  };

  const handleJDClick = (id) => {
    alert('Tính năng xem chi tiết JD đang được phát triển.');
  };

  // Upload CV (Mock directly to CV_DETAIL which handles actual upload, or show alert)
  const handleUploadCV = async (e) => {
    const file = e.target.files[0];
    if (file) {
      // In a real app, we might upload here then refresh the list.
      // But MyCVPage currently handles its own upload. Let's navigate to MyCVPage so the user can upload.
      navigate(USER_ROUTES.CV_DETAIL);
    }
  };

  // Handle JD Upload submission (Mock)
  const submitJDUpload = () => {
    if (mockJDs.length >= 5) {
      alert('Đã đạt giới hạn 5 JD!');
      return;
    }
    
    const newJd = {
      id: Date.now(),
      fileName: jdUploadType === 'file' ? 'New_JD.pdf' : 'Pasted_Text_JD.txt',
      uploadedAt: new Date().toISOString(),
      company: 'Unknown Company',
      type: jdUploadType
    };
    
    setMockJDs([newJd, ...mockJDs]);
    setShowJDModal(false);
    setJdText('');
  };

  return (
    <UserLayout>
      <div className="mycv-container animate-pageEntrance relative">
        {/* Header */}
        <section className="mycv-header">
          <div>
            <h1 className="mycv-title">Quản lí CV & JD</h1>
            <p className="mycv-subtitle">
              Quản lý danh sách CV và Job Description đã tải lên của bạn.
            </p>
          </div>
        </section>

        {/* Tabs */}
        <div className="flex border-b border-border mb-6">
          <button
            className={`px-6 py-3 text-sm font-semibold transition-colors relative ${activeTab === 'cv' ? 'text-primary' : 'text-text-secondary hover:text-text-primary'}`}
            onClick={() => setActiveTab('cv')}
          >
            <div className="flex items-center gap-2">
              <FileText size={18} />
              Danh sách CV ({cvs.length})
            </div>
            {activeTab === 'cv' && (
              <div className="absolute bottom-0 left-0 w-full h-0.5 bg-primary rounded-t-md" />
            )}
          </button>
          <button
            className={`px-6 py-3 text-sm font-semibold transition-colors relative ${activeTab === 'jd' ? 'text-primary' : 'text-text-secondary hover:text-text-primary'}`}
            onClick={() => setActiveTab('jd')}
          >
            <div className="flex items-center gap-2">
              <Briefcase size={18} />
              Danh sách JD ({mockJDs.length}/5)
            </div>
            {activeTab === 'jd' && (
              <div className="absolute bottom-0 left-0 w-full h-0.5 bg-primary rounded-t-md" />
            )}
          </button>
        </div>

        {/* Tab Content: CVs */}
        {activeTab === 'cv' && (
          <div className="space-y-4">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-lg font-semibold text-text-primary">Lịch sử upload CV</h2>
              <button className="mycv-btn mycv-btn--primary" onClick={() => navigate(USER_ROUTES.CV_DETAIL)}>
                <Upload size={16} />
                Tải CV mới
              </button>
            </div>

            {isLoadingCvs ? (
              <div className="text-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary mx-auto mb-4"></div>
                <p className="text-text-secondary">Đang tải danh sách CV...</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {cvs.map(cv => {
                  const isLatest = getStatusString(cv.status) !== 'Archived';
                  return (
                    <div key={cv.cvFileId} className="mycv-info-card hover:border-primary transition-colors cursor-pointer" onClick={() => handleCVClick(cv.cvFileId)}>
                      <div className={`mycv-info-card-accent ${isLatest ? 'bg-primary' : 'bg-surface-3'}`} />
                      <div className="mycv-info-left flex-1 overflow-hidden">
                        <div className={`mycv-info-icon-box ${isLatest ? 'bg-primary/10 text-primary' : 'bg-surface-2 text-text-disabled'}`}>
                          <FileText size={24} />
                        </div>
                        <div className="mycv-info-details min-w-0 flex-1">
                          <div className="flex justify-between items-start gap-2">
                            <h3 className="font-semibold text-text-primary truncate" title={cv.fileName}>
                              {cv.fileName}
                            </h3>
                            {getStatusBadge(cv.status)}
                          </div>
                          <p className="mycv-info-date mt-1">Tải lên: {formatDate(cv.uploadedAt)}</p>
                          {isLatest && (
                            <p className="text-xs text-primary font-medium mt-2 flex items-center gap-1">
                              <CheckCircle2 size={12} /> CV đang sử dụng
                            </p>
                          )}
                        </div>
                      </div>
                      <div className="mycv-info-actions border-l border-border pl-4 ml-2">
                        <button className="p-2 text-text-secondary hover:text-primary transition-colors" title="Xem chi tiết" onClick={(e) => { e.stopPropagation(); handleCVClick(cv.cvFileId); }}>
                          <Eye size={18} />
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
            
            {!isLoadingCvs && cvs.length === 0 && (
              <div className="text-center py-12 bg-surface-2 rounded-xl border border-dashed border-border">
                <FileText size={48} className="mx-auto text-text-disabled mb-4" />
                <p className="text-text-secondary">Bạn chưa tải lên CV nào.</p>
              </div>
            )}
          </div>
        )}

        {/* Tab Content: JDs */}
        {activeTab === 'jd' && (
          <div className="space-y-4">
            <div className="flex justify-between items-center mb-4">
              <div>
                <h2 className="text-lg font-semibold text-text-primary">Job Description (JD)</h2>
                <p className="text-sm text-text-secondary mt-1">Bạn có thể lưu tối đa 5 JD để luyện phỏng vấn sát với yêu cầu thực tế.</p>
              </div>
              <button 
                className={`mycv-btn ${mockJDs.length >= 5 ? 'mycv-btn--default opacity-50 cursor-not-allowed' : 'mycv-btn--primary'}`}
                onClick={() => mockJDs.length < 5 && setShowJDModal(true)}
                disabled={mockJDs.length >= 5}
              >
                <Plus size={16} />
                Thêm JD mới
              </button>
            </div>

            {mockJDs.length >= 5 && (
              <div className="bg-warning/10 border border-warning/30 text-warning px-4 py-3 rounded-lg flex items-center gap-3 mb-4">
                <AlertCircle size={20} />
                <span className="text-sm">Bạn đã đạt giới hạn tối đa 5 JD. Vui lòng xóa bớt JD cũ để có thể thêm mới.</span>
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {mockJDs.map(jd => (
                <div key={jd.id} className="mycv-info-card hover:border-primary transition-colors cursor-pointer" onClick={() => handleJDClick(jd.id)}>
                  <div className="mycv-info-card-accent bg-[#4A90E2]" />
                  <div className="mycv-info-left flex-1 overflow-hidden">
                    <div className="mycv-info-icon-box bg-[#4A90E2]/10 text-[#4A90E2]">
                      <Briefcase size={24} />
                    </div>
                    <div className="mycv-info-details min-w-0 flex-1">
                      <div className="flex justify-between items-start gap-2">
                        <h3 className="font-semibold text-text-primary truncate" title={jd.fileName}>
                          {jd.fileName}
                        </h3>
                        <span className="mycv-badge mycv-badge--info text-[10px] uppercase">
                          {jd.type === 'file' ? 'PDF' : 'Text'}
                        </span>
                      </div>
                      <p className="text-sm text-text-secondary mt-1 font-medium">{jd.company}</p>
                      <p className="mycv-info-date mt-1">Tải lên: {formatDate(jd.uploadedAt)}</p>
                    </div>
                  </div>
                  <div className="mycv-info-actions border-l border-border pl-4 ml-2">
                    <button className="p-2 text-text-secondary hover:text-primary transition-colors" title="Xem chi tiết" onClick={(e) => { e.stopPropagation(); handleJDClick(jd.id); }}>
                      <Eye size={18} />
                    </button>
                    <button className="p-2 text-text-secondary hover:text-error transition-colors" title="Xóa" onClick={(e) => { e.stopPropagation(); setMockJDs(mockJDs.filter(item => item.id !== jd.id)); }}>
                      <Trash2 size={18} />
                    </button>
                  </div>
                </div>
              ))}
            </div>
            
            {mockJDs.length === 0 && (
              <div className="text-center py-12 bg-surface-2 rounded-xl border border-dashed border-border">
                <Briefcase size={48} className="mx-auto text-text-disabled mb-4" />
                <p className="text-text-secondary">Bạn chưa có JD nào.</p>
              </div>
            )}
          </div>
        )}

      </div>

      {/* JD Upload Modal */}
      {showJDModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="bg-surface-1 rounded-xl shadow-xl w-full max-w-lg overflow-hidden animate-pageEntrance">
            <div className="flex items-center justify-between p-4 border-b border-border">
              <h3 className="text-lg font-semibold text-text-primary flex items-center gap-2">
                <Briefcase size={20} className="text-primary" />
                Thêm Job Description mới
              </h3>
              <button onClick={() => setShowJDModal(false)} className="text-text-secondary hover:text-text-primary">
                <X size={20} />
              </button>
            </div>
            
            <div className="p-4">
              {/* Type selector */}
              <div className="flex bg-surface-2 p-1 rounded-lg mb-6">
                <button 
                  className={`flex-1 flex items-center justify-center gap-2 py-2 text-sm font-medium rounded-md transition-colors ${jdUploadType === 'file' ? 'bg-surface-1 text-primary shadow-sm' : 'text-text-secondary hover:text-text-primary'}`}
                  onClick={() => setJdUploadType('file')}
                >
                  <Upload size={16} /> Upload PDF
                </button>
                <button 
                  className={`flex-1 flex items-center justify-center gap-2 py-2 text-sm font-medium rounded-md transition-colors ${jdUploadType === 'text' ? 'bg-surface-1 text-primary shadow-sm' : 'text-text-secondary hover:text-text-primary'}`}
                  onClick={() => setJdUploadType('text')}
                >
                  <Type size={16} /> Dán Text
                </button>
              </div>

              {jdUploadType === 'file' ? (
                <div className="border-2 border-dashed border-border rounded-xl p-8 text-center bg-surface-2 hover:bg-surface-3 transition-colors">
                  <div className="bg-primary/10 text-primary w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-4">
                    <Upload size={24} />
                  </div>
                  <h4 className="text-text-primary font-medium mb-1">Chọn hoặc kéo thả file JD</h4>
                  <p className="text-sm text-text-secondary mb-4">Hỗ trợ định dạng PDF (tối đa 5MB)</p>
                  <label className="mycv-btn mycv-btn--primary mx-auto w-max cursor-pointer">
                    Chọn tệp
                    <input type="file" accept=".pdf" hidden onChange={(e) => { if(e.target.files.length) submitJDUpload() }} />
                  </label>
                </div>
              ) : (
                <div>
                  <label className="block text-sm font-medium text-text-primary mb-2">Nội dung JD</label>
                  <textarea 
                    className="w-full bg-surface-2 border border-border rounded-lg p-3 text-text-primary min-h-[200px] focus:outline-none focus:border-primary resize-none"
                    placeholder="Dán nội dung Job Description vào đây..."
                    value={jdText}
                    onChange={(e) => setJdText(e.target.value)}
                  ></textarea>
                </div>
              )}
            </div>
            
            <div className="p-4 border-t border-border flex justify-end gap-3 bg-surface-2/50">
              <button className="mycv-btn mycv-btn--outline" onClick={() => setShowJDModal(false)}>Hủy</button>
              {jdUploadType === 'text' && (
                <button 
                  className="mycv-btn mycv-btn--primary" 
                  onClick={submitJDUpload}
                  disabled={!jdText.trim()}
                >
                  Xác nhận lưu
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </UserLayout>
  );
}

export default CVJDManagementPage;
