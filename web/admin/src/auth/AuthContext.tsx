import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { Tokens } from '../shared/types';
import { ApiError } from '../shared/types';

const cleanUrl = (val?: string) => {
  if (!val) return '';
  const match = val.match(/https?:\/\/[^\s\]\)\'\"\`\,]+/);
  return (match ? match[0] : val).replace(/\/$/, '');
};
const refreshKey = 'notification.refresh';
export const API_BASE = cleanUrl(import.meta.env.VITE_API_URL);
export const apiUrl = (path: string) => (path.startsWith('http') ? path : `${API_BASE}${path}`);

type Auth = {
  ready: boolean;
  authenticated: boolean;
  login: (e: string, p: string) => Promise<void>;
  logout: () => Promise<void>;
  request: <T>(path: string, init?: RequestInit) => Promise<T>;
};

const Context = createContext<Auth | null>(null);

async function json<T>(r: Response) {
  if (!r.ok) {
    let code = 'REQUEST_FAILED';
    try {
      code = (await r.json()).code ?? code;
    } catch {}
    throw new ApiError(r.status, code);
  }
  return r.status === 204 ? (undefined as T) : (r.json() as Promise<T>);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string>();
  const [ready, setReady] = useState(false);

  const rotate = useCallback(async () => {
    const refreshToken = sessionStorage.getItem(refreshKey);
    if (!refreshToken) throw new ApiError(401, 'UNAUTHORIZED');
    const x = await json<Tokens>(
      await fetch(apiUrl('/v1/auth/refresh'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
    );
    sessionStorage.setItem(refreshKey, x.refreshToken);
    setToken(x.accessToken);
    return x.accessToken;
  }, []);

  useEffect(() => {
    rotate()
      .catch(() => sessionStorage.removeItem(refreshKey))
      .finally(() => setReady(true));
  }, [rotate]);

  const login = async (email: string, password: string) => {
    const x = await json<Tokens>(
      await fetch(apiUrl('/v1/auth/login'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })
    );
    sessionStorage.setItem(refreshKey, x.refreshToken);
    setToken(x.accessToken);
  };

  const request = useCallback(
    async <T,>(path: string, init: RequestInit = {}) => {
      let current = token;
      if (!current) current = await rotate();
      const send = (t: string) =>
        fetch(apiUrl(path), {
          ...init,
          headers: { ...init.headers, Authorization: `Bearer ${t}` },
        });
      let r = await send(current);
      if (r.status === 401) {
        current = await rotate();
        r = await send(current);
      }
      return json<T>(r);
    },
    [token, rotate]
  );

  const logout = async () => {
    const refreshToken = sessionStorage.getItem(refreshKey);
    try {
      if (refreshToken && token)
        await fetch(apiUrl('/v1/auth/logout'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
          body: JSON.stringify({ refreshToken }),
        });
    } finally {
      sessionStorage.removeItem(refreshKey);
      setToken(undefined);
    }
  };

  const value = useMemo(
    () => ({ ready, authenticated: !!token, login, logout, request }),
    [ready, token, request]
  );
  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export const useAuth = () => {
  const x = useContext(Context);
  if (!x) throw new Error('AuthProvider missing');
  return x;
};
