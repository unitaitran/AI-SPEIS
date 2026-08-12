import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';

export const Pagination = ({
  currentPage = 1,
  totalPages = 1,
  onPageChange,
  totalItems,
  pageSize,
  className = '',
}) => {
  const canPrev = currentPage > 1;
  const canNext = currentPage < totalPages;

  const pages = [];
  for (let i = 1; i <= totalPages; i++) {
    if (
      i === 1 ||
      i === totalPages ||
      (i >= currentPage - 1 && i <= currentPage + 1)
    ) {
      pages.push(i);
    } else if (pages[pages.length - 1] !== '...') {
      pages.push('...');
    }
  }

  return (
    <div className={`flex items-center justify-between px-4 py-3 bg-surface border-t border-border ${className}`}>
      {totalItems !== undefined && (
        <div className="text-xs text-text-secondary">
          Hiển thị trang <span className="font-semibold text-text-primary">{currentPage}</span> / <span className="font-semibold text-text-primary">{totalPages}</span> ({totalItems} kết quả)
        </div>
      )}
      
      <div className="flex items-center gap-1.5 ml-auto">
        <button
          type="button"
          disabled={!canPrev}
          onClick={() => onPageChange(currentPage - 1)}
          className="p-1.5 rounded border border-border text-text-secondary hover:text-text-primary hover:bg-surface-muted disabled:opacity-40 disabled:cursor-not-allowed transition-colors focus-ring"
          aria-label="Trang trước"
        >
          <ChevronLeft size={16} />
        </button>

        {pages.map((page, index) => {
          if (page === '...') {
            return (
              <span key={`ellipsis-${index}`} className="px-2 text-xs text-text-muted">
                ...
              </span>
            );
          }

          const isActive = page === currentPage;
          return (
            <button
              key={page}
              type="button"
              onClick={() => onPageChange(page)}
              className={`
                min-w-[32px] h-[32px] px-2 text-xs font-semibold rounded transition-colors focus-ring
                ${isActive 
                  ? 'bg-primary text-white font-bold' 
                  : 'text-text-primary hover:bg-surface-muted border border-border'
                }
              `}
            >
              {page}
            </button>
          );
        })}

        <button
          type="button"
          disabled={!canNext}
          onClick={() => onPageChange(currentPage + 1)}
          className="p-1.5 rounded border border-border text-text-secondary hover:text-text-primary hover:bg-surface-muted disabled:opacity-40 disabled:cursor-not-allowed transition-colors focus-ring"
          aria-label="Trang sau"
        >
          <ChevronRight size={16} />
        </button>
      </div>
    </div>
  );
};

export default Pagination;
