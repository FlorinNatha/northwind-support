import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from './useAuth';
import { toErrorMessage } from '../../api/client';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Return to intended page or default to ticket list (AU-11)
  const from = (location.state as { from?: { pathname: string; search?: string } })?.from?.pathname || '/tickets';
  const fromSearch = (location.state as { from?: { pathname: string; search?: string } })?.from?.search || '';

  const validate = (): boolean => {
    const errors: { email?: string; password?: string } = {};

    if (!email.trim()) {
      errors.email = 'Email is required.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      errors.email = 'Please enter a valid email address.';
    }

    if (!password) {
      errors.password = 'Password is required.';
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);

    try {
      await login({ email: email.trim(), password });
      navigate(`${from}${fromSearch}`, { replace: true });
    } catch (err) {
      setServerError(toErrorMessage(err, 'Failed to sign in. Please verify your credentials.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <header className="login-header">
          <h1 className="login-title">Sign In</h1>
          <p className="login-subtitle">Northwind Support Ticket Management</p>
        </header>

        {serverError && (
          <div className="login-error-alert" role="alert" aria-live="assertive">
            <span className="login-error-icon" aria-hidden="true">⚠️</span>
            <span>{serverError}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate className="login-form">
          <div className="field">
            <label className="field__label" htmlFor="email-input">
              Email Address
            </label>
            <input
              id="email-input"
              name="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value);
                if (fieldErrors.email) {
                  setFieldErrors((prev) => ({ ...prev, email: undefined }));
                }
              }}
              aria-invalid={!!fieldErrors.email}
              aria-describedby={fieldErrors.email ? 'email-error' : undefined}
              disabled={isSubmitting}
              placeholder="agent@northwind-support.example"
              autoFocus
            />
            {fieldErrors.email && (
              <p id="email-error" className="field__error" role="alert">
                {fieldErrors.email}
              </p>
            )}
          </div>

          <div className="field">
            <label className="field__label" htmlFor="password-input">
              Password
            </label>
            <input
              id="password-input"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value);
                if (fieldErrors.password) {
                  setFieldErrors((prev) => ({ ...prev, password: undefined }));
                }
              }}
              aria-invalid={!!fieldErrors.password}
              aria-describedby={fieldErrors.password ? 'password-error' : undefined}
              disabled={isSubmitting}
              placeholder="••••••••"
            />
            {fieldErrors.password && (
              <p id="password-error" className="field__error" role="alert">
                {fieldErrors.password}
              </p>
            )}
          </div>

          <button
            type="submit"
            className="button button--primary button--full"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <footer className="login-dev-note">
          <strong>Development Fixtures (AU-3):</strong>
          <p>Password: <code>Password123!</code></p>
          <p>Active accounts: <code>alex.turner@northwind-support.example</code>, <code>sara.lindqvist@northwind-support.example</code></p>
        </footer>
      </div>
    </div>
  );
}
