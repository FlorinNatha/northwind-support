import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketEscalation } from '../../../types/api';

/** Loads the audit history of escalations for a ticket. */
export function useTicketEscalations(ticketId: number) {
  const load = useCallback(() => ticketsApi.getEscalations(ticketId), [ticketId]);

  return useAsyncData<TicketEscalation[]>(load, 'Unable to load escalation history.');
}
