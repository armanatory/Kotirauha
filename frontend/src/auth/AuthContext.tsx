import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, getToken, setToken } from "@/lib/api";

export interface Membership {
  buildingId: string;
  buildingName: string;
  sharedLanguage: string;
  role: "resident" | "board" | "admin";
  apartmentNumber: string | null;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  preferredLanguage: string;
  membership: Membership | null;
}

interface AuthState {
  user: CurrentUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (input: { email: string; password: string; displayName: string; preferredLanguage: string }) => Promise<void>;
  logout: () => void;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);

  async function refresh() {
    if (!getToken()) {
      setUser(null);
      setLoading(false);
      return;
    }
    try {
      const { data } = await api.get<CurrentUser>("/auth/me");
      setUser(data);
    } catch {
      setUser(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  async function login(email: string, password: string) {
    const { data } = await api.post<{ token: string }>("/auth/login", { email, password });
    setToken(data.token);
    await refresh();
  }

  async function register(input: { email: string; password: string; displayName: string; preferredLanguage: string }) {
    const { data } = await api.post<{ token: string }>("/auth/register", input);
    setToken(data.token);
    await refresh();
  }

  function logout() {
    setToken(null);
    setUser(null);
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout, refresh }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
