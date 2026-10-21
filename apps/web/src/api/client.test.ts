import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getAuthToken, onUnauthorized, request, setAuthToken } from './client';

describe('client.ts (AU-10, AU-13)', () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    localStorage.clear();
    setAuthToken(null);
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('attaches Bearer token to request headers when authenticated (AU-10)', async () => {
    setAuthToken('sample-jwt-token');
    expect(getAuthToken()).toBe('sample-jwt-token');

    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ success: true }),
    });
    globalThis.fetch = mockFetch;

    await request('/tickets');

    expect(mockFetch).toHaveBeenCalledOnce();
    const callHeaders = mockFetch.mock.calls[0][1].headers;
    expect(callHeaders['Authorization']).toBe('Bearer sample-jwt-token');
    expect(callHeaders['Content-Type']).toBe('application/json');
  });

  it('does not attach Authorization header when unauthenticated', async () => {
    setAuthToken(null);

    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ success: true }),
    });
    globalThis.fetch = mockFetch;

    await request('/tickets');

    const callHeaders = mockFetch.mock.calls[0][1].headers;
    expect(callHeaders['Authorization']).toBeUndefined();
  });

  it('intercepts 401 responses, clears token, and triggers onUnauthorized callback (AU-13)', async () => {
    setAuthToken('expired-jwt-token');

    const unauthorizedCallback = vi.fn();
    const unsubscribe = onUnauthorized(unauthorizedCallback);

    const mockFetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
      json: async () => ({ title: 'Unauthorized', detail: 'Token has expired.' }),
    });
    globalThis.fetch = mockFetch;

    await expect(request('/tickets')).rejects.toThrow('Token has expired.');

    expect(unauthorizedCallback).toHaveBeenCalledOnce();
    expect(getAuthToken()).toBeNull();
    expect(localStorage.getItem('northwind_support_token')).toBeNull();

    unsubscribe();
  });
});
