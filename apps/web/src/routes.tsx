import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom';
import { App } from './App';
import { AuthProvider } from './features/auth/AuthContext';
import { LoginPage } from './features/auth/LoginPage';
import { ProtectedRoute } from './features/auth/ProtectedRoute';
import { NotFoundPage } from './features/NotFoundPage';
import { CustomerDetailPage } from './features/customers/CustomerDetailPage';
import { CustomerListPage } from './features/customers/CustomerListPage';
import { CreateTicketPage } from './features/tickets/CreateTicketPage';
import { TicketDetailPage } from './features/tickets/TicketDetailPage';
import { TicketListPage } from './features/tickets/TicketListPage';

export const router = createBrowserRouter([
  {
    element: (
      <AuthProvider>
        <Outlet />
      </AuthProvider>
    ),
    children: [
      {
        path: '/login',
        element: <LoginPage />,
      },
      {
        path: '/',
        element: <App />,
        children: [
          {
            element: <ProtectedRoute />,
            children: [
              { index: true, element: <Navigate to="/tickets" replace /> },
              { path: 'tickets', element: <TicketListPage /> },
              { path: 'tickets/new', element: <CreateTicketPage /> },
              { path: 'tickets/:id', element: <TicketDetailPage /> },
              { path: 'customers', element: <CustomerListPage /> },
              { path: 'customers/:id', element: <CustomerDetailPage /> },
            ],
          },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);
