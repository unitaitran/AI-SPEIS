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
import jdService from '../../services/JDService';
import { API_BASE_URL } from '../../config/api';
import '../../styles/user/MyCVPage.css';

function CVJDManagementPage() {
  const { t } = useTranslation('dashboard');
  
  // CV State
  const [cvs, setCvs] = useState([]);
  const [isLoadingCvs, setIsLoadingCvs] = useState(true);

  // JD State
  const [jds, setJds] = useState([]);
  const [isLoadingJds, setIsLoadingJds] = useState(true);
  const [showJDModal, setShowJDModal] = useState(false);
  const [jdUploadType, setJdUploadType] = useState('file'); // 'file' or 'text'
  const [jdText, setJdText] = useState('');

  useEffect(() => {
    fetchCVHistory();
    fetchJDHistory();
  }, []);

  const fetchCVHistory = async () => {
    setIsLoadingCvs(true);
    try {
      const result = await cvService.getMyCVHistory(1, 10); 
      if (result && result.items) {
        // Chỉ lấy CV active cuối cùng
        const latestCV = result.items.find(cv => getStatusString(cv.status) !== 'Archived');
        setCvs(latestCV ? [latestCV] : []);
      }
    } catch (err) {
      console.error('Lỗi khi lấy lịch sử CV:', err);
    } finally {
      setIsLoadingCvs(false);
    }
  };

  const fetchJDHistory = async () => {
    setIsLoadingJds(true);
    try {
      const result = await jdService.getMyJDHistory(1, 5); // Tối đa 5 JDs
      if (result && result.items) {
        setJds(result.items);
      }
    } catch (err) {
      console.error('Lỗi khi lấy lịch sử JD:', err);
    } finally {
      setIsLoadingJds(false);
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

  // Handle JD Upload submission
  const submitJDUpload = async (file = null) => {
    if (jds.length >= 5) {
      alert('Đã đạt giới hạn 5 JD!');
      return;
    }
    
    try {
      if (jdUploadType === 'file' && file) {
        await jdService.uploadJD(file);
      } else if (jdUploadType === 'text' && jdText.trim()) {
        await jdService.submitJDText(jdText.trim());
      }
      setShowJDModal(false);
      setJdText('');
      fetchJDHistory();
    } catch (err) {
      alert('Lỗi khi tải lên JD: ' + err.message);
    }
  };

  const handleDeleteJD = async (id, e) => {
    e.stopPropagation();
    if (window.confirm('Bạn có chắc chắn muốn xóa JD này?')) {
      try {
        await jdService.deleteJD(id);
        fetchJDHistory();
      } catch (err) {
        alert('Lỗi khi xóa JD: ' + err.message);
      }
    }
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

        {/* 2 Columns Layout */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
          
          {/* Section: CVs (Left Column) */}
          <div className="space-y-4 bg-surface-1 p-6 rounded-2xl border border-border">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-lg font-semibold text-text-primary">CV hiện tại của bạn</h2>
              {cvs.length > 0 && !isLoadingCvs && (
                <button className="mycv-btn mycv-btn--outline" onClick={() => handleCVClick(cvs[0].cvFileId)}>
                  <Eye size={16} />
                  Xem chi tiết
                </button>
              )}
            </div>

            {isLoadingCvs ? (
              <div className="text-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary mx-auto mb-4"></div>
                <p className="text-text-secondary">Đang tải CV...</p>
              </div>
            ) : (
              <div>
                {cvs.length > 0 ? (
                  <div className="space-y-4">
                    {/* CV Preview */}
                    <div className="w-full h-[600px] rounded-xl border border-border overflow-hidden bg-surface-2">
                      <iframe 
                        src={`${API_BASE_URL}${cvs[0].filePath}#toolbar=0&navpanes=0`} 
                        title="CV Preview"
                        className="w-full h-full"
                      />
                    </div>
                  </div>
                ) : (
                  <div className="text-center py-12 bg-surface-2 rounded-xl border border-dashed border-border h-[500px] flex flex-col items-center justify-center">
                    <FileText size={48} className="mx-auto text-text-disabled mb-4" />
                    <p className="text-text-secondary mb-4">Bạn chưa có CV nào được sử dụng.</p>
                    <button className="mycv-btn mycv-btn--primary" onClick={() => navigate(USER_ROUTES.CV_DETAIL)}>
                      <Upload size={16} />
                      Tải CV lên ngay
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Section: JDs (Right Column) */}
          <div className="space-y-4 bg-surface-1 p-6 rounded-2xl border border-border">
            <div className="flex justify-between items-center mb-4">
              <div>
                <h2 className="text-lg font-semibold text-text-primary">Job Description (JD)</h2>
                <p className="text-sm text-text-secondary mt-1">Lưu tối đa 5 JD để luyện phỏng vấn.</p>
              </div>
              <button 
                className={`mycv-btn ${jds.length >= 5 ? 'mycv-btn--default opacity-50 cursor-not-allowed' : 'mycv-btn--primary'} flex-shrink-0`}
                onClick={() => jds.length < 5 && setShowJDModal(true)}
                disabled={jds.length >= 5}
              >
                <Plus size={16} />
                Thêm JD
              </button>
            </div>

            {jds.length >= 5 && (
              <div className="bg-warning/10 border border-warning/30 text-warning px-4 py-3 rounded-lg flex items-center gap-3 mb-4">
                <AlertCircle size={20} className="flex-shrink-0" />
                <span className="text-xs">Bạn đã đạt giới hạn 5 JD. Hãy xóa JD cũ để thêm mới.</span>
              </div>
            )}

            {isLoadingJds ? (
              <div className="text-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary mx-auto mb-4"></div>
                <p className="text-text-secondary">Đang tải danh sách JD...</p>
              </div>
            ) : (
              <div className="flex flex-col gap-4">
                {jds.map(jd => (
                  <div key={jd.jdFileId} className="mycv-info-card hover:border-primary transition-colors cursor-pointer" onClick={() => handleJDClick(jd.jdFileId)}>
                    <div className="mycv-info-card-accent bg-[#4A90E2]" />
                    <div className="mycv-info-left flex-1 overflow-hidden">
                      <div className="mycv-info-icon-box bg-[#4A90E2]/10 text-[#4A90E2]">
                        <Briefcase size={24} />
                      </div>
                      <div className="mycv-info-details min-w-0 flex-1">
                        <div className="flex justify-between items-start gap-2">
                          <h3 className="font-semibold text-sm text-text-primary truncate" title={jd.fileName || 'JD Text'}>
                            {jd.fileName || 'JD nhập bằng Text'}
                          </h3>
                          <span className="mycv-badge mycv-badge--info text-[10px] uppercase whitespace-nowrap">
                            {jd.inputType === 1 ? 'Text' : 'PDF'}
                          </span>
                        </div>
                        <p className="text-xs text-text-secondary mt-1">Tải lên: {formatDate(jd.uploadedAt)}</p>
                      </div>
                    </div>
                    <div className="mycv-info-actions border-l border-border pl-3 ml-2 flex gap-1">
                      <button className="p-1.5 text-text-secondary hover:text-primary transition-colors bg-surface-2 rounded hover:bg-primary/10" title="Xem chi tiết" onClick={(e) => { e.stopPropagation(); handleJDClick(jd.jdFileId); }}>
                        <Eye size={16} />
                      </button>
                      <button className="p-1.5 text-text-secondary hover:text-error transition-colors bg-surface-2 rounded hover:bg-error/10" title="Xóa" onClick={(e) => handleDeleteJD(jd.jdFileId, e)}>
                        <Trash2 size={16} />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
            
            {!isLoadingJds && jds.length === 0 && (
              <div className="text-center py-12 bg-surface-2 rounded-xl border border-dashed border-border flex flex-col items-center justify-center">
                <Briefcase size={48} className="mx-auto text-text-disabled mb-4" />
                <p className="text-text-secondary">Bạn chưa có JD nào.</p>
              </div>
            )}
          </div>
        </div>

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
                    <input type="file" accept=".pdf" hidden onChange={(e) => { if(e.target.files.length) submitJDUpload(e.target.files[0]) }} />
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
                  onClick={() => submitJDUpload()}
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
