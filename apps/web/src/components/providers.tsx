"use client";

import {
  MutationCache,
  QueryCache,
  QueryClient,
  QueryClientProvider,
} from "@tanstack/react-query";
import { MotionConfig } from "motion/react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast, Toaster } from "sonner";

import { ApiError } from "@/lib/api/client";

export function Providers({ children }: { children: React.ReactNode }) {
  const router = useRouter();

  const [client] = useState(() => {
    // Any 401 inside /app means the session expired — drop everything and
    // send the user back to login with a return path.
    const onAuthError = (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        queryClient.clear();
        const path = window.location.pathname + window.location.search;
        if (path.startsWith("/app")) {
          router.replace(`/login?next=${encodeURIComponent(path)}`);
        }
      }
    };
    const queryClient = new QueryClient({
      queryCache: new QueryCache({ onError: onAuthError }),
      mutationCache: new MutationCache({ onError: onAuthError }),
      defaultOptions: {
        queries: {
          staleTime: 15_000,
          retry: (count, error) => {
            if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
              return false;
            }
            return count < 2;
          },
        },
      },
    });
    return queryClient;
  });

  return (
    <QueryClientProvider client={client}>
      <MotionConfig reducedMotion="user">
        {children}
        <Toaster position="top-center" closeButton />
      </MotionConfig>
    </QueryClientProvider>
  );
}

export { toast };
