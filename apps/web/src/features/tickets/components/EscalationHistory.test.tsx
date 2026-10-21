import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { TicketEscalation } from '../../../types/api';
import { EscalationHistory } from './EscalationHistory';

describe('EscalationHistory', () => {
  it('renders empty message when there are no escalations', () => {
    render(<EscalationHistory escalations={[]} />);
    expect(screen.getByText('No escalations recorded for this ticket.')).toBeInTheDocument();
  });

  it('renders escalation records with priority changes and reason', () => {
    const escalations: TicketEscalation[] = [
      {
        id: 1,
        ticketId: 10,
        fromPriority: 'Low',
        toPriority: 'Medium',
        fromAgent: { id: 1, fullName: 'Alex Turner' },
        toAgent: { id: 2, fullName: 'Sarah Connor' },
        fromDueAtUtc: '2026-10-22T08:00:00Z',
        toDueAtUtc: '2026-10-21T18:00:00Z',
        reason: 'Customer reported service degraded',
        escalatedBy: 'Support Lead',
        escalatedAtUtc: '2026-10-21T12:00:00Z',
      },
    ];

    const { container } = render(<EscalationHistory escalations={escalations} />);

    expect(screen.getByText('Escalation History')).toBeInTheDocument();
    expect(screen.getByText('Low')).toBeInTheDocument();
    expect(screen.getByText('Medium')).toBeInTheDocument();
    expect(screen.getByText('Support Lead')).toBeInTheDocument();
    expect(container).toHaveTextContent(/Alex Turner →.*Sarah Connor/);
    expect(screen.getByText(/Customer reported service degraded/)).toBeInTheDocument();
  });
});
