import React from 'react';

export const Table = ({
  headers = [], // array of string or { key, label, align }
  children,
  className = '',
  emptyText = 'No data available',
  isEmpty = false,
}) => {
  return (
    <div className={`w-full overflow-x-auto border border-border rounded-lg bg-surface shadow-sm ${className}`}>
      <table className="w-full text-left border-collapse text-sm">
        <thead>
          <tr className="border-b border-border bg-surface-muted">
            {headers.map((head, idx) => {
              const label = typeof head === 'object' ? head.label : head;
              const align = typeof head === 'object' && head.align ? `text-${head.align}` : 'text-left';
              return (
                <th
                  key={idx}
                  className={`px-4 py-3 text-xs font-semibold text-text-secondary uppercase tracking-wider ${align}`}
                >
                  {label}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isEmpty ? (
            <tr>
              <td colSpan={headers.length} className="px-4 py-8 text-center text-text-muted">
                {emptyText}
              </td>
            </tr>
          ) : (
            children
          )}
        </tbody>
      </table>
    </div>
  );
};

export const TableRow = ({ children, className = '', onClick }) => {
  return (
    <tr 
      onClick={onClick}
      className={`
        hover:bg-surface-muted/60 transition-colors
        ${onClick ? 'cursor-pointer' : ''}
        ${className}
      `}
    >
      {children}
    </tr>
  );
};

export const TableCell = ({ children, className = '', align = 'left' }) => {
  const alignClass = align === 'right' ? 'text-right' : align === 'center' ? 'text-center' : 'text-left';
  return (
    <td className={`px-4 py-3.5 text-text-primary ${alignClass} ${className}`}>
      {children}
    </td>
  );
};

export default Table;
