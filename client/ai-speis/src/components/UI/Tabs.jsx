import React from 'react';

export const Tabs = ({
  tabs = [], // array of { id, label, icon: Icon, badge }
  activeTab,
  onChange,
  variant = 'underline', // 'underline' | 'pills'
  className = '',
}) => {
  return (
    <div className={`flex border-b border-border gap-1 overflow-x-auto scrollbar-none ${className}`} role="tablist">
      {tabs.map((tab) => {
        const isActive = activeTab === tab.id;
        const Icon = tab.icon;

        if (variant === 'pills') {
          return (
            <button
              key={tab.id}
              role="tab"
              aria-selected={isActive}
              onClick={() => onChange(tab.id)}
              className={`
                flex items-center gap-2 px-3.5 py-1.5 text-xs font-semibold rounded-full transition-all duration-150 whitespace-nowrap focus-ring
                ${isActive 
                  ? 'bg-primary text-white shadow-sm' 
                  : 'bg-surface-muted text-text-secondary hover:text-text-primary hover:bg-border'
                }
              `}
            >
              {Icon && <Icon size={14} />}
              <span>{tab.label}</span>
              {tab.badge && (
                <span className={`px-1.5 py-0.2 text-[10px] rounded-full ${isActive ? 'bg-white/20 text-white' : 'bg-border text-text-secondary'}`}>
                  {tab.badge}
                </span>
              )}
            </button>
          );
        }

        return (
          <button
            key={tab.id}
            role="tab"
            aria-selected={isActive}
            onClick={() => onChange(tab.id)}
            className={`
              flex items-center gap-2 px-4 py-3 text-sm font-semibold border-b-2 transition-all duration-150 whitespace-nowrap focus-ring -mb-px
              ${isActive 
                ? 'border-primary text-primary font-bold' 
                : 'border-transparent text-text-secondary hover:text-text-primary hover:border-border-strong'
              }
            `}
          >
            {Icon && <Icon size={16} />}
            <span>{tab.label}</span>
            {tab.badge && (
              <span className={`px-2 py-0.5 text-xs rounded-full ${isActive ? 'bg-primary-light text-primary' : 'bg-surface-muted text-text-muted'}`}>
                {tab.badge}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
};

export default Tabs;
