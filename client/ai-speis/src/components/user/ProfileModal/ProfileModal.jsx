import React, { useEffect, useState, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { X, Lock, Camera, User, CheckCircle2, AlertCircle, Pencil, ArrowLeft } from 'lucide-react';
import { ENDPOINTS } from '../../../config/api';
import Input from '../../UI/Input';
import Button from '../../UI/Button';
import { getAvatarUrl } from '../../../routes/auth';

function ProfileModal({ onClose = () => {}, onUserUpdated = () => {} }) {
  const { t } = useTranslation('login');
  const [isChangePasswordMode, setIsChangePasswordMode] = useState(false);
  
  // Profile states
  const [fullName, setFullName] = useState('Nguyễn Minh Anh');
  const [originalFullName, setOriginalFullName] = useState('Nguyễn Minh Anh');
  const [email, setEmail] = useState('anh.nguyen@example.com');
  const [originalEmail, setOriginalEmail] = useState('anh.nguyen@example.com');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [originalPhoneNumber, setOriginalPhoneNumber] = useState('');
  const [jobTitle, setJobTitle] = useState('Frontend Developer');
  const [originalJobTitle, setOriginalJobTitle] = useState('Frontend Developer');
  const [interviewLanguage, setInterviewLanguage] = useState('en');
  const [originalInterviewLanguage, setOriginalInterviewLanguage] = useState('en');
  const [avatar, setAvatar] = useState('');
  const [avatarError, setAvatarError] = useState(false);
  const [hasPassword, setHasPassword] = useState(true);
  const [isPhoneEditable, setIsPhoneEditable] = useState(false);

  useEffect(() => {
    setAvatarError(false);
  }, [avatar]);

  // Password change states
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  // Status states
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const fileInputRef = useRef(null);

  const isDirty = fullName !== originalFullName
    || email !== originalEmail
    || phoneNumber !== originalPhoneNumber
    || jobTitle !== originalJobTitle
    || interviewLanguage !== originalInterviewLanguage;

  // Fetch current user details on mount
  useEffect(() => {
    const fetchProfile = async () => {
      setLoading(true);
      setError('');
      try {
        const token = localStorage.getItem('token');
        if (!token) {
          setLoading(false);
          return;
        }

        const response = await fetch(ENDPOINTS.GET_PROFILE, {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          }
        });

        if (!response.ok) {
          throw new Error(t('profile_error_load', 'Không thể tải thông tin cá nhân.'));
        }

        const data = await response.json();
        setFullName(data.fullName || 'Nguyễn Minh Anh');
        setOriginalFullName(data.fullName || 'Nguyễn Minh Anh');
        setEmail(data.email || 'anh.nguyen@example.com');
        setOriginalEmail(data.email || 'anh.nguyen@example.com');
        setPhoneNumber(data.phoneNumber || '');
        setOriginalPhoneNumber(data.phoneNumber || '');
        setJobTitle(data.jobTitle || 'Frontend Developer');
        setOriginalJobTitle(data.jobTitle || 'Frontend Developer');
        setInterviewLanguage(data.interviewLanguage || 'en');
        setOriginalInterviewLanguage(data.interviewLanguage || 'en');
        setHasPassword(data.hasPassword !== false);

        // Google account with no phone number yet is allowed to add phone number once
        const isGoogleAccount = data.hasPassword === false;
        const hasNoPhone = !data.phoneNumber || data.phoneNumber.trim() === '';
        setIsPhoneEditable(isGoogleAccount && hasNoPhone);

        if (data.imageUrl) {
          setAvatar(data.imageUrl);
          
          // Keep localStorage user details in sync with the fetched profile
          const userStr = localStorage.getItem('user');
          if (userStr) {
            try {
              const userData = JSON.parse(userStr);
              if (userData.avatar !== data.imageUrl || userData.fullName !== data.fullName) {
                const updatedUser = {
                  ...userData,
                  fullName: data.fullName,
                  avatar: data.imageUrl
                };
                localStorage.setItem('user', JSON.stringify(updatedUser));
                if (onUserUpdated) {
                  onUserUpdated(updatedUser);
                }
              }
            } catch (e) {
              console.error('Failed to sync user storage', e);
            }
          }
        } else {
          // Load avatar from localStorage user object if it exists
          const userStr = localStorage.getItem('user');
          if (userStr) {
            const userData = JSON.parse(userStr);
            if (userData.avatar) {
              setAvatar(userData.avatar);
            }
          }
        }
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    fetchProfile();
  }, [t, onUserUpdated]);

  useEffect(() => {
    const fullNameInput = document.getElementById('fullName');
    const emailInput = document.getElementById('email');
    const phoneInput = document.getElementById('phoneNumber');
    const jobTitleInput = document.getElementById('jobTitle');
    const interviewLanguageInput = document.getElementById('interviewLanguage');

    if (fullNameInput && fullNameInput.value !== fullName) {
      fullNameInput.value = fullName;
    }
    if (emailInput && emailInput.value !== email) {
      emailInput.value = email;
    }
    if (phoneInput && phoneInput.value !== (phoneNumber || '')) {
      phoneInput.value = phoneNumber || '';
    }
    if (jobTitleInput && jobTitleInput.value !== jobTitle) {
      jobTitleInput.value = jobTitle;
    }
    if (interviewLanguageInput && interviewLanguageInput.value !== interviewLanguage) {
      interviewLanguageInput.value = interviewLanguage;
    }
  }, [fullName, email, phoneNumber, jobTitle, interviewLanguage]);

  const handleAvatarChange = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      setError(t('invalid_image_type', 'Vui lòng chọn một tệp hình ảnh hợp lệ.'));
      return;
    }

    setLoading(true);
    setError('');
    setSuccessMsg('');

    try {
      const token = localStorage.getItem('token');
      const formData = new FormData();
      formData.append('file', file);

      const response = await fetch(ENDPOINTS.UPDATE_AVATAR, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`
        },
        body: formData
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.detail || data.message || t('avatar_upload_failed', 'Tải ảnh đại diện thất bại.'));
      }

      const newImageUrl = data.imageUrl;
      setAvatar(newImageUrl);

      // Cập nhật local storage
      const userStr = localStorage.getItem('user');
      if (userStr) {
        const userData = JSON.parse(userStr);
        const updatedUser = {
          ...userData,
          avatar: newImageUrl
        };
        localStorage.setItem('user', JSON.stringify(updatedUser));
        if (onUserUpdated) {
          onUserUpdated(updatedUser);
        }
      }

      setSuccessMsg(t('avatar_success_msg', 'Cập nhật ảnh đại diện thành công.'));
      setTimeout(() => setSuccessMsg(''), 3000);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleSaveProfile = async (e) => {
    e.preventDefault();

    const form = e.currentTarget;
    const fullNameInput = form.querySelector('#fullName');
    const emailInput = form.querySelector('#email');
    const phoneInput = form.querySelector('#phoneNumber');
    const nextFullName = fullNameInput?.value ?? fullName;
    const nextEmail = emailInput?.value ?? email;
    const nextPhoneNumber = phoneInput?.value ?? phoneNumber;

    if (!nextFullName.trim()) {
      setError(t('profile_name_required', 'Họ và tên là bắt buộc.'));
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(nextEmail.trim())) {
      setError('Email chưa đúng định dạng.');
      return;
    }

    // Phone format check if it's editable and inputted
    if (isPhoneEditable && nextPhoneNumber && nextPhoneNumber.trim()) {
      if (!/^0\d{9}$/.test(nextPhoneNumber.trim())) {
        setError(t('phone_invalid_format', 'Số điện thoại phải có 10 chữ số và bắt đầu bằng số 0.'));
        return;
      }
    }

    setLoading(true);
    setError('');
    setSuccessMsg('');

    const token = localStorage.getItem('token');

    if (!token) {
      const normalizedFullName = nextFullName.trim();
      const normalizedEmail = nextEmail.trim();
      const normalizedPhoneNumber = nextPhoneNumber ? nextPhoneNumber.trim() : '';

      setFullName(normalizedFullName);
      setOriginalFullName(normalizedFullName);
      setEmail(normalizedEmail);
      setOriginalEmail(normalizedEmail);
      setPhoneNumber(normalizedPhoneNumber);
      setOriginalPhoneNumber(normalizedPhoneNumber);
      setJobTitle(jobTitle.trim());
      setOriginalJobTitle(jobTitle.trim());
      setInterviewLanguage(interviewLanguage);
      setOriginalInterviewLanguage(interviewLanguage);
      setIsPhoneEditable(false);
      setSuccessMsg('Đã lưu thay đổi hồ sơ');
      setTimeout(() => setSuccessMsg(''), 3000);
      setLoading(false);
      return;
    }

    try {
      const response = await fetch(ENDPOINTS.UPDATE_PROFILE, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          fullName: nextFullName.trim(),
          phoneNumber: nextPhoneNumber ? nextPhoneNumber.trim() : null,
          jobTitle: jobTitle.trim(),
          interviewLanguage
        })
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.detail || data.message || t('profile_save_failed', 'Cập nhật hồ sơ thất bại.'));
      }

      setFullName(data.fullName || nextFullName);
      setOriginalFullName(data.fullName || nextFullName);
      setEmail(data.email || nextEmail);
      setOriginalEmail(data.email || nextEmail);
      setPhoneNumber(data.phoneNumber || nextPhoneNumber);
      setOriginalPhoneNumber(data.phoneNumber || nextPhoneNumber);
      setJobTitle(data.jobTitle || jobTitle);
      setOriginalJobTitle(data.jobTitle || jobTitle);
      setInterviewLanguage(data.interviewLanguage || interviewLanguage);
      setOriginalInterviewLanguage(data.interviewLanguage || interviewLanguage);
      
      // Once successfully saved, phone number is no longer editable
      if (data.phoneNumber) {
        setIsPhoneEditable(false);
      }

      // Update local storage user state
      const userStr = localStorage.getItem('user');
      if (userStr) {
        const userData = JSON.parse(userStr);
        const updatedUser = {
          ...userData,
          fullName: data.fullName || nextFullName,
          avatar: avatar,
          jobTitle: data.jobTitle || jobTitle,
          interviewLanguage: data.interviewLanguage || interviewLanguage
        };
        localStorage.setItem('user', JSON.stringify(updatedUser));
        if (onUserUpdated) {
          onUserUpdated(updatedUser);
        }
      }

      setSuccessMsg('Đã lưu thay đổi hồ sơ');
      setTimeout(() => setSuccessMsg(''), 3000);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleChangePassword = async (e) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      setError(t('profile_pwd_mismatch', 'Mật khẩu xác nhận không khớp.'));
      return;
    }

    setLoading(true);
    setError('');
    setSuccessMsg('');

    try {
      const token = localStorage.getItem('token');
      const response = await fetch(ENDPOINTS.CHANGE_PASSWORD, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          currentPassword: hasPassword ? currentPassword : null,
          newPassword,
          confirmNewPassword: confirmPassword
        })
      });

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.detail || data.message || t('profile_pwd_failed', 'Thay đổi mật khẩu thất bại.'));
      }

      setSuccessMsg(hasPassword 
        ? t('profile_pwd_success', 'Mật khẩu của bạn đã được cập nhật thành công.')
        : t('profile_pwd_created_success', 'Mật khẩu đã được thiết lập thành công.')
      );
      
      setHasPassword(true);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      
      // Go back to profile view after 2 seconds
      setTimeout(() => {
        setIsChangePasswordMode(false);
        setSuccessMsg('');
      }, 2000);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div 
      onClick={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
      className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-in fade-in duration-200"
    >
      <div className="bg-surface-2 border border-border w-full max-w-md rounded-2xl shadow-xl overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-300">
        
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-border">
          <div className="flex items-center gap-2">
            {isChangePasswordMode && (
              <button
                type="button"
                onClick={() => {
                  setError('');
                  setSuccessMsg('');
                  setIsChangePasswordMode(false);
                }}
                className="text-text-secondary hover:text-text-primary hover:bg-surface-3 p-1.5 rounded-full transition-colors focus:outline-none cursor-pointer flex items-center justify-center"
                aria-label="Go back"
              >
                <ArrowLeft size={18} />
              </button>
            )}
            <h3 className="text-lg font-bold text-text-primary">
              {isChangePasswordMode
                ? (hasPassword ? 'Đổi mật khẩu' : 'Tạo mật khẩu')
                : 'Hồ sơ cá nhân'}
            </h3>
          </div>
          <button 
            onClick={onClose}
            className="text-text-secondary hover:text-text-primary hover:bg-surface-3 p-1.5 rounded-full transition-colors focus:outline-none cursor-pointer"
            aria-label="Close modal"
          >
            <X size={18} />
          </button>
        </div>

        {/* Alerts */}
        <div className="px-6 pt-4 empty:hidden">
          {error && (
            <div className="p-3 bg-error-light border border-error rounded-xl text-error text-[13px] flex items-start gap-2.5 animate-in fade-in duration-200">
              <AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}
          {successMsg && (
            <div className="p-3 bg-success-light border border-success rounded-xl text-success text-[13px] flex items-start gap-2.5 animate-in fade-in duration-200">
              <CheckCircle2 size={16} className="shrink-0 mt-0.5" />
              <span>{successMsg}</span>
            </div>
          )}
        </div>

        {/* Modal Body */}
        {!isChangePasswordMode ? (
          /* Profile View */
          <form onSubmit={handleSaveProfile} className="flex-1 flex flex-col">
            <div className="p-6 flex-1 flex flex-col gap-5">
              
              {/* Avatar section */}
              <div className="flex flex-col items-center gap-2">
                <div className="relative w-24 h-24 rounded-full border border-border bg-surface-1 flex items-center justify-center overflow-hidden group shadow-sm">
                  {avatar && !avatarError ? (
                    <img
                      src={getAvatarUrl(avatar)}
                      alt="Avatar"
                      onError={() => setAvatarError(true)}
                      className="w-full h-full object-cover"
                    />
                  ) : (
                    <User size={40} className="text-text-secondary" />
                  )}
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex flex-col items-center justify-center text-white text-[10px] font-semibold gap-1 cursor-pointer"
                  >
                    <Camera size={16} />
                    CHỈNH SỬA
                  </button>
                </div>
                
                <input 
                  type="file" 
                  ref={fileInputRef}
                  className="hidden" 
                  accept="image/*"
                  onChange={handleAvatarChange}
                />
                
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="text-xs font-bold text-text-primary tracking-wide border border-border px-3 py-1.5 rounded-lg hover:bg-surface-3 transition-colors uppercase mt-1 cursor-pointer"
                >
                  Upload Photo
                </button>
              </div>

              {/* Input Fields */}
              <div className="flex flex-col gap-4">
                <Input
                  label={t('profile_fullname_label', 'Họ và tên')}
                  id="fullName"
                  type="text"
                  placeholder={t('profile_fullname_placeholder', 'Nhập họ và tên')}
                  value={fullName}
                  onChange={(e) => {
                    setFullName(e.target.value);
                  }}
                  required
                  disabled={loading}
                  rightElement={<Pencil size={16} className="text-text-secondary" />}
                />
                
                <Input
                  label={t('profile_job_title_label', 'Công việc hiện tại')}
                  id="jobTitle"
                  type="text"
                  placeholder={t('profile_job_title_placeholder', 'Frontend Developer')}
                  value={jobTitle}
                  onChange={(e) => {
                    setJobTitle(e.target.value);
                  }}
                  disabled={loading}
                />

                <div className="flex flex-col gap-1.5 w-full">
                  <label className="text-[12px] font-bold text-text-primary uppercase tracking-wide" htmlFor="interviewLanguage">
                    {t('profile_interview_language_label', 'Ngôn ngữ phỏng vấn ưu tiên')}
                  </label>
                  <select
                    id="interviewLanguage"
                    className="w-full px-4 py-3 bg-surface-2 border rounded-[12px] text-[14px] font-normal text-text-primary outline-none transition-all duration-200 shadow-[0_2px_4px_rgba(31,45,61,0.02)] border-border focus:border-primary focus:ring-4 focus:ring-primary-xlight focus:shadow-none hover:border-border-strong"
                    value={interviewLanguage}
                    onChange={(e) => {
                      setInterviewLanguage(e.target.value);
                    }}
                    disabled={loading}
                  >
                    <option value="en">English</option>
                    <option value="vi">Tiếng Việt</option>
                  </select>
                </div>

                <Input
                  label={t('email_label', 'Email')}
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => {
                    setEmail(e.target.value);
                  }}
                  disabled={loading}
                  placeholder="Email"
                />

                <Input
                  label={t('profile_phone_label', 'Số điện thoại')}
                  id="phoneNumber"
                  type="text"
                  value={phoneNumber || ''}
                  disabled={!isPhoneEditable || loading}
                  readOnly={!isPhoneEditable}
                  onChange={(e) => {
                    setPhoneNumber(e.target.value);
                  }}
                  placeholder={isPhoneEditable 
                    ? t('profile_phone_placeholder_edit', 'Nhập số điện thoại (chỉ được lưu 1 lần)')
                    : t('profile_phone_placeholder', 'Không có số điện thoại')
                  }
                  rightElement={isPhoneEditable ? <Pencil size={16} className="text-text-secondary" /> : null}
                />
              </div>

              {/* Change Password Link */}
              <button
                type="button"
                onClick={() => {
                  setError('');
                  setSuccessMsg('');
                  setIsChangePasswordMode(true);
                }}
                className="text-[13px] font-semibold text-primary hover:text-primary-dark flex items-center gap-1.5 transition-colors self-start mt-1 focus:outline-none cursor-pointer"
              >
                <Lock size={14} />
                {hasPassword 
                  ? t('profile_change_pwd_btn', 'Đổi mật khẩu')
                  : t('profile_create_pwd_btn', 'Tạo mật khẩu')
                }
              </button>

            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border bg-surface-1 animate-in fade-in slide-in-from-bottom-2 duration-200">
              <Button
                type="button"
                disabled={loading || !isDirty}
                onClick={() => {
                  const fullNameInput = document.getElementById('fullName');
                  const emailInput = document.getElementById('email');
                  const phoneInput = document.getElementById('phoneNumber');
                  const jobTitleInput = document.getElementById('jobTitle');
                  const interviewLanguageInput = document.getElementById('interviewLanguage');

                  setFullName(originalFullName);
                  setEmail(originalEmail);
                  setPhoneNumber(originalPhoneNumber);
                  setJobTitle(originalJobTitle);
                  setInterviewLanguage(originalInterviewLanguage);
                  setError('');
                  setSuccessMsg('');

                  if (fullNameInput) fullNameInput.value = originalFullName;
                  if (emailInput) emailInput.value = originalEmail;
                  if (phoneInput) phoneInput.value = originalPhoneNumber || '';
                  if (jobTitleInput) jobTitleInput.value = originalJobTitle;
                  if (interviewLanguageInput) interviewLanguageInput.value = originalInterviewLanguage;
                }}
                className="px-5 py-2 text-sm cursor-pointer"
              >
                Hủy
              </Button>
              <Button
                type="submit"
                disabled={loading || !isDirty}
                className="px-5 py-2 text-sm cursor-pointer"
              >
                Lưu thay đổi
              </Button>
            </div>
          </form>
        ) : (
          /* Change Password View */
          <form onSubmit={handleChangePassword} className="flex-1 flex flex-col">
            <div className="p-6 flex-1 flex flex-col gap-4">
              
              {hasPassword && (
                <div className="flex flex-col gap-1 w-full">
                  <Input
                    label={t('profile_current_pwd', 'Mật khẩu cũ *')}
                    id="currentPassword"
                    type="password"
                    placeholder={t('profile_current_pwd_placeholder', 'Nhập mật khẩu cũ')}
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                    required
                    disabled={loading}
                  />
                  <button 
                    type="button" 
                    onClick={() => {
                      onClose();
                      window.location.hash = '#forgot-password';
                    }}
                    className="text-xs text-primary hover:underline hover:text-primary-dark transition-colors mt-0.5 focus:outline-none cursor-pointer self-start"
                  >
                    {t('forgot_password', 'Quên mật khẩu?')}
                  </button>
                </div>
              )}
              
              <Input
                label={hasPassword ? t('profile_new_pwd', 'Mật khẩu mới *') : t('profile_new_pwd_create', 'Mật khẩu mới *')}
                id="newPassword"
                type="password"
                placeholder={t('profile_new_pwd_placeholder', 'Nhập mật khẩu mới')}
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                required
                disabled={loading}
              />

              <Input
                label={t('profile_confirm_pwd', 'Xác nhận mật khẩu mới *')}
                id="confirmPassword"
                type="password"
                placeholder={t('profile_confirm_pwd_placeholder', 'Nhập lại mật khẩu mới')}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                disabled={loading}
              />

            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border bg-surface-1">
              <Button
                type="submit"
                disabled={loading}
                className="px-5 py-2 text-sm cursor-pointer"
              >
                {hasPassword ? t('profile_save', 'LƯU THAY ĐỔI') : t('profile_create_pwd_btn', 'TẠO MẬT KHẨU')}
              </Button>
            </div>
          </form>
        )}

      </div>
    </div>
  );
}

export default ProfileModal;
