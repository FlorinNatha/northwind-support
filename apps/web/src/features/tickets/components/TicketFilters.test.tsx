import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../hooks/useTicketList';
import { TicketFilters } from './TicketFilters';

describe('TicketFilters', () => {
  it('renders SLA status dropdown with all options', () => {
    const onChange = vi.fn();
    const onReset = vi.fn();

    render(
      <TicketFilters
        filters={emptyFilters}
        agents={[]}
        categories={[]}
        customers={[]}
        onChange={onChange}
        onReset={onReset}
      />
    );

    const slaSelect = screen.getByLabelText('SLA Status');
    expect(slaSelect).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'All SLA statuses' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Within SLA' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'At risk' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Breached' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Met' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'No SLA' })).toBeInTheDocument();
  });

  it('notifies onChange when SLA status is selected', () => {
    const onChange = vi.fn();
    const onReset = vi.fn();

    render(
      <TicketFilters
        filters={emptyFilters}
        agents={[]}
        categories={[]}
        customers={[]}
        onChange={onChange}
        onReset={onReset}
      />
    );

    const slaSelect = screen.getByLabelText('SLA Status');
    fireEvent.change(slaSelect, { target: { value: 'AtRisk' } });

    expect(onChange).toHaveBeenCalledWith({ slaStatus: 'AtRisk' });
  });
});
