import React, { cloneElement, useId, useRef, useState } from 'react';
import {
  BriefcaseBusiness,
  Camera,
  CheckCircle2,
  GraduationCap,
  Info,
  Save,
  UserRound,
  Wrench,
  X,
} from 'lucide-react';
import UserLayout from '../../../layouts/user/UserLayout';
import './ProfilePage.css';

const INITIAL_PROFILE = {
  fullName: 'Nguyễn Minh Anh',
  email: 'minh.anh@example.com',
  phone: '0912 345 678',
  university: 'Đại học FPT',
  major: 'Kỹ thuật phần mềm',
  studyYear: '4',
  targetRole: 'Frontend Developer',
  level: 'fresher',
  preferredLanguage: 'vi',
  technicalSkills: 'HTML, CSS, JavaScript, React, TypeScript',
  softSkills: 'Giao tiếp, Làm việc nhóm, Quản lý thời gian',
  technologies: 'Git, Docker, Figma, REST API',
  avatar: '',
};

const SECTION_CONFIG = [
  { id: 'basic', title: 'Thông tin cơ bản', icon: UserRound },
  { id: 'education', title: 'Thông tin học tập', icon: GraduationCap },
  { id: 'career', title: 'Mục tiêu nghề nghiệp', icon: BriefcaseBusiness },
  { id: 'skills', title: 'Kỹ năng hiện có', icon: Wrench },
];

function FormField({ label, error, children, hint }) {
  const fieldId = useId();
  const messageId = `${fieldId}-message`;

  return (
    <div className={`profile-field ${error ? 'has-error' : ''}`}>
      <label htmlFor={fieldId}>{label}</label>
      {cloneElement(children, {
        id: fieldId,
        'aria-describedby': error || hint ? messageId : undefined,
        'aria-invalid': error ? 'true' : undefined,
      })}
      {error && <span id={messageId} className="profile-field-message">{error}</span>}
      {!error && hint && <span id={messageId} className="profile-field-hint">{hint}</span>}
    </div>
  );
}

function SectionCard({ sectionId, children }) {
  const section = SECTION_CONFIG.find((item) => item.id === sectionId);
  const Icon = section.icon;

  return (
    <section className={`profile-card profile-card-${sectionId}`}>
      <div className="profile-card-heading">
        <span><Icon size={19} /></span>
        <h2>{section.title}</h2>
      </div>
      {children}
    </section>
  );
}

function ProfilePage() {
  const [profile, setProfile] = useState(INITIAL_PROFILE);
  const [savedProfile, setSavedProfile] = useState(INITIAL_PROFILE);
  const [errors, setErrors] = useState({});
  const [showSuccess, setShowSuccess] = useState(false);
  const fileInputRef = useRef(null);
  const isDirty = JSON.stringify(profile) !== JSON.stringify(savedProfile);

  const updateField = (field, value) => {
    setProfile((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: '' }));
    setShowSuccess(false);
  };

  const handleAvatarChange = (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      setErrors((current) => ({ ...current, avatar: 'Vui lòng chọn một tệp hình ảnh.' }));
      return;
    }

    const reader = new FileReader();
    reader.onload = () => updateField('avatar', reader.result);
    reader.readAsDataURL(file);
  };

  const validate = () => {
    const nextErrors = {};
    
    // Basic Information Section
    if (!profile.fullName.trim()) {
      nextErrors.fullName = 'Vui lòng nhập họ và tên.';
    } else if (profile.fullName.trim().length < 3) {
      nextErrors.fullName = 'Họ và tên phải có ít nhất 3 ký tự.';
    } else if (profile.fullName.trim().length > 100) {
      nextErrors.fullName = 'Họ và tên không được vượt quá 100 ký tự.';
    }
    
    if (!profile.email.trim()) {
      nextErrors.email = 'Vui lòng nhập email.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(profile.email)) {
      nextErrors.email = 'Email chưa đúng định dạng.';
    }
    
    if (profile.phone.trim() && !/^[0-9+\s().-]{9,}$/.test(profile.phone)) {
      nextErrors.phone = 'Số điện thoại chưa hợp lệ.';
    }

    // Education Section
    if (!profile.university.trim()) {
      nextErrors.university = 'Vui lòng nhập tên trường đại học.';
    } else if (profile.university.trim().length < 3) {
      nextErrors.university = 'Tên trường phải có ít nhất 3 ký tự.';
    }

    if (!profile.major.trim()) {
      nextErrors.major = 'Vui lòng nhập chuyên ngành.';
    } else if (profile.major.trim().length < 2) {
      nextErrors.major = 'Chuyên ngành phải có ít nhất 2 ký tự.';
    }

    if (!profile.studyYear) {
      nextErrors.studyYear = 'Vui lòng chọn năm học.';
    }

    // Career Section
    if (!profile.targetRole.trim()) {
      nextErrors.targetRole = 'Vui lòng nhập vị trí mong muốn.';
    } else if (profile.targetRole.trim().length < 2) {
      nextErrors.targetRole = 'Vị trí phải có ít nhất 2 ký tự.';
    }

    // Skills Section
    if (!profile.technicalSkills.trim()) {
      nextErrors.technicalSkills = 'Vui lòng nhập ít nhất một kỹ năng kỹ thuật.';
    } else if (profile.technicalSkills.trim().length < 2) {
      nextErrors.technicalSkills = 'Kỹ năng kỹ thuật phải có ít nhất 2 ký tự.';
    }

    if (!profile.softSkills.trim()) {
      nextErrors.softSkills = 'Vui lòng nhập ít nhất một kỹ năng mềm.';
    } else if (profile.softSkills.trim().length < 2) {
      nextErrors.softSkills = 'Kỹ năng mềm phải có ít nhất 2 ký tự.';
    }

    if (!profile.technologies.trim()) {
      nextErrors.technologies = 'Vui lòng nhập ít nhất một công nghệ đã sử dụng.';
    } else if (profile.technologies.trim().length < 2) {
      nextErrors.technologies = 'Công nghệ phải có ít nhất 2 ký tự.';
    }

    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    if (!validate()) return;

    setSavedProfile(profile);
    setShowSuccess(true);
  };

  const handleCancel = () => {
    setProfile(savedProfile);
    setErrors({});
    setShowSuccess(false);
  };

  return (
    <UserLayout>
      <div className="profile-page">
        <header className="profile-page-header">
          <div>
            <span className="profile-eyebrow">Hồ sơ & cá nhân hóa</span>
            <h1>Hồ sơ cá nhân</h1>
            <p>
              Thông tin này được dùng để cá nhân hóa câu hỏi phỏng vấn, phản hồi
              và lộ trình luyện tập của bạn.
            </p>
          </div>
          <div className="profile-guidance">
            <Info size={18} />
            <span>Hồ sơ càng đầy đủ, gợi ý luyện tập càng phù hợp.</span>
          </div>
        </header>

        {showSuccess && (
          <div className="profile-success" role="status">
            <CheckCircle2 size={19} />
            <span>Đã lưu thay đổi hồ sơ.</span>
          </div>
        )}

        <form className="profile-form" onSubmit={handleSubmit} noValidate>
          <div className="profile-grid">
            <SectionCard sectionId="basic">
              <div className="avatar-editor">
                <div className="avatar-preview">
                  {profile.avatar ? (
                    <img src={profile.avatar} alt="Ảnh đại diện" />
                  ) : (
                    <UserRound size={34} aria-hidden="true" />
                  )}
                </div>
                <div>
                  <input
                    ref={fileInputRef}
                    className="avatar-file-input"
                    type="file"
                    accept="image/*"
                    onChange={handleAvatarChange}
                  />
                  <button
                    type="button"
                    className="avatar-change-button"
                    onClick={() => fileInputRef.current?.click()}
                  >
                    <Camera size={17} />
                    Chỉnh sửa ảnh
                  </button>
                  <p>JPG hoặc PNG, tối đa 5 MB.</p>
                  {errors.avatar && <span className="profile-field-message">{errors.avatar}</span>}
                </div>
              </div>

              <div className="profile-card-fields">
                <FormField label="Họ và tên" error={errors.fullName}>
                  <input
                    value={profile.fullName}
                    onChange={(event) => updateField('fullName', event.target.value)}
                    placeholder="Nhập họ và tên"
                  />
                </FormField>
                <FormField label="Email" error={errors.email}>
                  <input
                    type="email"
                    value={profile.email}
                    onChange={(event) => updateField('email', event.target.value)}
                    placeholder="example@email.com"
                  />
                </FormField>
                <FormField label="Số điện thoại" error={errors.phone}>
                  <input
                    type="tel"
                    value={profile.phone}
                    onChange={(event) => updateField('phone', event.target.value)}
                    placeholder="Nhập số điện thoại"
                  />
                </FormField>
              </div>
            </SectionCard>

            <SectionCard sectionId="education">
              <div className="profile-card-fields">
                <FormField label="Trường đại học" error={errors.university}>
                  <input
                    value={profile.university}
                    onChange={(event) => updateField('university', event.target.value)}
                    placeholder="Tên trường đại học"
                  />
                </FormField>
                <FormField label="Chuyên ngành" error={errors.major}>
                  <input
                    value={profile.major}
                    onChange={(event) => updateField('major', event.target.value)}
                    placeholder="Ví dụ: Khoa học máy tính"
                  />
                </FormField>
                <FormField label="Năm học" error={errors.studyYear}>
                  <select
                    value={profile.studyYear}
                    onChange={(event) => updateField('studyYear', event.target.value)}
                  >
                    <option value="">Chọn năm học</option>
                    <option value="1">Năm 1</option>
                    <option value="2">Năm 2</option>
                    <option value="3">Năm 3</option>
                    <option value="4">Năm 4</option>
                    <option value="5">Năm 5 hoặc cao hơn</option>
                    <option value="graduated">Đã tốt nghiệp</option>
                  </select>
                </FormField>
              </div>
            </SectionCard>

            <SectionCard sectionId="career">
              <div className="profile-card-fields">
                <FormField label="Vị trí mong muốn" error={errors.targetRole}>
                  <input
                    value={profile.targetRole}
                    onChange={(event) => updateField('targetRole', event.target.value)}
                    placeholder="Ví dụ: Frontend Developer"
                  />
                </FormField>

                <fieldset className="profile-level-fieldset">
                  <legend>Cấp độ</legend>
                  <div className="profile-radio-group">
                    {[
                      ['intern', 'Intern'],
                      ['fresher', 'Fresher'],
                      ['junior', 'Junior'],
                    ].map(([value, label]) => (
                      <label key={value}>
                        <input
                          type="radio"
                          name="level"
                          value={value}
                          checked={profile.level === value}
                          onChange={(event) => updateField('level', event.target.value)}
                        />
                        <span>{label}</span>
                      </label>
                    ))}
                  </div>
                </fieldset>

                <FormField
                  label="Ngôn ngữ phỏng vấn ưu tiên"
                  hint="Bạn vẫn có thể đổi ngôn ngữ khi bắt đầu từng buổi phỏng vấn."
                >
                  <select
                    value={profile.preferredLanguage}
                    onChange={(event) => updateField('preferredLanguage', event.target.value)}
                  >
                    <option value="vi">Tiếng Việt</option>
                    <option value="en">English</option>
                    <option value="bilingual">Song ngữ Việt - Anh</option>
                  </select>
                </FormField>
              </div>
            </SectionCard>

            <SectionCard sectionId="skills">
              <div className="profile-card-fields">
                <FormField
                  label="Kỹ năng kỹ thuật"
                  hint="Phân tách các kỹ năng bằng dấu phẩy."
                  error={errors.technicalSkills}
                >
                  <textarea
                    rows="3"
                    value={profile.technicalSkills}
                    onChange={(event) => updateField('technicalSkills', event.target.value)}
                    placeholder="Ví dụ: HTML, CSS, JavaScript, React"
                  />
                </FormField>
                <FormField label="Kỹ năng mềm" error={errors.softSkills}>
                  <textarea
                    rows="3"
                    value={profile.softSkills}
                    onChange={(event) => updateField('softSkills', event.target.value)}
                    placeholder="Ví dụ: Giao tiếp, Làm việc nhóm, Giải quyết vấn đề"
                  />
                </FormField>
                <FormField label="Công nghệ đã sử dụng" error={errors.technologies}>
                  <textarea
                    rows="3"
                    value={profile.technologies}
                    onChange={(event) => updateField('technologies', event.target.value)}
                    placeholder="Ví dụ: Git, Docker, AWS"
                  />
                </FormField>
              </div>
            </SectionCard>
          </div>

          <div className="profile-form-actions">
            <span className={`unsaved-indicator ${isDirty ? 'is-visible' : ''}`}>
              {isDirty ? 'Bạn có thay đổi chưa lưu' : 'Mọi thay đổi đã được lưu'}
            </span>
            <div>
              <button
                className="profile-secondary-button"
                type="button"
                onClick={handleCancel}
                disabled={!isDirty}
              >
                <X size={18} />
                Hủy
              </button>
              <button
                className="profile-primary-button"
                type="submit"
                disabled={!isDirty}
              >
                <Save size={18} />
                Lưu thay đổi
              </button>
            </div>
          </div>
        </form>
      </div>
    </UserLayout>
  );
}

export default ProfilePage;
