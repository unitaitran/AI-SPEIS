import React, { useEffect, useState, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronDown, Ticket, User, LogOut, Settings, Globe, Loader2, Crown } from 'lucide-react';
import { navigate } from '../../../routes/navigation';
import { USER_ROUTES } from '../../../routes/routePaths';
import { getAvatarUrl } from '../../../routes/auth';
import cvService from '../../../services/CVService';
import jdService from '../../../services/JDService';
import interviewSessionService from '../../../services/InterviewSessionService';
import { NotificationBell } from '../../../features/notifications/NotificationBell';
import { NOTIFICATION_STATE_RESET_EVENT } from '../../../features/notifications/NotificationProvider';

function UserTopbar({ onMenuClick, onOpenProfile, user: propUser }) {
  const { t, i18n } = useTranslation('dashboard');
  const [user, setUser] = useState(null);
  const [remainingInterviewQuota, setRemainingInterviewQuota] = useState(null);
  const [maxInterviewQuota, setMaxInterviewQuota] = useState(null);
  const [planName, setPlanName] = useState('Free');

  useEffect(() => {
    if (propUser) {
      setUser(propUser);
    } else {
      const userStr = localStorage.getItem('user');
      if (userStr) {
        try {
          setUser(JSON.parse(userStr));
        } catch (e) {
          console.error('Failed to parse user', e);
        }
      }
    }
  }, [propUser]);

  useEffect(() => {
    let isMounted = true;

    const loadQuota = async () => {
      try {
        const quota = await interviewSessionService.getQuota();
        if (isMounted) {
          setRemainingInterviewQuota(quota.remainingInterviewQuota);
          setMaxInterviewQuota(quota.maxInterviewQuota ?? null);
          const isPrem = Boolean(quota.planName && String(quota.planName).toLowerCase() !== 'free');
          setPlanName(quota.planName || 'Free');

          const userStr = localStorage.getItem('user');
          if (userStr) {
            try {
              const u = JSON.parse(userStr);
              if (u.isPremium !== isPrem) {
                u.isPremium = isPrem;
                localStorage.setItem('user', JSON.stringify(u));
              }
            } catch (e) {}
          }
        }
      } catch {
        if (isMounted) {
          const isUserPremium = Boolean(propUser?.isPremium ?? user?.isPremium);
          const fallbackRemaining = propUser?.remainingInterviewQuota ?? user?.remainingInterviewQuota ?? (isUserPremium ? 15 : 3);
          setRemainingInterviewQuota(fallbackRemaining);
          setMaxInterviewQuota(isUserPremium ? 15 : 5);
          setPlanName(isUserPremium ? 'Premium' : 'Free');
        }
      }
    };

    const handleQuotaChanged = (event) => {
      const nextQuota = event.detail?.remainingInterviewQuota;
      const nextMaxQuota = event.detail?.maxInterviewQuota;
      const nextPlanName = event.detail?.planName;

      if (Number.isInteger(nextQuota)) {
        setRemainingInterviewQuota(nextQuota);
        if (Number.isInteger(nextMaxQuota)) setMaxInterviewQuota(nextMaxQuota);
        if (typeof nextPlanName === 'string' && nextPlanName.trim()) setPlanName(nextPlanName);
      } else loadQuota();
    };

    loadQuota();
    window.addEventListener('interview:quota-changed', handleQuotaChanged);
    return () => {
      isMounted = false;
      window.removeEventListener('interview:quota-changed', handleQuotaChanged);
    };
  }, [propUser, user?.remainingInterviewQuota, user?.isPremium]);

  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const [isProcessingBg, setIsProcessingBg] = useState(false);

  useEffect(() => {
    let intervalId;
    const checkProcessingStatus = async () => {
      try {
        let processing = false;
        
        // Check CV
        const cvRes = await cvService.getMyCVHistory(1, 1);
        if (cvRes?.items?.length > 0) {
          const cvStatus = cvRes.items[0].status; // 0: Pending, 1: Processing
          if (cvStatus === 0 || cvStatus === 1) processing = true;
        }
        
        // Check JD
        if (!processing) {
          const jdRes = await jdService.getMyJDHistory(1, 1);
          if (jdRes?.items?.length > 0) {
            const jdStatus = jdRes.items[0].status;
            if (jdStatus === 0 || jdStatus === 1) processing = true;
          }
        }
        
        setIsProcessingBg(processing);
      } catch (e) {
        // ignore errors
      }
    };

    checkProcessingStatus();
    intervalId = setInterval(checkProcessingStatus, 5000);
    return () => clearInterval(intervalId);
  }, []);

  useEffect(() => {
    if (!isDropdownOpen) return;

    const handleScroll = () => {
      setIsDropdownOpen(false);
    };

    window.addEventListener('scroll', handleScroll, { capture: true, passive: true });
    return () => {
      window.removeEventListener('scroll', handleScroll, { capture: true });
    };
  }, [isDropdownOpen]);

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.dispatchEvent(new Event(NOTIFICATION_STATE_RESET_EVENT));
    window.location.href = '/#login';
  };

  const toggleLanguage = () => {
    const nextLang = i18n.language.startsWith('vi') ? 'en' : 'vi';
    i18n.changeLanguage(nextLang);
  };

  const isLastAttempt = remainingInterviewQuota === 1;
  const isPaidPlan = Boolean(planName && String(planName).toLowerCase() !== 'free');

  return (
    <header className="h-[85px] bg-surface-2 border-b border-border flex items-center justify-between px-6 shrink-0 z-10 sticky top-0">

      {/* Spacer for desktop since logo is in sidebar */}
      <div className="hidden lg:block flex-1"></div>

      <div className="flex items-center space-x-4 ml-auto">
        {/* Quota Badge */}
        <div className="hidden sm:flex items-center bg-gradient-to-r from-primary-light to-primary-xlight border border-primary-light rounded-full px-3 py-1.5 text-sm font-semibold text-primary-dark shadow-sm">
          <Ticket size={16} className="text-primary-dark mr-2" />
          <span>
            {remainingInterviewQuota ?? '—'} / {maxInterviewQuota ?? '—'} {t('topbar.quota_remaining', 'Interviews Left')}
          </span>
        </div>

        <div className={`hidden sm:inline-flex items-center rounded-full border px-3 py-1 text-xs font-bold shadow-sm ${isPaidPlan ? 'border-[#FFD700] bg-[#FFF8DC] text-[#DAA520]' : 'border-border bg-surface-1 text-text-secondary'}`}>
          {isPaidPlan ? (
            <div className="flex items-center space-x-1">
              <Crown size={14} className="text-[#FFD700]" />
              <span className="bg-gradient-to-r from-[#FFD700] to-[#FFA500] text-transparent bg-clip-text">{planName}</span>
            </div>
          ) : (
            planName
          )}
        </div>

        {isLastAttempt && (
          <div className="hidden md:flex items-center rounded-full border border-warning bg-warning-light px-3 py-1 text-xs font-semibold text-warning">
            1 attempt remaining
          </div>
        )}

        {/* Processing Indicator */}
        {isProcessingBg && (
          <div className="hidden sm:flex items-center text-primary-dark bg-primary-xlight/50 px-3 py-1.5 rounded-full border border-primary-light/50 shadow-sm animate-pulse" title="AI đang phân tích tài liệu">
            <Loader2 size={16} className="animate-spin mr-2" />
            <span className="text-xs font-medium">Đang phân tích</span>
          </div>
        )}

        <NotificationBell variant="user" />

        <div className="w-px h-6 bg-border mx-1"></div>

        {/* Profile Dropdown */}
        <div className="relative" ref={dropdownRef}>
          <button
            className="flex items-center space-x-2 p-1 pl-2 pr-3 hover:bg-surface-3 rounded-full transition-colors border border-transparent hover:border-border group"
            onClick={() => setIsDropdownOpen(!isDropdownOpen)}
          >
            <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary to-primary-dark flex items-center justify-center text-white shadow-sm overflow-hidden">
              {user && user.avatar ? (
                <img src={getAvatarUrl(user.avatar)} alt="Avatar" className="w-full h-full object-cover" />
              ) : (
                <User size={16} />
              )}
            </div>
            <span className="text-sm font-bold text-text-primary hidden sm:block group-hover:text-primary-dark transition-colors">
              {user ? user.fullName : 'User Name'}
            </span>
            <ChevronDown size={16} className={`text-text-secondary group-hover:text-primary-dark transition-transform ${isDropdownOpen ? 'rotate-180' : ''}`} />
          </button>

          {/* Dropdown Menu */}
          {isDropdownOpen && (
            <div className="absolute right-0 mt-2 w-48 bg-surface-1 border border-border rounded-xl shadow-lg py-2 z-50 animate-in fade-in slide-in-from-top-2">
              <div className="px-4 py-2 border-b border-border mb-2">
                <p className="text-sm font-semibold text-text-primary line-clamp-1">{user ? user.fullName : 'User Name'}</p>
                <p className="text-xs text-text-secondary line-clamp-1">{user ? user.email : ''}</p>
              </div>
              <button
                className="w-full flex items-center px-4 py-2 text-sm text-text-secondary hover:text-primary-dark hover:bg-primary-xlight transition-colors cursor-pointer"
                onClick={() => {
                  setIsDropdownOpen(false);
                  if (onOpenProfile) {
                    onOpenProfile();
                  } else {
                    navigate(USER_ROUTES.PROFILE);
                  }
                }}
              >
                <Settings size={16} className="mr-3" />
                {t('topbar.profile_info', 'Thông tin cá nhân')}
              </button>

              {/* Language Switcher Button */}
              <button
                className="w-full flex items-center px-4 py-2 text-sm text-text-secondary hover:text-primary-dark hover:bg-primary-xlight transition-colors cursor-pointer mt-1"
                onClick={toggleLanguage}
              >
                <Globe size={16} className="mr-3" />
                {i18n.language.startsWith('vi') ? 'English (EN)' : 'Tiếng Việt (VI)'}
              </button>

              <button
                className="w-full flex items-center px-4 py-2 text-sm text-error hover:bg-error/10 transition-colors mt-1 cursor-pointer"
                onClick={handleLogout}
              >
                <LogOut size={16} className="mr-3" />
                {t('topbar.logout', 'Đăng xuất')}
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}

export default UserTopbar;
