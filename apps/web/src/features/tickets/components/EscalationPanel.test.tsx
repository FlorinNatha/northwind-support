import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import type { EscalateTicketResult, TicketEscalation } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

describe('EscalationPanel', () => {
  it('renders current and next priority for a medium ticket', () => {
    const ticket = ticketDetail({ priority: 'Medium' });
    render(<EscalationPanel ticket={ticket} onEscalated={vi.fn()} />);

    expect(screen.getByText('Current priority:')).toBeInTheDocument();
    expect(screen.getByText('Target priority:')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Escalate Ticket' })).toBeDisabled();
  });

  it('disables escalation when ticket is Critical', () => {
    const ticket = ticketDetail({ priority: 'Critical' });
    render(<EscalationPanel ticket={ticket} onEscalated={vi.fn()} />);

    expect(screen.getByRole('note')).toHaveTextContent(/This ticket is already at Critical priority/i);
    expect(screen.queryByRole('button', { name: 'Escalate Ticket' })).not.toBeInTheDocument();
  });

  it('disables escalation when ticket is Resolved', () => {
    const ticket = ticketDetail({ status: 'Resolved' });
    render(<EscalationPanel ticket={ticket} onEscalated={vi.fn()} />);

    expect(screen.getByRole('note')).toHaveTextContent(/Tickets that are resolved cannot be escalated/i);
    expect(screen.queryByRole('button', { name: 'Escalate Ticket' })).not.toBeInTheDocument();
  });

  it('enables button and submits when reason has 5 or more characters', async () => {
    const ticket = ticketDetail({ id: 42, priority: 'Medium' });
    const onEscalated = vi.fn();

    const mockEscalation: TicketEscalation = {
      id: 1,
      ticketId: 42,
      fromPriority: 'Medium',
      toPriority: 'High',
      fromAgent: null,
      toAgent: null,
      fromDueAtUtc: null,
      toDueAtUtc: '2026-10-22T10:00:00Z',
      reason: 'Customer requested rapid escalation',
      escalatedBy: 'Support Lead',
      escalatedAtUtc: '2026-10-21T10:00:00Z',
    };

    const mockResult: EscalateTicketResult = {
      ticket: { ...ticket, priority: 'High' },
      escalation: mockEscalation,
    };

    vi.spyOn(ticketsApi, 'escalateTicket').mockResolvedValue(mockResult);

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const reasonInput = screen.getByLabelText(/Reason for escalation/i);
    const byInput = screen.getByLabelText(/Escalated by/i);
    const submitBtn = screen.getByRole('button', { name: 'Escalate Ticket' });

    expect(submitBtn).toBeDisabled();

    fireEvent.change(reasonInput, { target: { value: 'Customer requested rapid escalation' } });
    fireEvent.change(byInput, { target: { value: 'Support Lead' } });

    expect(submitBtn).not.toBeDisabled();

    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(ticketsApi.escalateTicket).toHaveBeenCalledWith(42, {
        reason: 'Customer requested rapid escalation',
        escalatedBy: 'Support Lead',
      });
      expect(onEscalated).toHaveBeenCalledWith(mockResult.ticket, mockResult.escalation);
    });
  });
});
