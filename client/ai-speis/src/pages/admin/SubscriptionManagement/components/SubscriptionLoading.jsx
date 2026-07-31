import React from 'react';

function SubscriptionLoading() {
  return (
    <div className="space-y-4" aria-live="polite" aria-busy="true">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        {Array.from({ length: 3 }).map((_, index) => (
          <div key={index} className="h-24 animate-pulse rounded-2xl border border-border/50 bg-surface-2" />
        ))}
      </div>
      <div className="rounded-2xl border border-border/60 bg-surface-2 p-4">
        <div className="h-10 w-full animate-pulse rounded-xl bg-surface-3" />
        <div className="mt-4 space-y-3">
          {Array.from({ length: 5 }).map((_, index) => (
            <div key={index} className="h-12 w-full animate-pulse rounded-lg bg-surface-3" />
          ))}
        </div>
      </div>
    </div>
  );
}

export default SubscriptionLoading;
