import React, { useEffect, useState } from 'react';
import { Plus, RefreshCw, Save } from 'lucide-react';
import { API_BASE_URL } from '../../../config/api';
import notify from '../../../utils/notification';

const authHeaders = () => ({
  Authorization: `Bearer ${localStorage.getItem('token')}`,
  'Content-Type': 'application/json',
});

async function api(path, options = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers: { ...authHeaders(), ...options.headers } });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.message || body.Message || 'Không thể xử lý yêu cầu.');
  }
  return response.status === 204 ? null : response.json();
}

const emptyPlan = { code: '', name: '', description: '', interviewQuota: 15, quotaResetDays: 30, isFree: false, displayOrder: 10 };

export default function SubscriptionManagementPage() {
  const [plans, setPlans] = useState([]);
  const [monitoring, setMonitoring] = useState(null);
  const [drafts, setDrafts] = useState({});
  const [newPlan, setNewPlan] = useState(emptyPlan);
  const [showCreate, setShowCreate] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    setBusy(true);
    try {
      const [items, summary] = await Promise.all([
        api('/api/admin/subscription-plans'),
        api('/api/admin/subscription-monitoring/summary'),
      ]);
      setPlans(items);
      setDrafts(Object.fromEntries(items.map((plan) => [plan.planId, plan])));
      setMonitoring(summary);
    } catch (error) {
      notify.error(error.message);
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => { load(); }, []);

  const savePlan = async (planId) => {
    const plan = drafts[planId];
    try {
      await api(`/api/admin/subscription-plans/${planId}`, {
        method: 'PUT',
        body: JSON.stringify({
          code: plan.code,
          name: plan.name,
          description: plan.description,
          interviewQuota: Number(plan.interviewQuota),
          quotaResetDays: plan.isFree ? null : Number(plan.quotaResetDays),
          isFree: plan.isFree,
          displayOrder: Number(plan.displayOrder),
        }),
      });
      notify.success('Đã lưu cấu hình gói.');
      await load();
    } catch (error) { notify.error(error.message); }
  };

  const savePrice = async (price) => {
    try {
      await api(`/api/admin/subscription-plans/prices/${price.priceId}`, {
        method: 'PUT',
        body: JSON.stringify({
          billingCycle: price.billingCycle,
          billingCycleCount: price.billingCycleCount,
          amount: Number(price.amount),
          currency: price.currency,
          effectiveFrom: price.effectiveFrom,
          effectiveTo: price.effectiveTo,
        }),
      });
      notify.success('Đã cập nhật giá.');
      await load();
    } catch (error) { notify.error(error.message); }
  };

  const toggleStatus = async (kind, id, isActive) => {
    const path = kind === 'plan'
      ? `/api/admin/subscription-plans/${id}/status`
      : `/api/admin/subscription-plans/prices/${id}/status`;
    try {
      await api(path, { method: 'PATCH', body: JSON.stringify({ isActive: !isActive }) });
      await load();
    } catch (error) { notify.error(error.message); }
  };

  const createPlan = async () => {
    try {
      await api('/api/admin/subscription-plans', { method: 'POST', body: JSON.stringify({
        ...newPlan,
        interviewQuota: Number(newPlan.interviewQuota),
        quotaResetDays: newPlan.isFree ? null : Number(newPlan.quotaResetDays),
        displayOrder: Number(newPlan.displayOrder),
      }) });
      setNewPlan(emptyPlan);
      setShowCreate(false);
      await load();
    } catch (error) { notify.error(error.message); }
  };

  const addPrice = async (planId, billingCycle) => {
    try {
      await api(`/api/admin/subscription-plans/${planId}/prices`, {
        method: 'POST',
        body: JSON.stringify({
          billingCycle,
          billingCycleCount: 1,
          amount: 0,
          currency: 'VND',
          effectiveFrom: new Date().toISOString(),
          effectiveTo: null,
        }),
      });
      await load();
    } catch (error) { notify.error(error.message); }
  };

  const updateDraft = (planId, key, value) => setDrafts((current) => ({
    ...current,
    [planId]: { ...current[planId], [key]: value },
  }));
  const updatePriceDraft = (planId, priceId, key, value) => setDrafts((current) => ({
    ...current,
    [planId]: {
      ...current[planId],
      prices: current[planId].prices.map((price) => price.priceId === priceId ? { ...price, [key]: value } : price),
    },
  }));

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div><h1 className="text-3xl font-bold text-text-primary">Subscription Plan & Pricing</h1><p className="text-text-secondary">UC-29: cấu hình cấp gói; không điều chỉnh quota từng user.</p></div>
        <div className="flex gap-2">
          <button onClick={load} className="rounded-xl border border-border p-3"><RefreshCw size={18} className={busy ? 'animate-spin' : ''} /></button>
          <button onClick={() => setShowCreate(!showCreate)} className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-3 font-bold text-white"><Plus size={18} /> Thêm gói</button>
        </div>
      </div>

      {monitoring && <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          ['Premium đang hoạt động', monitoring.activePremiumUsers],
          ['Quota đã dùng', `${monitoring.quota.usedQuota}/${monitoring.quota.totalQuota}`],
          ['Đơn đã thanh toán', monitoring.payments.paidOrders],
          ['Doanh thu VND', Number(monitoring.payments.revenueVnd).toLocaleString('vi-VN')],
        ].map(([label, value]) => <div key={label} className="rounded-2xl border border-border bg-surface-1 p-5"><p className="text-xs text-text-secondary uppercase">{label}</p><p className="mt-2 text-2xl font-bold text-text-primary">{value}</p></div>)}
      </div>}

      {showCreate && <div className="rounded-2xl border border-border bg-surface-1 p-5 grid md:grid-cols-3 gap-3">
        {['code', 'name', 'description', 'interviewQuota', 'quotaResetDays', 'displayOrder'].map((key) => <input key={key} value={newPlan[key] ?? ''} onChange={(e) => setNewPlan({ ...newPlan, [key]: e.target.value })} placeholder={key} className="rounded-xl border border-border bg-surface-2 px-3 py-2" />)}
        <button onClick={createPlan} className="rounded-xl bg-primary px-4 py-2 font-bold text-white">Tạo gói</button>
      </div>}

      <div className="space-y-5">
        {plans.map((sourcePlan) => {
          const plan = drafts[sourcePlan.planId] || sourcePlan;
          return <section key={plan.planId} className="rounded-2xl border border-border bg-surface-1 p-5 shadow-sm">
            <div className="grid md:grid-cols-6 gap-3 items-end">
              {[['code', 'Mã'], ['name', 'Tên'], ['interviewQuota', 'Quota'], ['quotaResetDays', 'Reset (ngày)'], ['displayOrder', 'Thứ tự']].map(([key, label]) => <label key={key} className="text-xs text-text-secondary">{label}<input disabled={plan.isFree && key === 'quotaResetDays'} value={plan[key] ?? ''} onChange={(e) => updateDraft(plan.planId, key, e.target.value)} className="mt-1 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-text-primary" /></label>)}
              <button onClick={() => savePlan(plan.planId)} className="inline-flex justify-center gap-2 rounded-xl bg-primary px-3 py-2 font-bold text-white"><Save size={17} /> Lưu</button>
            </div>
            <div className="mt-3 flex items-center gap-3"><span className="text-sm">{plan.isActive ? 'Đang hoạt động' : 'Đã tắt'}</span><button disabled={plan.isFree} onClick={() => toggleStatus('plan', plan.planId, plan.isActive)} className="rounded-lg border border-border px-3 py-1 text-sm">{plan.isActive ? 'Tắt gói' : 'Kích hoạt'}</button></div>
            {!plan.isFree && <div className="mt-5 border-t border-border pt-4 space-y-3">
              {(plan.prices || []).map((price) => <div key={price.priceId} className="grid md:grid-cols-5 gap-3 items-end rounded-xl bg-surface-2 p-3">
                <strong>{price.billingCycle === 2 ? 'Năm' : 'Tháng'}</strong>
                <label className="text-xs">Giá VND<input type="number" value={price.amount} onChange={(e) => updatePriceDraft(plan.planId, price.priceId, 'amount', e.target.value)} className="mt-1 w-full rounded-lg border border-border px-3 py-2" /></label>
                <label className="text-xs">Hiệu lực từ<input value={price.effectiveFrom} onChange={(e) => updatePriceDraft(plan.planId, price.priceId, 'effectiveFrom', e.target.value)} className="mt-1 w-full rounded-lg border border-border px-3 py-2" /></label>
                <button onClick={() => savePrice(price)} className="rounded-lg bg-primary px-3 py-2 text-white">Lưu giá</button>
                <button onClick={() => toggleStatus('price', price.priceId, price.isActive)} className="rounded-lg border border-border px-3 py-2">{price.isActive ? 'Tắt giá' : 'Bật giá'}</button>
              </div>)}
              <div className="flex gap-2"><button onClick={() => addPrice(plan.planId, 1)} className="rounded-lg border border-border px-3 py-2 text-sm">+ Giá tháng</button><button onClick={() => addPrice(plan.planId, 2)} className="rounded-lg border border-border px-3 py-2 text-sm">+ Giá năm</button></div>
            </div>}
          </section>;
        })}
      </div>
    </div>
  );
}
