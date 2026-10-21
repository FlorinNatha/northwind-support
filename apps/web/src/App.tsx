import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/useAuth';

/** Application shell: the header, the navigation, agent profile, and wherever the router puts the page. */
export function App() {
  const { agent, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        {agent && (
          <div className="app__user-panel">
            <span className="app__user-name" title={agent.email}>
              👤 {agent.fullName}
            </span>
            <button
              type="button"
              className="button button--ghost app__logout-btn"
              onClick={handleLogout}
              aria-label="Log out"
            >
              Sign Out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
