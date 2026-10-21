import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { getCurrentAgent, login as apiLogin } from '../../api/auth';
import { getAuthToken, onUnauthorized, setAuthToken } from '../../api/client';
import { AuthContext } from './authContextDef';
import type { AuthAgent, LoginPayload } from '../../types/api';

const AGENT_KEY = 'northwind_support_agent';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [agent, setAgent] = useState<AuthAgent | null>(() => {
    if (typeof window === 'undefined') return null;
    const stored = localStorage.getItem(AGENT_KEY);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as AuthAgent;
    } catch {
      return null;
    }
  });

  const [token, setToken] = useState<string | null>(() => getAuthToken());
  const [isLoading, setIsLoading] = useState<boolean>(true);

  // Synchronise state and storage
  const handleSetSession = useCallback((newToken: string | null, newAgent: AuthAgent | null) => {
    setAuthToken(newToken);
    setToken(newToken);
    setAgent(newAgent);

    if (typeof window !== 'undefined') {
      if (newAgent) {
        localStorage.setItem(AGENT_KEY, JSON.stringify(newAgent));
      } else {
        localStorage.removeItem(AGENT_KEY);
      }
    }
  }, []);

  const logout = useCallback(() => {
    handleSetSession(null, null);
  }, [handleSetSession]);

  // AU-13: Any 401 automatically clears the session
  useEffect(() => {
    const unsubscribe = onUnauthorized(() => {
      logout();
    });
    return unsubscribe;
  }, [logout]);

  // Validate existing token with /api/auth/me on initial load
  useEffect(() => {
    let isMounted = true;

    async function validateSession() {
      const currentToken = getAuthToken();
      if (!currentToken) {
        if (isMounted) setIsLoading(false);
        return;
      }

      try {
        const verifiedAgent = await getCurrentAgent();
        if (isMounted) {
          setAgent(verifiedAgent);
          if (typeof window !== 'undefined') {
            localStorage.setItem(AGENT_KEY, JSON.stringify(verifiedAgent));
          }
        }
      } catch {
        if (isMounted) {
          logout();
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    validateSession();

    return () => {
      isMounted = false;
    };
  }, [logout]);

  const login = async (payload: LoginPayload) => {
    const res = await apiLogin(payload);
    handleSetSession(res.token, res.agent);
  };

  return (
    <AuthContext.Provider
      value={{
        agent,
        token,
        isAuthenticated: !!agent && !!token,
        isLoading,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
