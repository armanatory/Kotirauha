import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, getToken, setToken } from "@/lib/api";
import i18n from "@/i18n";

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
  isAdmin: boolean;
  membership: Membership | null;
}

interface MagicLinkInput {
  email: string;
  displayName?: string;
  preferredLanguage?: string;
}

interface AuthState {
  user: CurrentUser | null;
  loading: boolean;
  requestMagicLink: (input: MagicLinkInput) => Promise<{ devLink?: string; devCode?: string }>;
  verify: (token: string) => Promise<{ profileComplete: boolean }>;
  verifyCode: (email: string, code: string) => Promise<{ profileComplete: boolean }>;
  updateProfile: (input: { displayName?: string; preferredLanguage?: string }) => Promise<void>;
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
      // Follow the user's preferred language unless they picked one manually.
      if (!localStorage.getItem("lang") && data.preferredLanguage) {
        void i18n.changeLanguage(data.preferredLanguage === "en" ? "en" : "fi");
      }
    } catch {
      setUser(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  async function requestMagicLink(input: MagicLinkInput) {
    const { data } = await api.post<{ sent: boolean; devLink?: string; devCode?: string }>("/auth/magic-link", input);
    return { devLink: data.devLink, devCode: data.devCode };
  }

  async function verify(token: string) {
    const { data } = await api.post<{ token: string; profileComplete: boolean }>("/auth/verify", { token });
    setToken(data.token);
    await refresh();
    return { profileComplete: data.profileComplete };
  }

  async function verifyCode(email: string, code: string) {
    const { data } = await api.post<{ token: string; profileComplete: boolean }>("/auth/verify-code", { email, code });
    setToken(data.token);
    await refresh();
    return { profileComplete: data.profileComplete };
  }

  async function updateProfile(input: { displayName?: string; preferredLanguage?: string }) {
    await api.patch("/auth/me", input);
    await refresh();
  }

  function logout() {
    setToken(null);
    setUser(null);
  }

  return (
    <AuthContext.Provider value={{ user, loading, requestMagicLink, verify, verifyCode, updateProfile, logout, refresh }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
