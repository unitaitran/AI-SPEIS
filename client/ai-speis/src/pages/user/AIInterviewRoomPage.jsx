import React from 'react';
import { ArrowLeft, Mic, Sparkles } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';

function AIInterviewRoomPage() {
  return (
    <UserLayout>
      <div className="animate-pageEntrance max-w-[960px] mx-auto pb-10">
        <section className="bg-surface-2 border border-border rounded-2xl shadow-sm p-8 md:p-10">
          <div className="flex items-start gap-4">
            <div className="w-12 h-12 rounded-xl bg-primary-xlight text-primary-dark flex items-center justify-center shrink-0">
              <Mic size={24} />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase text-primary-dark mb-2">AI Interview Room</p>
              <h1 className="text-2xl md:text-3xl font-bold text-text-primary mb-3">
                Phòng phỏng vấn AI
              </h1>
              <p className="text-sm text-text-secondary leading-relaxed max-w-2xl">
                Thiết bị đã được kiểm tra. Màn phòng phỏng vấn chính sẽ tiếp tục được triển khai ở bước sau.
              </p>
            </div>
          </div>

          <div className="mt-8 p-5 rounded-xl bg-primary-xlight border border-primary-light flex items-start gap-3">
            <Sparkles size={20} className="text-primary-dark shrink-0 mt-0.5" />
            <p className="m-0 text-sm text-text-primary leading-relaxed">
              Chưa implement
            </p>
          </div>

          <button
            type="button"
            className="mt-8 inline-flex items-center gap-2 min-h-[44px] px-5 rounded-xl border border-border bg-surface-2 text-text-primary font-bold hover:bg-primary-xlight hover:border-primary-light transition-colors"
            onClick={() => navigate(USER_ROUTES.DEVICE_CHECK)}
          >
            <ArrowLeft size={18} />
            Quay lại kiểm tra thiết bị
          </button>
        </section>
      </div>
    </UserLayout>
  );
}

export default AIInterviewRoomPage;
