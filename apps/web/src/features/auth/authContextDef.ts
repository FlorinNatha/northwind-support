import { createContext } from 'react';
import type { AuthAgent, LoginPayload } from '../../types/api';

export interface AuthContextValue {
  agent: AuthAgent | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (payload: LoginPayload) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
