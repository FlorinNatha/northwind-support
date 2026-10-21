import { useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail, TicketEscalation, TicketPriority } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (ticket: TicketDetail, escalation: TicketEscalation) => void;
}

const NEXT_PRIORITY: Partial<Record<TicketPriority, TicketPriority>> = {
  Low: 'Medium',
  Medium: 'High',
  High: 'Critical',
};

export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('Support Staff');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const isCritical = ticket.priority === 'Critical';
  const isClosedOrResolved = ticket.status === 'Resolved' || ticket.status === 'Closed';
  const canEscalate = !isCritical && !isClosedOrResolved;
  const nextPriority = NEXT_PRIORITY[ticket.priority];

  const trimmedReason = reason.trim();
  const trimmedBy = escalatedBy.trim();

  const validate = (): boolean => {
    if (trimmedReason.length < 5) {
      setValidationError('Escalation reason must be at least 5 characters long.');
      return false;
    }
    if (trimmedReason.length > 500) {
      setValidationError('Escalation reason cannot exceed 500 characters.');
      return false;
    }
    if (!trimmedBy) {
      setValidationError('Please provide who is escalating the ticket.');
      return false;
    }
    if (trimmedBy.length > 100) {
      setValidationError('Escalated by name cannot exceed 100 characters.');
      return false;
    }
    setValidationError(null);
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canEscalate || isSubmitting) return;

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const result = await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmedReason,
        escalatedBy: trimmedBy,
      });

      setReason('');
      onEscalated(result.ticket, result.escalation);
    } catch (err) {
      const message =
        err instanceof ApiError
          ? err.problem?.detail || err.message
          : 'Failed to escalate ticket. Please try again.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card escalation-panel" aria-labelledby="escalation-panel-title">
      <h2 id="escalation-panel-title">Ticket Escalation</h2>

      {!canEscalate ? (
        <div className="escalation-panel__notice" role="note">
          {isCritical ? (
            <p>
              This ticket is already at <strong>Critical</strong> priority and cannot be escalated
              further.
            </p>
          ) : (
            <p>
              Tickets that are <strong>{ticket.status.toLowerCase()}</strong> cannot be escalated.
            </p>
          )}
        </div>
      ) : (
        <form onSubmit={handleSubmit} noValidate>
          <div className="escalation-panel__preview">
            <span>Current priority:</span>
            <PriorityBadge priority={ticket.priority} />
            <span aria-hidden="true">→</span>
            <span>Target priority:</span>
            {nextPriority && <PriorityBadge priority={nextPriority} />}
          </div>

          <p className="field__hint">
            Escalating will raise priority, recalculate the SLA due date from now, and re-evaluate
            specialist agent assignment.
          </p>

          <div className="field">
            <label className="field__label" htmlFor="escalate-by">
              Escalated by <span aria-hidden="true">*</span>
            </label>
            <input
              id="escalate-by"
              type="text"
              value={escalatedBy}
              onChange={(e) => {
                setEscalatedBy(e.target.value);
                if (validationError) setValidationError(null);
              }}
              disabled={isSubmitting}
              maxLength={100}
              placeholder="Your name or role"
              required
            />
          </div>

          <div className="field">
            <div className="escalation-panel__reason-header">
              <label className="field__label" htmlFor="escalate-reason">
                Reason for escalation <span aria-hidden="true">*</span>
              </label>
              <span
                className={`field__char-count ${
                  trimmedReason.length > 500 ? 'field__char-count--overflow' : ''
                }`}
                aria-live="polite"
              >
                {trimmedReason.length}/500
              </span>
            </div>
            <textarea
              id="escalate-reason"
              rows={3}
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (validationError) setValidationError(null);
              }}
              disabled={isSubmitting}
              placeholder="Explain why this ticket requires urgent escalation (5–500 characters)..."
              required
            />
          </div>

          {validationError && (
            <p className="field__error" role="alert">
              {validationError}
            </p>
          )}

          {error && (
            <div className="field__error field__error--banner" role="alert">
              <strong>Cannot escalate:</strong> {error}
            </div>
          )}

          <div className="button-row">
            <button
              type="submit"
              className="button button--danger"
              disabled={isSubmitting || trimmedReason.length < 5 || trimmedReason.length > 500}
            >
              {isSubmitting ? 'Escalating...' : 'Escalate Ticket'}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
