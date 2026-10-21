import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  escalations: TicketEscalation[];
  isLoading?: boolean;
}

export function EscalationHistory({ escalations, isLoading = false }: EscalationHistoryProps) {
  if (isLoading) {
    return (
      <div className="card escalation-history" aria-labelledby="escalation-history-title">
        <h2 id="escalation-history-title">Escalation History</h2>
        <p className="field__hint">Loading escalation records...</p>
      </div>
    );
  }

  return (
    <div className="card escalation-history" aria-labelledby="escalation-history-title">
      <h2 id="escalation-history-title">
        Escalation History
        {escalations.length > 0 && (
          <span className="badge badge--neutral" style={{ marginLeft: 8 }}>
            {escalations.length}
          </span>
        )}
      </h2>

      {escalations.length === 0 ? (
        <p className="field__hint">No escalations recorded for this ticket.</p>
      ) : (
        <ol className="escalation-timeline" aria-label="Escalation history entries">
          {escalations.map((item) => (
            <li key={item.id} className="escalation-item">
              <div className="escalation-item__header">
                <div className="escalation-item__priority-change">
                  <PriorityBadge priority={item.fromPriority} />
                  <span aria-hidden="true">→</span>
                  <PriorityBadge priority={item.toPriority} />
                </div>
                <time className="escalation-item__time" dateTime={item.escalatedAtUtc}>
                  {formatDateTime(item.escalatedAtUtc)}
                </time>
              </div>

              <p className="escalation-item__meta">
                Escalated by <strong>{item.escalatedBy}</strong>
                {item.fromAgent?.id !== item.toAgent?.id && (
                  <span>
                    {' '}
                    • Assignee: {item.fromAgent?.fullName ?? 'Unassigned'} →{' '}
                    <strong>{item.toAgent?.fullName ?? 'Unassigned'}</strong>
                  </span>
                )}
                {item.toDueAtUtc && (
                  <span> • New SLA Due: {formatDateTime(item.toDueAtUtc)}</span>
                )}
              </p>

              <blockquote className="escalation-item__reason">
                "{item.reason}"
              </blockquote>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
