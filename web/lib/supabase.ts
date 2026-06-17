import { createClient } from "@supabase/supabase-js";

// Public anon client — safe in the browser; row-level security protects data.
// (Private data like saved rooms requires the user to be signed in.)
export const supabase = createClient(
  process.env.NEXT_PUBLIC_SUPABASE_URL ?? "",
  process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ?? "",
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
