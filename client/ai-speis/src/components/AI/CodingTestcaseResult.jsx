import React from 'react';
import { CheckCircle2, XCircle, Clock, AlertTriangle } from 'lucide-react';
import Badge from '../UI/Badge';

export const CodingTestcaseResult = ({
  testcases = [], // array of { id, title, status: 'passed' | 'failed' | 'error', executionTime, expectedOutput, actualOutput }
  className = '',
}) => {
  const passedCount = testcases.filter((tc) => tc.status === 'passed').length;
  const totalCount = testcases.length;

  return (
    <div className={`flex flex-col gap-3 w-full ${className}`}>
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-bold text-text-primary">Kết quả Test Cases</h4>
        <Badge variant={passedCount === totalCount ? 'success' : 'warning'} size="sm">
          {passedCount} / {totalCount} Passed
        </Badge>
      </div>

      <div className="flex flex-col gap-2">
        {testcases.map((tc, idx) => {
          const isPassed = tc.status === 'passed';
          const isError = tc.status === 'error';

          return (
            <div
              key={tc.id || idx}
              className={`
                p-3.5 rounded-lg border text-xs flex flex-col gap-2 transition-colors
                ${isPassed 
                  ? 'bg-success-light/40 border-success/30 text-text-primary' 
                  : isError 
                  ? 'bg-error-light/40 border-error/30 text-text-primary' 
                  : 'bg-warning-light/40 border-warning/30 text-text-primary'
                }
              `}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  {isPassed ? (
                    <CheckCircle2 size={16} className="text-success shrink-0" />
                  ) : isError ? (
                    <AlertTriangle size={16} className="text-error shrink-0" />
                  ) : (
                    <XCircle size={16} className="text-error shrink-0" />
                  )}
                  <span className="font-bold">{tc.title || `Test case #${idx + 1}`}</span>
                </div>

                {tc.executionTime && (
                  <span className="flex items-center gap-1 text-[11px] text-text-muted font-mono">
                    <Clock size={12} /> {tc.executionTime}ms
                  </span>
                )}
              </div>

              {!isPassed && (
                <div className="mt-1 p-2 bg-surface rounded border border-border font-mono text-[11px] flex flex-col gap-1">
                  <div><span className="text-text-muted">Expected:</span> <code className="text-success">{tc.expectedOutput}</code></div>
                  <div><span className="text-text-muted">Actual:</span> <code className="text-error">{tc.actualOutput}</code></div>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default CodingTestcaseResult;
