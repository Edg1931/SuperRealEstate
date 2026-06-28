import { createClient } from "@supabase/supabase-js";

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL ?? "";
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? "";

/**
 * True when both public Supabase env vars are present. When false, a fresh
 * deploy is missing config — pages can render <EnvNotice /> instead of failing
 * silently against an empty URL/key.
 */
export function isSupabaseConfigured(): boolean {
  return supabaseUrl.length > 0 && supabaseAnonKey.length > 0;
}

// Public anon client — safe in the browser; row-level security protects data.
// (Private data like saved rooms requires the user to be signed in.)
//
// createClient() throws on empty url/key, so when unconfigured we pass harmless
// placeholders: the module still imports (no crash on a fresh deploy) and pages
// gate behind isSupabaseConfigured() to render <EnvNotice /> instead of querying.
// A stray request against the placeholder simply errors, which existing pages
// already handle via their `error` branch.
export const supabase = createClient(
  supabaseUrl || "https://placeholder.supabase.co",
  supabaseAnonKey || "placeholder-anon-key",
);

export type MaterialRow = {
  id: string;
  name: string;
  category: string;
  unit: string;
  price_per_unit: number;
  brand: string | null;
  product_code: string | null;
  color_hex: string | null;
  buy_url: string | null;
};
