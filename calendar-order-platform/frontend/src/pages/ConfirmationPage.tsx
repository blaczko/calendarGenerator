import { useLocation, Link } from "react-router-dom";
import { Copy, Check } from "lucide-react";
import { useState } from "react";
import { hu } from "../i18n/hu";

export default function ConfirmationPage() {
  const location = useLocation();
  const state = (location.state ?? {}) as {
    orderId?: string;
    editToken?: string;
    editUrl?: string;
  };
  const [copied, setCopied] = useState(false);
  const editUrl =
    state.editUrl ??
    (state.editToken ? `${window.location.origin}/edit/${state.editToken}` : "");

  if (!state.orderId) {
    return (
      <main className="mx-auto max-w-xl px-4 py-16 text-center">
        <p>Nincs megerősítendő megrendelés.</p>
        <Link to="/" className="mt-4 inline-block text-[var(--accent)]">
          Vissza a kezdőlapra
        </Link>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <div className="rounded-3xl border border-[var(--line)] bg-[var(--card)] p-8 shadow-lg">
        <p className="text-sm uppercase tracking-widest text-[var(--ok)]">Sikeres megrendelés</p>
        <h1 className="mt-2 text-3xl">Köszönjük!</h1>
        <p className="mt-2 text-[var(--muted)]">
          A megrendelés azonosítója: <strong>{state.orderId}</strong>
        </p>
        <div className="mt-6 rounded-2xl bg-[#f3ebe0] p-4">
          <p className="text-sm font-semibold">Szerkesztő link</p>
          <p className="mt-1 break-all text-sm">{editUrl}</p>
          <button
            type="button"
            className="mt-3 inline-flex items-center gap-2 rounded-full bg-[var(--accent)] px-4 py-2 text-sm text-white"
            onClick={async () => {
              await navigator.clipboard.writeText(editUrl);
              setCopied(true);
            }}
          >
            {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
            {copied ? "Másolva" : "Link másolása"}
          </button>
          <p className="mt-3 text-xs text-[var(--muted)]">{hu.editWarning}</p>
        </div>
        <Link to={`/edit/${state.editToken}`} className="mt-6 inline-block text-[var(--accent-deep)]">
          Megrendelés szerkesztése
        </Link>
      </div>
    </main>
  );
}
