import { request } from './client';
import type { AuthAgent, LoginPayload, LoginResponse } from '../types/api';

/**
 * Signs in an agent with their email and password (AU-4).
 */
export async function login(payload: LoginPayload): Promise<LoginResponse> {
  return request<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

/**
 * Retrieves the currently authenticated agent's profile (AU-5).
 */
export async function getCurrentAgent(): Promise<AuthAgent> {
  return request<AuthAgent>('/auth/me', {
    method: 'GET',
  });
}
