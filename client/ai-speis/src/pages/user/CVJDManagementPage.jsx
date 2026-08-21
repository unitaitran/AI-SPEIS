import React, { useState, useEffect, useRef, useCallback } from 'react';
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
  Sparkles,
  Info,
  ChevronDown,
  ChevronUp,
  Copy,
  Check,
  TrendingUp
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
  const [isRawTextExpanded, setIsRawTextExpanded] = useState(false);
  const [isCopiedRawText, setIsCopiedRawText] = useState(false);
  const jdPollTimerRef = useRef(null);

  const handleCopyRawText = (text) => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setIsCopiedRawText(true);
    setTimeout(() => setIsCopiedRawText(false), 2000);
    notify.success('Đã sao chép nội dung JD vào clipboard!');
  };

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
          const errorMsg = statusResp?.errorMessage || statusResp?.ErrorMessage || statusResp?.data?.errorMessage || "Đây không phải là JD hợp lệ hoặc vị trí tuyển dụng chưa được hỗ trợ. Hãy thử lại.";
          notify.error(errorMsg, { title: 'Phân tích JD thất bại' });
        }
      } catch (err) {
        // keep polling
      }
    }, 3000);
  };

  const fetchCVHistory = useCallback(async () => {
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
  }, []);

  const fetchFastCheckResults = useCallback(async () => {
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
  }, []);

  const fetchJDHistory = useCallback(async () => {
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
  }, []);

  useEffect(() => {
    fetchCVHistory();
    fetchJDHistory();
    fetchFastCheckResults();
  }, [fetchCVHistory, fetchJDHistory, fetchFastCheckResults]);

  const formatDate = (dateStr) => {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    return date.toLocaleDateString('vi-VN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
  };

  // Convert numeric status enum back to string mapping if needed


  const handleCVClick = (id) => {
    navigate(USER_ROUTES.CV_DETAIL);
  };

  const handleViewJD = async (jdId, e) => {
    if (e) e.stopPropagation();
    try {
      const parsed = await jdService.getParsedData(jdId);
      if (parsed && parsed.data) {
        setSelectedJDParsedData(parsed.data);
        setSelectedJDId(jdId);
        setShowJDInfoModal(true);
      }
    } catch (err) {
      notify.error(`Lỗi lấy dữ liệu: ${err.message}`, { title: 'Không thể tải dữ liệu JD' });
    }
  };

  const handleJDClick = (id) => {
    handleViewJD(id);
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
      handleViewJD(jdId, e);
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

  // Delete JD State
  const [deleteJDConfirmId, setDeleteJDConfirmId] = useState(null);
  const [isDeletingJD, setIsDeletingJD] = useState(false);

  const handleDeleteJD = (id, e) => {
    e.stopPropagation();
    setDeleteJDConfirmId(id);
  };

  const handleConfirmDeleteJD = async () => {
    if (!deleteJDConfirmId) return;
    setIsDeletingJD(true);
    try {
      await jdService.deleteJD(deleteJDConfirmId);
      fetchJDHistory();
      notify.success('Job Description đã được xóa thành công.');
      setDeleteJDConfirmId(null);
    } catch (err) {
      notify.error(`Lỗi khi xóa JD: ${err.message}`, { title: 'Không thể xóa JD' });
    } finally {
      setIsDeletingJD(false);
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
                    <div className="mycv-info-actions border-l border-border pl-3 ml-2 flex items-center gap-2">
                      {getStatusString(jd.status) === 'ConfirmationRequired' || getStatusString(jd.status) === 'Confirmed' ? (
                        <>
                          {fastCheckResults[jd.jdFileId] ? (
                            <div 
                              className={`flex items-center gap-1.5 px-3 py-1.5 border rounded-lg text-xs font-semibold cursor-pointer transition-colors ${
                                fastCheckResults[jd.jdFileId].score >= 75
                                  ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30 hover:bg-emerald-500/20'
                                  : fastCheckResults[jd.jdFileId].score >= 50
                                  ? 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30 hover:bg-amber-500/20'
                                  : 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/30 hover:bg-rose-500/20'
                              }`}
                              onClick={(e) => {
                                e.stopPropagation();
                                setCurrentFastCheckJD(jd.jdFileId);
                                setShowFastCheckModal(true);
                              }}
                              title="Bấm để xem chi tiết kết quả Fast Check"
                            >
                              <Sparkles size={14} />
                              <span>{fastCheckResults[jd.jdFileId].score}% phù hợp</span>
                            </div>
                          ) : (
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
                          )}
                          <button className="mycv-btn mycv-btn--outline mycv-btn--sm py-1.5 px-3" onClick={(e) => handleJDActionClick(jd.jdFileId, e)}>
                            <Eye size={14} /> Xem
                          </button>
                        </>
                      ) : getStatusString(jd.status) === 'Processing' ? (
                        <button className="mycv-btn mycv-btn--warning mycv-btn--sm py-1.5 px-3" disabled>
                          <Loader2 size={14} className="animate-spin" /> Đang xử lí
                        </button>
                      ) : (
                        <button className="mycv-btn mycv-btn--primary mycv-btn--sm py-1.5 px-3" onClick={(e) => handleJDActionClick(jd.jdFileId, e)}>
                          <Sparkles size={14} /> Phân tích
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
              {/* Notice for supported roles */}
              <div className="bg-primary/10 border border-primary/20 rounded-lg p-3 text-xs text-primary mb-4 flex items-start gap-2">
                <Info size={18} className="shrink-0 mt-0.5" />
                <div>
                  <strong>Lưu ý về vị trí hỗ trợ:</strong> Hệ thống hiện chỉ hỗ trợ phân tích JD cho các vị trí: <strong>Backend Developer, Frontend Developer, Fullstack Developer, Mobile Developer, Business Analyst (BA), QA/Tester, DevOps Engineer, và Data Analyst</strong>.
                </div>
              </div>

              {/* Type selector */}
              <div className="flex bg-surface-2 p-1 rounded-lg mb-6">
                <button 
                  className={`flex-1 flex items-center justify-center gap-2 py-2 text-sm font-medium rounded-md transition-colors ${jdUploadType === 'file' ? 'bg-surface-1 text-primary shadow-sm' : 'text-text-secondary hover:text-text-primary'}`}
                  onClick={() => setJdUploadType('file')}
                >
                  <Upload size={16} /> {t('uploadPdf', 'Upload PDF')}
                </button>
                <button 
                  className={`flex-1 flex items-center justify-center gap-2 py-2 text-sm font-medium rounded-md transition-colors ${jdUploadType === 'text' ? 'bg-surface-1 text-primary shadow-sm' : 'text-text-secondary hover:text-text-primary'}`}
                  onClick={() => setJdUploadType('text')}
                >
                  <Type size={16} /> {t('pasteText', 'Dán Text')}
                </button>
              </div>

              {jdUploadType === 'file' ? (
                <div className="border-2 border-dashed border-border rounded-xl p-8 text-center bg-surface-2 hover:bg-surface-3 transition-colors">
                  <div className="bg-primary/10 text-primary w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-4">
                    <Upload size={24} />
                  </div>
                  <h4 className="text-text-primary font-medium mb-1">{t('dropJdFile', 'Chọn hoặc kéo thả file JD')}</h4>
                  <p className="text-sm text-text-secondary mb-4">{t('pdfSupport', 'Hỗ trợ định dạng PDF (tối đa 5MB)')}</p>
                  <label className="mycv-btn mycv-btn--primary mx-auto w-max cursor-pointer">
                    {t('selectFile', 'Chọn tệp')}
                    <input type="file" accept=".pdf" hidden onChange={(e) => { if(e.target.files.length) submitJDUpload(e.target.files[0]) }} />
                  </label>
                </div>
              ) : (
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-text-primary mb-2">{t('jdNameLabel', 'Tên Job Description')}</label>
                    <input 
                      type="text"
                      className="w-full bg-surface-2 border border-border rounded-lg p-3 text-text-primary focus:outline-none focus:border-primary"
                      placeholder={t('jdNamePlaceholder', 'VD: Frontend Developer tại công ty X...')}
                      value={jdTextName}
                      onChange={(e) => setJdTextName(e.target.value)}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-text-primary mb-2">{t('jdContentLabel', 'Nội dung JD')}</label>
                    <textarea 
                      className="w-full bg-surface-2 border border-border rounded-lg p-3 text-text-primary min-h-[200px] focus:outline-none focus:border-primary resize-none"
                      placeholder={t('jdContentPlaceholder', 'Dán nội dung Job Description vào đây...')}
                      value={jdText}
                      onChange={(e) => setJdText(e.target.value)}
                    ></textarea>
                  </div>
                </div>
              )}
            </div>
            
            <div className="p-4 border-t border-border flex justify-end gap-3 bg-surface-2/50">
              <button className="mycv-btn mycv-btn--outline" onClick={() => setShowJDModal(false)}>{t('cancel', 'Hủy')}</button>
              {jdUploadType === 'text' && (
                <button 
                  className="mycv-btn mycv-btn--primary" 
                  onClick={() => submitJDUpload()}
                  disabled={!jdText.trim() || !jdTextName.trim()}
                >
                  {t('confirmSave', 'Xác nhận lưu')}
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* JD Info Modal */}
      {showJDInfoModal && selectedJDParsedData && (() => {
        const selectedJD = jds.find(j => j.jdFileId === selectedJDId);
        const displayJobTitle = selectedJDParsedData.jobTitle || selectedJD?.jobTitle || t('untitledPosition', 'Vị trí chưa đặt tên');
        const displayFileName = selectedJDParsedData.fileName || selectedJD?.fileName || selectedJD?.jdTextName || t('defaultFileName', 'Tệp Job Description');
        
        return (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-3 sm:p-4 jd-modal-overlay">
            <div className="bg-surface-1 rounded-xl shadow-xl w-full max-w-4xl overflow-hidden max-h-[92vh] flex flex-col jd-modal-dialog border border-border relative">
              
              {/* Modal Header */}
              <div className="flex items-center justify-between px-5 py-4 border-b border-border bg-gradient-to-r from-primary/10 to-transparent">
                <div className="flex items-center gap-3 min-w-0">
                  <div className="p-2 bg-primary text-white rounded-lg shadow-sm flex-shrink-0">
                    <Briefcase size={20} />
                  </div>
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <h3 className="text-lg sm:text-xl font-bold text-text-primary truncate">
                        {displayJobTitle}
                      </h3>
                      {selectedJDParsedData.experienceLevel && (
                        <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-primary/10 text-primary border border-primary/20 flex items-center gap-1">
                          <TrendingUp size={12} />
                          {selectedJDParsedData.experienceLevel}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-2 text-xs text-text-muted mt-0.5 flex-wrap">
                      <span className="flex items-center gap-1 font-medium text-text-secondary">
                        <FileText size={13} className="text-primary flex-shrink-0" />
                        <span className="truncate max-w-[220px] sm:max-w-xs">{displayFileName}</span>
                      </span>
                      <span className="text-border">•</span>
                      <span className="flex items-center gap-1 text-primary font-medium">
                        <Sparkles size={12} />
                        {t('aiParsed', 'AI Đã phân tích')}
                      </span>
                    </div>
                  </div>
                </div>
                
                <button 
                  onClick={() => setShowJDInfoModal(false)} 
                  className="p-1.5 text-text-secondary hover:text-error hover:bg-error/10 rounded-lg transition-colors cursor-pointer flex-shrink-0 ml-2"
                  title={t('close', 'Đóng')}
                >
                  <X size={20} />
                </button>
              </div>
              
              {/* Modal Body */}
              <div className="p-4 sm:p-5 overflow-y-auto flex-1 space-y-4 bg-surface-1">
                
                {/* Top 2 Cards: Overview + Fast Check */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {/* Left: Role & Target Info */}
                  <div className="bg-surface-2 p-5 rounded-2xl border border-border/80 shadow-sm flex flex-col justify-between">
                    <div>
                      <div className="flex items-center gap-2 mb-4">
                        <Target size={22} className="text-primary" />
                        <h4 className="text-xl sm:text-2xl font-bold text-text-primary tracking-tight">
                          {t('roleAndLevel', 'Chức danh & Cấp bậc')}
                        </h4>
                      </div>

                      <div className="space-y-3">
                        <div className="flex items-center justify-between gap-3">
                          <span className="text-sm sm:text-base font-medium text-text-secondary">{t('position', 'Vị trí')}:</span>
                          <span className="text-sm sm:text-base font-semibold text-text-primary text-right truncate max-w-[240px]">
                            {selectedJDParsedData.jobTitle || t('undefined', 'Không xác định')}
                          </span>
                        </div>

                        <div className="flex items-center justify-between gap-3">
                          <span className="text-sm sm:text-base font-medium text-text-secondary">{t('level', 'Cấp bậc')}:</span>
                          <span className="text-sm sm:text-base font-semibold px-2.5 py-0.5 rounded-md bg-primary/10 text-primary">
                            {selectedJDParsedData.experienceLevel || t('undefined', 'Không xác định')}
                          </span>
                        </div>

                        {selectedJDParsedData.roleTarget && (
                          <div className="flex items-center justify-between gap-3">
                            <span className="text-sm sm:text-base font-medium text-text-secondary">{t('field', 'Lĩnh vực')}:</span>
                            <span className="text-sm sm:text-base font-semibold text-text-primary">
                              {selectedJDParsedData.roleTarget}
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Right: CV-JD Fast Check Card */}
                  <div className="bg-surface-2 p-5 rounded-2xl border border-border/80 shadow-sm flex flex-col justify-between relative overflow-hidden">
                    {/* Top Row: Title + Enlarged Mascot Box */}
                    <div className="flex items-center justify-between gap-4">
                      <div>
                        <h4 className="text-xl sm:text-2xl font-bold text-text-primary tracking-tight">
                          {t('fastCheckTitle', 'CV–JD Fast Check')}
                        </h4>
                      </div>

                      {/* Enlarged Mascot Thumbnail */}
                      <div className="w-20 h-20 sm:w-24 sm:h-24 rounded-2xl overflow-hidden border border-border/80 shadow-sm bg-white flex-shrink-0 flex items-center justify-center">
                        <img 
                          src="/studying_mascot.jpg" 
                          alt="AI SPEIS Studying Mascot" 
                          className="w-full h-full object-cover"
                        />
                      </div>
                    </div>

                    {/* Bottom Row: Score & Detail Button */}
                    {fastCheckResults[selectedJDId] ? (
                      <div className="flex items-end justify-between gap-3 mt-3">
                        <div>
                          <div className="text-xs sm:text-sm font-semibold text-text-secondary mb-0.5">
                            {t('matchRate', 'Độ phù hợp')}
                          </div>
                          <div className="flex items-baseline gap-2.5">
                            <span className="text-3xl sm:text-4xl font-extrabold text-emerald-500 tracking-tight">
                              {fastCheckResults[selectedJDId].score}%
                            </span>
                            <span className="text-sm sm:text-base font-semibold text-text-primary">
                              {fastCheckResults[selectedJDId].suitabilityLevel}
                            </span>
                          </div>
                        </div>

                        <button 
                          onClick={(e) => { 
                            e.stopPropagation(); 
                            setShowJDInfoModal(false); 
                            handleFastCheckClick(selectedJDId, e); 
                          }} 
                          className="px-4 py-2 rounded-xl border border-border bg-surface-1 hover:border-primary hover:bg-primary/5 text-text-primary text-xs sm:text-sm font-semibold transition-all duration-200 shadow-sm cursor-pointer whitespace-nowrap"
                        >
                          {t('viewDetails', 'Xem chi tiết')}
                        </button>
                      </div>
                    ) : (
                      <div className="flex items-end justify-between gap-3 mt-3">
                        <div>
                          <div className="text-xs sm:text-sm font-medium text-text-secondary">
                            {t('notCheckedYet', 'Chưa đối chiếu với CV')}
                          </div>
                          <div className="text-xs text-text-muted mt-0.5">
                            {t('clickToAnalyze', 'Bấm để AI kiểm tra ngay')}
                          </div>
                        </div>

                        <button 
                          onClick={(e) => { 
                            e.stopPropagation(); 
                            handleFastCheckClick(selectedJDId, e); 
                          }} 
                          disabled={isFastChecking[selectedJDId] || !cvs[0]} 
                          className="px-4 py-2 rounded-xl bg-primary text-white hover:bg-primary-dark text-xs sm:text-sm font-semibold transition-all duration-200 shadow-sm cursor-pointer whitespace-nowrap disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5"
                        >
                          {isFastChecking[selectedJDId] ? (
                            <>
                              <Loader2 size={14} className="animate-spin" />
                              <span>{t('analyzing', 'Đang phân tích')}...</span>
                            </>
                          ) : (
                            <>
                              <CheckCircle2 size={14} />
                              <span>{t('checkNow', 'Kiểm tra ngay')}</span>
                            </>
                          )}
                        </button>
                      </div>
                    )}
                  </div>
                </div>

                {/* Skills Row: Left (Required) + Right (Nice-to-have) */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {/* Left: Required Skills */}
                  <div className="space-y-2">
                    <div className="flex items-center gap-2 mb-2">
                      <CheckSquare size={16} className="text-success" />
                      <h4 className="text-sm font-semibold text-text-primary">{t('requiredSkills', 'Kỹ năng yêu cầu (Bắt buộc)')}</h4>
                      {selectedJDParsedData.requiredSkills?.length > 0 && (
                        <span className="text-[11px] font-semibold px-2 py-0.2 rounded bg-success-light text-success">
                          {selectedJDParsedData.requiredSkills.length}
                        </span>
                      )}
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      {selectedJDParsedData.requiredSkills?.length > 0 ? (
                        selectedJDParsedData.requiredSkills.map((sk, i) => (
                          <span 
                            key={i} 
                            className="px-2.5 py-1 bg-primary/10 text-primary border border-primary/20 rounded-md text-xs font-medium"
                          >
                            {sk}
                          </span>
                        ))
                      ) : (
                        <span className="text-xs text-text-muted italic">{t('noData', 'Không có dữ liệu')}</span>
                      )}
                    </div>
                  </div>

                  {/* Right: Nice-to-have Skills */}
                  <div className="space-y-2">
                    <div className="flex items-center gap-2 mb-2">
                      <Star size={16} className="text-warning" />
                      <h4 className="text-sm font-semibold text-text-primary">{t('niceToHaveSkills', 'Kỹ năng ưu tiên (Nice-to-have)')}</h4>
                      {selectedJDParsedData.niceToHaveSkills?.length > 0 && (
                        <span className="text-[11px] font-semibold px-2 py-0.2 rounded bg-warning-light text-warning">
                          {selectedJDParsedData.niceToHaveSkills.length}
                        </span>
                      )}
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      {selectedJDParsedData.niceToHaveSkills?.length > 0 ? (
                        selectedJDParsedData.niceToHaveSkills.map((sk, i) => (
                          <span 
                            key={i} 
                            className="px-2.5 py-1 bg-info/10 text-info border border-info/20 rounded-md text-xs font-medium"
                          >
                            {sk}
                          </span>
                        ))
                      ) : (
                        <span className="text-xs text-text-muted italic">{t('noData', 'Không có dữ liệu')}</span>
                      )}
                    </div>
                  </div>
                </div>

                {/* Responsibilities & Company (Full width) */}
                <div className="space-y-3 border-t border-border/50 pt-3">
                  {/* Responsibilities */}
                  <div>
                    <div className="flex items-center gap-2 mb-1.5">
                      <Award size={16} className="text-primary" />
                      <h4 className="text-sm font-semibold text-text-primary">{t('responsibilities', 'Trách nhiệm công việc')}</h4>
                    </div>
                    <div className="bg-surface-2 p-3.5 rounded-lg border border-border/60 border-l-4 border-l-primary">
                      <p className="text-sm text-text-secondary whitespace-pre-wrap leading-relaxed">
                        {selectedJDParsedData.responsibilities || t('noData', 'Không có dữ liệu')}
                      </p>
                    </div>
                  </div>

                  {/* Company Culture */}
                  <div>
                    <div className="flex items-center gap-2 mb-1.5">
                      <Building size={16} className="text-primary" />
                      <h4 className="text-sm font-semibold text-text-primary">{t('companyCulture', 'Đặc điểm công ty')}</h4>
                    </div>
                    <div className="bg-surface-2 p-3.5 rounded-lg border border-border/60">
                      <p className="text-sm text-text-secondary whitespace-pre-wrap leading-relaxed">
                        {selectedJDParsedData.companyCharacteristics || t('noData', 'Không có dữ liệu')}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Raw JD Text Collapsible */}
                {selectedJDParsedData.rawText && (
                  <div className="bg-surface-2 rounded-lg border border-border/60 overflow-hidden">
                    <div 
                      className="flex items-center justify-between p-3 bg-surface-1 cursor-pointer select-none hover:bg-surface-2 transition-colors" 
                      onClick={() => setIsRawTextExpanded(!isRawTextExpanded)}
                    >
                      <div className="flex items-center gap-2">
                        <FileText size={16} className="text-primary" />
                        <span className="text-sm font-semibold text-text-primary">{t('rawJdContent', 'Nội dung JD gốc')}</span>
                      </div>
                      <div className="flex items-center gap-2">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleCopyRawText(selectedJDParsedData.rawText);
                          }}
                          className="text-xs px-2.5 py-1 rounded border border-border hover:bg-surface-1 text-text-secondary hover:text-text-primary transition-colors flex items-center gap-1 cursor-pointer"
                          title="Sao chép toàn bộ nội dung JD"
                        >
                          {isCopiedRawText ? <Check size={13} className="text-success" /> : <Copy size={13} />}
                          {isCopiedRawText ? t('copied', 'Đã sao chép') : t('copy', 'Sao chép')}
                        </button>
                        <span className="text-text-secondary p-0.5">
                          {isRawTextExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                        </span>
                      </div>
                    </div>
                    {isRawTextExpanded && (
                      <div className="p-3 border-t border-border/60 bg-surface-1">
                        <pre className="jd-raw-textarea text-xs text-text-secondary whitespace-pre-wrap font-mono max-h-56 overflow-y-auto leading-relaxed">
                          {selectedJDParsedData.rawText}
                        </pre>
                      </div>
                    )}
                  </div>
                )}

              </div>
            </div>
          </div>
        );
      })()}

      {/* Fast Check Result Modal */}
      {showFastCheckModal && currentFastCheckJD && fastCheckResults[currentFastCheckJD] && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-surface-1 rounded-xl shadow-xl w-full max-w-3xl overflow-hidden max-h-[90vh] flex flex-col animate-pageEntrance border border-border">
            <div className="flex items-center justify-between p-4 border-b border-border bg-gradient-to-r from-primary/10 to-transparent">
              <h3 className="text-lg font-semibold text-primary flex items-center gap-2">
                <div className="p-1.5 bg-primary text-white rounded-md shadow-sm">
                  <Sparkles size={18} />
                </div>
                {t('fastCheckResultTitle', 'Kết quả CV-JD Fast Check')}
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

      {/* Confirm Delete JD Modal */}
      {deleteJDConfirmId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-surface-1 rounded-xl shadow-xl w-full max-w-md overflow-hidden animate-pageEntrance border border-border">
            {/* Modal Header */}
            <div className="flex items-center justify-between p-4 border-b border-border bg-gradient-to-r from-error/10 to-transparent">
              <h3 className="text-lg font-semibold text-error flex items-center gap-2">
                <div className="p-1.5 bg-error/10 text-error rounded-md">
                  <AlertCircle size={20} />
                </div>
                Xác nhận xóa Job Description
              </h3>
              <button 
                onClick={() => setDeleteJDConfirmId(null)} 
                className="p-1 text-text-secondary hover:text-error hover:bg-error/10 rounded-md transition-colors"
                disabled={isDeletingJD}
              >
                <X size={20} />
              </button>
            </div>
            
            {/* Modal Body */}
            <div className="p-6 space-y-2 bg-surface-1">
              <p className="text-base font-medium text-text-primary">
                Bạn có chắc chắn muốn xóa JD này không?
              </p>
              <p className="text-sm text-text-secondary leading-relaxed">
                Hành động này sẽ xóa vĩnh viễn Job Description khỏi tài khoản của bạn và không thể hoàn tác.
              </p>
            </div>

            {/* Modal Footer */}
            <div className="p-4 border-t border-border flex justify-end gap-3 bg-surface-2/50">
              <button 
                className="mycv-btn mycv-btn--outline" 
                onClick={() => setDeleteJDConfirmId(null)}
                disabled={isDeletingJD}
              >
                Hủy
              </button>
              <button 
                className="mycv-btn bg-error text-white hover:bg-error/90 border-transparent flex items-center gap-2 cursor-pointer" 
                onClick={handleConfirmDeleteJD}
                disabled={isDeletingJD}
              >
                {isDeletingJD ? <Loader2 size={16} className="animate-spin" /> : <Trash2 size={16} />}
                {isDeletingJD ? 'Đang xóa...' : 'Xóa JD'}
              </button>
            </div>
          </div>
        </div>
      )}
    </UserLayout>
  );
}

export default CVJDManagementPage;
