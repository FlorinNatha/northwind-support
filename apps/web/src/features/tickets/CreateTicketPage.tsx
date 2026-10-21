import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import { formatDateTime } from '../../lib/format';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { PriorityBadge } from './components/TicketBadges';
import { SlaIndicator } from './components/SlaIndicator';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status">
          <h2>Ticket Created: {created.reference}</h2>
          <p>
            Raised <strong>{created.reference}</strong> for <strong>{created.customer.name}</strong>.
          </p>

          {created.triage && (
            <div className="triage-breakdown">
              <h3>Triage Assessment</h3>
              <dl className="summary">
                <div>
                  <dt>Priority</dt>
                  <dd>
                    <PriorityBadge priority={created.priority} />
                    <span className="summary__note">{created.triage.priorityReason}</span>
                  </dd>
                </div>

                <div>
                  <dt>SLA Target</dt>
                  <dd>
                    <SlaIndicator status={created.slaStatus} />
                    {created.dueAtUtc && (
                      <span className="summary__note">Due: {formatDateTime(created.dueAtUtc)}</span>
                    )}
                    <span className="summary__note">{created.triage.slaReason}</span>
                  </dd>
                </div>

                <div>
                  <dt>Assignment</dt>
                  <dd>
                    <strong>{created.assignedAgent?.fullName ?? 'Unassigned'}</strong>
                    <span className="summary__note">{created.triage.assignmentReason}</span>
                  </dd>
                </div>
              </dl>
            </div>
          )}

          <div className="button-row" style={{ marginTop: 16 }}>
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setCreated(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
