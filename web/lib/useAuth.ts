"use client";

import { useCallback, useEffect, useState } from "react";
import { supabase } from "@/lib/supabase";

/**
 * Result of attempting to send a magic link. Mirrors the inline pattern used by
 * the design page: on failure we surface the Supabase error message so callers
 * can render it; on success the caller can tell the user to check their inbox.
 */
export interface SendMagicLinkResult {
  ok: boolean;
  error: string | null;
}

export interface UseAuth {
  /** The signed-in user's id, or null when signed out. */
  userId: string | null;
  /** The signed-in user's email (best-effort), or null when signed out. */
  email: string | null;
  /** False until the initial getUser() check has resolved. */
  authChecked: boolean;
  /** Send a magic link to the given email (uses the current URL as the redirect). */
  sendMagicLink: (email: string) => Promise<SendMagicLinkResult>;
  /** Sign the current user out. */
  signOut: () => Promise<void>;
}

/**
 * Small reusable client-side auth hook wrapping `supabase.auth`, factored out of
 * the inline pattern in `app/design/page.tsx`:
 *  - `getUser()` once on mount to seed state,
 *  - an `onAuthStateChange` subscription (cleaned up on unmount),
 *  - `signInWithOtp` with `emailRedirectTo = window.location.href`,
 *  - `signOut()`.
 *
 * Pure client hook — it touches `window`, so only call it from `"use client"`
 * components.
 */
export function useAuth(): UseAuth {
  const [userId, setUserId] = useState<string | null>(null);
  const [email, setEmail] = useState<string | null>(null);
  const [authChecked, setAuthChecked] = useState(false);

  useEffect(() => {
    let active = true;

    (async () => {
      const { data } = await supabase.auth.getUser();
      if (!active) return;
      setUserId(data.user?.id ?? null);
      setEmail(data.user?.email ?? null);
      setAuthChecked(true);
    })();

    const { data: sub } = supabase.auth.onAuthStateChange((_event, session) => {
      setUserId(session?.user?.id ?? null);
      setEmail(session?.user?.email ?? null);
      setAuthChecked(true);
    });

    return () => {
      active = false;
      sub.subscription.unsubscribe();
    };
  }, []);

  const sendMagicLink = useCallback(
    async (rawEmail: string): Promise<SendMagicLinkResult> => {
      const trimmed = rawEmail.trim();
      if (!trimmed) {
        return { ok: false, error: "Enter an email address first." };
      }
      const { error } = await supabase.auth.signInWithOtp({
        email: trimmed,
        options:
          typeof window !== "undefined"
            ? { emailRedirectTo: window.location.href }
            : undefined,
      });
      if (error) {
        return { ok: false, error: error.message };
      }
      return { ok: true, error: null };
    },
    [],
  );

  const signOut = useCallback(async (): Promise<void> => {
    await supabase.auth.signOut();
  }, []);

  return { userId, email, authChecked, sendMagicLink, signOut };
}
