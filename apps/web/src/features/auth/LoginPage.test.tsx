import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LoginPage } from './LoginPage';
import { AuthContext, type AuthContextValue } from './authContextDef';

const mockNavigate = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useLocation: () => ({ pathname: '/login', state: { from: { pathname: '/tickets/42' } } }),
  };
});

describe('LoginPage (AU-9)', () => {
  const mockLogin = vi.fn();

  const authValue: AuthContextValue = {
    agent: null,
    token: null,
    isAuthenticated: false,
    isLoading: false,
    login: mockLogin,
    logout: vi.fn(),
  };

  const renderWithAuth = () => {
    return render(
      <MemoryRouter>
        <AuthContext.Provider value={authValue}>
          <LoginPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    );
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders labelled, accessible fields and a submit button', () => {
    renderWithAuth();

    expect(screen.getByRole('heading', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
  });

  it('validates required fields on client side without calling login API', async () => {
    const user = userEvent.setup();
    renderWithAuth();

    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(screen.getByText('Email is required.')).toBeInTheDocument();
    expect(screen.getByText('Password is required.')).toBeInTheDocument();
    expect(mockLogin).not.toHaveBeenCalled();
  });

  it('validates email format on client side', async () => {
    const user = userEvent.setup();
    renderWithAuth();

    await user.type(screen.getByLabelText(/email address/i), 'invalid-email');
    await user.type(screen.getByLabelText(/password/i), 'Password123!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(screen.getByText('Please enter a valid email address.')).toBeInTheDocument();
    expect(mockLogin).not.toHaveBeenCalled();
  });

  it('displays clear error on rejected credentials', async () => {
    const user = userEvent.setup();
    mockLogin.mockRejectedValueOnce(new Error('Invalid email or password.'));

    renderWithAuth();

    await user.type(screen.getByLabelText(/email address/i), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText(/password/i), 'WrongPassword!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(mockLogin).toHaveBeenCalledWith({
      email: 'alex.turner@northwind-support.example',
      password: 'WrongPassword!',
    });

    const errorAlert = await screen.findByRole('alert');
    expect(errorAlert).toHaveTextContent(/invalid email or password/i);
  });

  it('successfully logs in and redirects to the intended page (AU-11)', async () => {
    const user = userEvent.setup();
    mockLogin.mockResolvedValueOnce(undefined);

    renderWithAuth();

    await user.type(screen.getByLabelText(/email address/i), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText(/password/i), 'Password123!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(mockLogin).toHaveBeenCalledWith({
      email: 'alex.turner@northwind-support.example',
      password: 'Password123!',
    });

    expect(mockNavigate).toHaveBeenCalledWith('/tickets/42', { replace: true });
  });
});
