import React, { useState, useEffect, useRef } from 'react';
import {
  Upload,
  FileText,
  Briefcase,
  Trash2,
  Eye,
  Plus,
  CheckCircle2,
  AlertCircle,
  X,
  Type,
  Loader2,
  Target,
  Award,
  Star,
  CheckSquare,
  Building,
  Sparkles
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { useTranslation } from 'react-i18next';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import cvService from '../../services/CVService';
import jdService from '../../services/JDService';
import { API_BASE_URL } from '../../config/api';
import { mapCvJdMatchResponse } from '../../features/cvJdFastCheck/cvJdMatchAdapter';
import FastCheckResult from '../../features/cvJdFastCheck/FastCheckResult';
import notify from '../../utils/notification';
import '../../styles/user/MyCVPage.css';

function CVJDManagementPage() {
  const { t } = useTranslation('cvjd');  
  // CV State
  const [cvs, setCvs] = useState([]);
  const [isLoadingCvs, setIsLoadingCvs] = useState(true);

  // JD State
  const [jds, setJds] = useState([]);
  const [isLoadingJds, setIsLoadingJds] = useState(true);
  const [showJDModal, setShowJDModal] = useState(false);
  const [jdUploadType, setJdUploadType] = useState('file'); // 'file' or 'text'
  const [jdText, setJdText] = useState('');
  const [jdTextName, setJdTextName] = useState('');
  const [showJDInfoModal, setShowJDInfoModal] = useState(false);
  const [selectedJDParsedData, setSelectedJDParsedData] = useState(null);
  const [selectedJDId, setSelectedJDId] = useState(null);
  const jdPollTimerRef = useRef(null);

  // Fast Check State
  const [fastCheckResults, setFastCheckResults] = useState({});
  const [isFastChecking, setIsFastChecking] = useState({});
  const [showFastCheckModal, setShowFastCheckModal] = useState(false);
  const [currentFastCheckJD, setCurrentFastCheckJD] = useState(null);

  useEffect(() => {
    return () => stopJdPolling();
  }, []);

  const stopJdPolling = () => {
    if (jdPollTimerRef.current) {
      clearInterval(jdPollTimerRef.current);
      jdPollTimerRef.current = null;
    }
  };

  const startJdPolling = (jdId) => {
    stopJdPolling();
    jdPollTimerRef.current = setInterval(async () => {
      try {
        const statusResp = await jdService.getParseStatus(jdId);
        const statusStr = getStatusString(statusResp.data.status);
        if (statusStr === 'ConfirmationRequired' || statusStr === 'Confirmed') {
          stopJdPolling();
          fetchJDHistory();
          const parsed = await jdService.getParsedData(jdId);
          setSelectedJDParsedData(parsed.data);
          setSelectedJDId(jdId);
          setShowJDInfoModal(true);
        } else if (statusStr === 'AnalysisFailed' || statusStr === 'Failed') {
          stopJdPolling();
          fetchJDHistory();
          const errorMsg = "Đây không phải là JD/CV hoặc bạn đang upload chưa phải thông tin JD hoàn thiện. Hãy thử lại.";
          notify.error(errorMsg, { title: 'Phân tích JD thất bại' });
        }
      } catch (err) {
        // keep polling
      }
    }, 3000);
  };

  useEffect(() => {
    fetchCVHistory();
    fetchJDHistory();
    fetchFastCheckResults();
  }, []);

  const fetchCVHistory = async () => {
    setIsLoadingCvs(true);
    try {
      const result = await cvService.getMyCVHistory(1, 10); 
      if (result && result.items) {
        // Chỉ lấy CV active cuối cùng (loại bỏ Archived và AnalysisFailed/Failed)
        const latestCV = result.items.find(cv => {
          const status = getStatusString(cv.status);
          return status !== 'Archived' && status !== 'AnalysisFailed' && status !== 'Failed';
        });
        setCvs(latestCV ? [latestCV] : []);
      }
    } catch (err) {
      console.error('Lỗi khi lấy lịch sử CV:', err);
    } finally {
      setIsLoadingCvs(false);
    }
  };

  const fetchFastCheckResults = async () => {
    try {
      const response = await jdService.getFastCheckResults();
      if (response && response.data) {
        const resultsMap = {};
        response.data.forEach(item => {
          resultsMap[item.jdFileId] = mapCvJdMatchResponse(item);
        });
        setFastCheckResults(resultsMap);
      }
    } catch (err) {
      console.error('Lỗi khi lấy lịch sử FastCheck:', err);
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

  const handleCVClick = (id) => {
    navigate(USER_ROUTES.CV_DETAIL);
  };

  const handleJDClick = (id) => {
    // Navigate or default action
  };

  const handleJDActionClick = async (jdId, e) => {
    e.stopPropagation();
    const jd = jds.find(j => j.jdFileId === jdId);
    if (!jd) return;
    
    const statusStr = getStatusString(jd.status);
    if (statusStr === 'Pending' || statusStr === 'Failed' || statusStr === 'AnalysisFailed') {
      try {
        await jdService.triggerParse(jdId);
        setJds(prev => prev.map(j => j.jdFileId === jdId ? { ...j, status: 1 } : j));
        startJdPolling(jdId);
      } catch (err) {
        notify.error(`Không thể bắt đầu phân tích: ${err.message}`, { title: 'Không thể phân tích JD' });
      }
    } else if (statusStr === 'Processing') {
      startJdPolling(jdId);
      notify.info('JD đang được xử lý, vui lòng chờ...', { title: `${t('analyzing', 'Đang phân tích')} JD` });
    } else if (statusStr === 'ConfirmationRequired' || statusStr === 'Confirmed') {
      try {
        const parsed = await jdService.getParsedData(jdId);
        setSelectedJDParsedData(parsed.data);
        setSelectedJDId(jdId);
        setShowJDInfoModal(true);
      } catch (err) {
        notify.error(`Lỗi lấy dữ liệu: ${err.message}`, { title: 'Không thể tải dữ liệu JD' });
      }
    }
  };

  // Handle JD Upload submission
  const submitJDUpload = async (file = null) => {
    if (jds.length >= 5) {
      notify.warning('Bạn đã đạt giới hạn tối đa 5 JD.', { title: 'Không thể thêm JD' });
      return;
    }

    if (file && file.size > 5 * 1024 * 1024) {
      notify.warning('File tải lên vượt quá dung lượng tối đa 5MB. Vui lòng chọn file khác.', { title: 'File không hợp lệ' });
      return;
    }
    
    try {
      if (jdUploadType === 'file' && file) {
        await jdService.uploadJD(file);
      } else if (jdUploadType === 'text' && jdText.trim() && jdTextName.trim()) {
        await jdService.submitJDText(jdTextName.trim(), jdText.trim());
      }
      setShowJDModal(false);
      setJdText('');
      setJdTextName('');
      fetchJDHistory();
      notify.success('Job Description đã được thêm thành công.');
    } catch (err) {
      notify.error(`Lỗi khi tải lên JD: ${err.message}`, { title: 'Không thể thêm JD' });
    }
  };

  const handleDeleteJD = async (id, e) => {
    e.stopPropagation();
    if (window.confirm('Bạn có chắc chắn muốn xóa JD này?')) {
      try {
        await jdService.deleteJD(id);
        fetchJDHistory();
        notify.success('Job Description đã được xóa thành công.');
      } catch (err) {
        notify.error(`Lỗi khi xóa JD: ${err.message}`, { title: 'Không thể xóa JD' });
      }
    }
  };

  const handleFastCheckClick = async (jdId, e) => {
    e.stopPropagation();
    if (!cvs[0]) {
      notify.warning('Vui lòng tải lên CV trước khi dùng tính năng Fast Check.');
      return;
    }
    
    // If we already have the result, just show it
    if (fastCheckResults[jdId]) {
      setCurrentFastCheckJD(jdId);
      setShowFastCheckModal(true);
      return;
    }

    // Call API to do fast check
    setIsFastChecking(prev => ({ ...prev, [jdId]: true }));
    try {
      const response = await jdService.matchCvToJd(jdId, cvs[0].cvFileId);
      const mappedResult = mapCvJdMatchResponse(response);
      setFastCheckResults(prev => ({ ...prev, [jdId]: mappedResult }));
      setCurrentFastCheckJD(jdId);
      setShowFastCheckModal(true);
    } catch (err) {
      notify.error(err.message, { title: 'Lỗi Fast Check' });
    } finally {
      setIsFastChecking(prev => ({ ...prev, [jdId]: false }));
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
                        </div>
                        <p className="text-xs text-text-secondary mt-1">Tải lên: {formatDate(jd.uploadedAt)}</p>
                      </div>
                    </div>
                    <div className="mycv-info-actions border-l border-border pl-3 ml-2 flex gap-2">
                      {getStatusString(jd.status) === 'ConfirmationRequired' || getStatusString(jd.status) === 'Confirmed' ? (
                        <>
                          <button className="mycv-btn mycv-btn--outline mycv-btn--sm py-1.5 px-3" onClick={(e) => handleJDActionClick(jd.jdFileId, e)}>
                            <Eye size={14} /> Xem
                          </button>
                          <button 
                            className="mycv-btn mycv-btn--primary mycv-btn--sm py-1.5 px-3" 
                            onClick={(e) => handleFastCheckClick(jd.jdFileId, e)}
                            disabled={isFastChecking[jd.jdFileId]}
                          >
                            {isFastChecking[jd.jdFileId] ? (
                              <><Loader2 size={14} className="animate-spin" /> Đang Check</>
                            ) : (
                              <><Sparkles size={14} /> Fast Check</>
                            )}
                          </button>
                        </>
                      ) : getStatusString(jd.status) === 'Processing' ? (
                        <button className="mycv-btn mycv-btn--warning mycv-btn--sm py-1.5 px-3" disabled>
                          <Loader2 size={14} className="animate-spin" /> Đang xử lí
                        </button>
                      ) : (
                        <button className="mycv-btn mycv-btn--primary mycv-btn--sm py-1.5 px-3" onClick={(e) => handleJDActionClick(jd.jdFileId, e)}>
                          <CheckCircle2 size={14} /> Check tỉ lệ
                        </button>
                      )}
                      <button className="p-1.5 text-text-secondary hover:text-error transition-colors bg-surface-2 rounded hover:bg-error/10" title="{t('delete', 'Xóa')}" onClick={(e) => handleDeleteJD(jd.jdFileId, e)}>
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
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-text-primary mb-2">Tên Job Description</label>
                    <input 
                      type="text"
                      className="w-full bg-surface-2 border border-border rounded-lg p-3 text-text-primary focus:outline-none focus:border-primary"
                      placeholder="VD: Frontend Developer tại công ty X..."
                      value={jdTextName}
                      onChange={(e) => setJdTextName(e.target.value)}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-text-primary mb-2">Nội dung JD</label>
                    <textarea 
                      className="w-full bg-surface-2 border border-border rounded-lg p-3 text-text-primary min-h-[200px] focus:outline-none focus:border-primary resize-none"
                      placeholder="Dán nội dung Job Description vào đây..."
                      value={jdText}
                      onChange={(e) => setJdText(e.target.value)}
                    ></textarea>
                  </div>
                </div>
              )}
            </div>
            
            <div className="p-4 border-t border-border flex justify-end gap-3 bg-surface-2/50">
              <button className="mycv-btn mycv-btn--outline" onClick={() => setShowJDModal(false)}>Hủy</button>
              {jdUploadType === 'text' && (
                <button 
                  className="mycv-btn mycv-btn--primary" 
                  onClick={() => submitJDUpload()}
                  disabled={!jdText.trim() || !jdTextName.trim()}
                >
                  Xác nhận lưu
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* JD Info Modal */}
      {showJDInfoModal && selectedJDParsedData && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-surface-1 rounded-xl shadow-xl w-full max-w-3xl overflow-hidden max-h-[90vh] flex flex-col animate-pageEntrance border border-border">
            {/* Modal Header */}
            <div className="flex items-center justify-between p-4 border-b border-border bg-gradient-to-r from-primary/10 to-transparent">
              <h3 className="text-lg font-semibold text-primary flex items-center gap-2">
                <div className="p-1.5 bg-primary text-white rounded-md shadow-sm">
                  <Briefcase size={18} />
                </div>
                Kết quả phân tích JD
              </h3>
              <button onClick={() => setShowJDInfoModal(false)} className="p-1 text-text-secondary hover:text-error hover:bg-error/10 rounded-md transition-colors">
                <X size={20} />
              </button>
            </div>
            
            {/* Modal Body */}
            <div className="p-4 overflow-y-auto flex-1 space-y-6 bg-surface-1">
              
              {/* Header Cards (2 columns) */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-surface-2 p-4 rounded-lg border border-border/50 shadow-sm">
                   <div className="flex items-center gap-2 mb-2">
                     <Target size={16} className="text-primary" />
                     <h4 className="text-base font-semibold text-text-primary">Chức danh & Cấp bậc</h4>
                   </div>
                   <div className="space-y-1">
                     <p className="text-sm text-text-secondary"><strong className="text-text-primary">Vị trí:</strong> {selectedJDParsedData.jobTitle || 'Không xác định'}</p>
                     <p className="text-sm text-text-secondary"><strong className="text-text-primary">Cấp bậc:</strong> {selectedJDParsedData.experienceLevel || 'Không xác định'}</p>
                   </div>
                </div>
                <div className="bg-surface-2 p-4 rounded-lg border border-border/50 shadow-sm flex flex-col justify-between">
                   <div className="flex items-center gap-2 mb-2">
                     <CheckCircle2 size={16} className="text-[#4A90E2]" />
                     <h4 className="text-base font-semibold text-text-primary">CV-JD Fast Check</h4>
                   </div>
                   {fastCheckResults[selectedJDId] ? (
                     <div className="space-y-2">
                       <p className="text-sm text-text-secondary">Độ phù hợp: <span className="font-bold text-[#4A90E2]">{fastCheckResults[selectedJDId].suitabilityLevel}</span> ({fastCheckResults[selectedJDId].score}%)</p>
                       <button onClick={(e) => { e.stopPropagation(); setShowJDInfoModal(false); handleFastCheckClick(selectedJDId, e); }} className="text-xs bg-[#4A90E2]/10 text-[#4A90E2] px-3 py-1.5 rounded font-medium hover:bg-[#4A90E2]/20 transition-colors w-max cursor-pointer">
                         Xem chi tiết Fast Check
                       </button>
                     </div>
                   ) : (
                     <div className="space-y-2">
                       <p className="text-sm text-text-secondary leading-relaxed">
                         JD này đã có dữ liệu trích xuất. Hãy chạy Fast Check để đối chiếu với CV của bạn.
                       </p>
                       <button onClick={(e) => { e.stopPropagation(); handleFastCheckClick(selectedJDId, e); }} disabled={isFastChecking[selectedJDId] || !cvs[0]} className="text-xs bg-[#4A90E2]/10 text-[#4A90E2] px-3 py-1.5 rounded font-medium hover:bg-[#4A90E2]/20 transition-colors flex items-center gap-1.5 w-max disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer">
                         {isFastChecking[selectedJDId] ? <Loader2 size={14} className="animate-spin" /> : <CheckCircle2 size={14} />}
                         {isFastChecking[selectedJDId] ? `${t('analyzing', 'Đang phân tích')}...` : 'Chạy Fast Check ngay'}
                       </button>
                     </div>
                   )}
                </div>
              </div>

              {/* Skills Section */}
              <div className="space-y-4">
                <div>
                   <div className="flex items-center gap-2 mb-2">
                     <CheckSquare size={16} className="text-success" />
                     <h4 className="text-base font-semibold text-text-primary">{t('skills', 'Kỹ năng')} yêu cầu (Bắt buộc)</h4>
                   </div>
                   <div className="flex flex-wrap gap-2">
                     {selectedJDParsedData.requiredSkills?.length > 0 ? (
                       selectedJDParsedData.requiredSkills.map((sk, i) => (
                          <span key={i} className="px-2.5 py-1 bg-primary/10 text-primary border border-primary/20 rounded text-xs font-medium">{sk}</span>
                       ))
                     ) : (
                       <span className="text-sm text-text-secondary italic">Không có dữ liệu</span>
                     )}
                   </div>
                </div>

                <div>
                   <div className="flex items-center gap-2 mb-2">
                     <Star size={16} className="text-warning" />
                     <h4 className="text-base font-semibold text-text-primary">{t('skills', 'Kỹ năng')} ưu tiên (Nice-to-have)</h4>
                   </div>
                   <div className="flex flex-wrap gap-2">
                     {selectedJDParsedData.niceToHaveSkills?.length > 0 ? (
                       selectedJDParsedData.niceToHaveSkills.map((sk, i) => (
                          <span key={i} className="px-2.5 py-1 bg-info/10 text-info border border-info/20 rounded text-xs font-medium">{sk}</span>
                       ))
                     ) : (
                       <span className="text-sm text-text-secondary italic">Không có dữ liệu</span>
                     )}
                   </div>
                </div>
              </div>

              {/* Detail Text Sections */}
              <div className="space-y-4 border-t border-border/50 pt-4">
                <div>
                   <div className="flex items-center gap-2 mb-2">
                     <Award size={16} className="text-primary" />
                     <h4 className="text-base font-semibold text-text-primary">Trách nhiệm công việc</h4>
                   </div>
                   <div className="bg-surface-2 p-4 rounded-lg border border-border/50">
                     <p className="text-sm text-text-secondary whitespace-pre-wrap leading-relaxed">{selectedJDParsedData.responsibilities || 'Không có dữ liệu'}</p>
                   </div>
                </div>
                <div>
                   <div className="flex items-center gap-2 mb-2">
                     <Building size={16} className="text-primary" />
                     <h4 className="text-base font-semibold text-text-primary">Đặc điểm công ty</h4>
                   </div>
                   <div className="bg-surface-2 p-4 rounded-lg border border-border/50">
                     <p className="text-sm text-text-secondary whitespace-pre-wrap leading-relaxed">{selectedJDParsedData.companyCharacteristics || 'Không có dữ liệu'}</p>
                   </div>
                </div>
              </div>

            </div>
          </div>
        </div>
      )}

      {/* Fast Check Result Modal */}
      {showFastCheckModal && currentFastCheckJD && fastCheckResults[currentFastCheckJD] && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-surface-1 rounded-xl shadow-xl w-full max-w-3xl overflow-hidden max-h-[90vh] flex flex-col animate-pageEntrance border border-border">
            <div className="flex items-center justify-between p-4 border-b border-border bg-gradient-to-r from-primary/10 to-transparent">
              <h3 className="text-lg font-semibold text-primary flex items-center gap-2">
                <div className="p-1.5 bg-primary text-white rounded-md shadow-sm">
                  <Sparkles size={18} />
                </div>
                Kết quả CV-JD Fast Check
              </h3>
              <button onClick={() => setShowFastCheckModal(false)} className="p-1 text-text-secondary hover:text-error hover:bg-error/10 rounded-md transition-colors">
                <X size={20} />
              </button>
            </div>
            <div className="p-4 overflow-y-auto flex-1 bg-surface-1 relative">
               <FastCheckResult result={fastCheckResults[currentFastCheckJD]} />
            </div>
          </div>
        </div>
      )}
    </UserLayout>
  );
}

export default CVJDManagementPage;
