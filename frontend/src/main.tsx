import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Toaster } from "sonner";
import "./i18n";
import "./index.css";
import App from "./App.tsx";
import { AuthProvider } from "@/auth/AuthContext";

const queryClient = new QueryClient();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <App />
        <Toaster position="top-center" richColors />
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
);
