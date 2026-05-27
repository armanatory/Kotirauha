import axios from "axios";

export const api = axios.create({
  baseURL: "/api/v1",
});

const TOKEN_KEY = "kotirauha_token";

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

api.interceptors.request.use((config) => {
  const token = getToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error?.response?.status === 401 && getToken()) {
      setToken(null);
      if (!location.pathname.startsWith("/login")) location.href = "/login";
    }
    return Promise.reject(error);
  },
);
