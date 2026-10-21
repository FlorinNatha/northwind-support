import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ProtectedRoute } from './ProtectedRoute';
import { AuthContext, type AuthContextValue } from './authContextDef';

describe('ProtectedRoute (AU-11)', () => {
  const createAuthValue = (isAuthenticated: boolean, isLoading: boolean): AuthContextValue => ({
    agent: isAuthenticated ? { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' } : null,
    token: isAuthenticated ? 'mock.token' : null,
    isAuthenticated,
    isLoading,
    login: vi.fn(),
    logout: vi.fn(),
  });

  it('renders loading state when session is being validated', () => {
    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <AuthContext.Provider value={createAuthValue(false, true)}>
          <Routes>
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets" element={<div>Protected Content</div>} />
            </Route>
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    expect(screen.getByText(/loading session/i)).toBeInTheDocument();
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
  });

  it('redirects unauthenticated visitors to /login', () => {
    render(
      <MemoryRouter initialEntries={['/tickets/new']}>
        <AuthContext.Provider value={createAuthValue(false, false)}>
          <Routes>
            <Route path="/login" element={<div>Login Page</div>} />
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets/new" element={<div>New Ticket Form</div>} />
            </Route>
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    expect(screen.getByText('Login Page')).toBeInTheDocument();
    expect(screen.queryByText('New Ticket Form')).not.toBeInTheDocument();
  });

  it('renders protected child content when authenticated', () => {
    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <AuthContext.Provider value={createAuthValue(true, false)}>
          <Routes>
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets" element={<div>Protected Content</div>} />
            </Route>
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    );

    expect(screen.getByText('Protected Content')).toBeInTheDocument();
  });
});
