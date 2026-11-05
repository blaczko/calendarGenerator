import { useEffect, useMemo, useState, type DragEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Check, ChevronLeft, ChevronRight, Upload, X } from "lucide-react";
import { client } from "../api/client";
import { hu } from "../i18n/hu";

type DraftEvent = { eventDate: string; description: string };

const emptyDraft = {
  fullName: "",
  phone: "",
  email: "",
  calendarYear: new Date().getFullYear() + 1,
  calendarLanguage: "hu",
  calendarFormatPref: "portrait",
  comments: "",
  privacyConsent: false,
  events: [] as DraftEvent[],
};

export default function CustomerWizard() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [draft, setDraft] = useState(emptyDraft);
  const [years, setYears] = useState<number[]>([]);
  const [maxPhotos, setMaxPhotos] = useState(6);
  const [token, setToken] = useState<string | null>(null);
  const [orderId, setOrderId] = useState<string | null>(null);
  const [photosByMonth, setPhotosByMonth] = useState<Record<number, { id: string; name: string; url: string }[]>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uploadMsg, setUploadMsg] = useState<Record<number, string>>({});

  useEffect(() => {
    client.config().then((c) => {
      setYears(c.supportedYears);
      setMaxPhotos(c.maxPhotosPerMonth);
      setDraft((d) => ({
        ...d,
        calendarYear: c.supportedYears.includes(d.calendarYear)
          ? d.calendarYear
          : c.supportedYears[0],
      }));
    });
  }, []);

  const canNext = useMemo(() => {
    if (step === 0) return draft.fullName.trim() && draft.phone.trim();
    if (step === 1)
      return (
        !!draft.calendarYear &&
        (draft.calendarFormatPref === "portrait" ||
          draft.calendarFormatPref === "landscape")
      );
    if (step === 4) return draft.privacyConsent;
    return true;
  }, [step, draft]);

  async function ensureOrder(requireConsent = false): Promise<string> {
    if (token) return token;
    if (requireConsent && !draft.privacyConsent) {
      throw new Error("Az adatkezelési hozzájárulás kötelező.");
    }
    setBusy(true);
    try {
      const res = await client.createOrder({
        ...draft,
        email: draft.email || null,
        events: draft.events,
        privacyConsent: requireConsent ? true : draft.privacyConsent,
      });
      setToken(res.editToken);
      setOrderId(res.orderId);
      sessionStorage.setItem(
        "cop_last_order",
        JSON.stringify({ orderId: res.orderId, editToken: res.editToken, editUrl: res.editUrl })
      );
      return res.editToken;
    } finally {
      setBusy(false);
    }
  }

  async function onUpload(month: number, files: FileList | null) {
    if (!files?.length) return;
    setError("");
    setUploadMsg((m) => ({ ...m, [month]: "" }));
    try {
      const t = await ensureOrder();
      const current = photosByMonth[month]?.length ?? 0;
      const remaining = maxPhotos - current;
      if (remaining <= 0) {
        setUploadMsg((m) => ({
          ...m,
          [month]: `Ebben a hónapban már ${maxPhotos} fénykép van (maximum ${maxPhotos}).`,
        }));
        return;
      }
      const selected = Array.from(files);
      if (selected.length > remaining) {
        setUploadMsg((m) => ({
          ...m,
          [month]: `Havonként 1–${maxPhotos} fénykép tölthető fel. Most még ${remaining} fér el; az első ${remaining} fájlt töltjük fel.`,
        }));
      }
      const toUpload = selected.slice(0, remaining);
      for (const file of toUpload) {
        const res = await client.uploadPhoto(t, month, file);
        const monthPhotos = res.order.photos
          .filter((p) => p.month === month)
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .map((p) => ({
            id: p.id,
            name: p.originalFilename,
            url: client.photoUrl(t, p.id),
          }));
        setPhotosByMonth((prev) => ({ ...prev, [month]: monthPhotos }));
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Feltöltési hiba");
    }
  }

  async function removePhoto(month: number, photoId: string) {
    if (!token) return;
    const res = await client.deletePhoto(token, photoId);
    setPhotosByMonth((prev) => ({
      ...prev,
      [month]: res.order.photos
        .filter((p) => p.month === month)
        .map((p) => ({ id: p.id, name: p.originalFilename, url: client.photoUrl(token, p.id) })),
    }));
  }

  async function movePhoto(month: number, photoId: string, direction: -1 | 1) {
    if (!token) return;
    const list = [...(photosByMonth[month] ?? [])];
    const idx = list.findIndex((p) => p.id === photoId);
    const next = idx + direction;
    if (idx < 0 || next < 0 || next >= list.length) return;
    const [item] = list.splice(idx, 1);
    list.splice(next, 0, item);
    setPhotosByMonth((prev) => ({ ...prev, [month]: list }));
    const res = await client.reorderPhotos(
      token,
      month,
      list.map((p) => p.id)
    );
    setPhotosByMonth((prev) => ({
      ...prev,
      [month]: res.order.photos
        .filter((p) => p.month === month)
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((p) => ({
          id: p.id,
          name: p.originalFilename,
          url: client.photoUrl(token, p.id),
        })),
    }));
  }

  function onDropFiles(month: number, e: DragEvent) {
    e.preventDefault();
    e.stopPropagation();
    if (e.dataTransfer.files?.length) {
      void onUpload(month, e.dataTransfer.files);
    }
  }

  async function submitFinal() {
    setError("");
    if (!draft.privacyConsent) {
      setError("Az adatkezelési hozzájárulás kötelező.");
      return;
    }
    setBusy(true);
    try {
      let t = token;
      if (!t) {
        t = await ensureOrder();
      } else {
        await client.updateOrder(t, {
          fullName: draft.fullName,
          phone: draft.phone,
          email: draft.email || null,
          calendarYear: draft.calendarYear,
          calendarLanguage: draft.calendarLanguage,
          calendarFormatPref: draft.calendarFormatPref || null,
          comments: draft.comments || null,
          privacyConsent: true,
          events: draft.events,
        });
      }
      const stored = sessionStorage.getItem("cop_last_order");
      navigate("/confirmation", { state: stored ? JSON.parse(stored) : { orderId, editToken: t } });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Mentési hiba");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="mx-auto max-w-4xl px-4 py-8">
      <div className="mb-8 overflow-hidden rounded-3xl border border-[var(--line)] bg-[var(--card)] shadow-[0_20px_60px_rgba(80,50,20,0.08)]">
        <div className="bg-[linear-gradient(120deg,#3d2a1f,#8a3c1c_45%,#c47a3a)] px-6 py-10 text-[#fff8f1] md:px-10">
          <p className="mb-2 text-sm uppercase tracking-[0.2em] text-white/70">Megrendelés</p>
          <h1 className="max-w-2xl text-3xl leading-tight md:text-4xl">{hu.title}</h1>
          <p className="mt-3 max-w-2xl text-base text-white/85">{hu.subtitle}</p>
        </div>

        <div className="border-b border-[var(--line)] px-4 py-4 md:px-8">
          <ol className="flex flex-wrap gap-2">
            {hu.steps.map((label, i) => (
              <li key={label}>
                <button
                  type="button"
                  onClick={() => setStep(i)}
                  className={`rounded-full px-3 py-1.5 text-sm ${
                    i === step
                      ? "bg-[var(--accent)] text-white"
                      : i < step
                        ? "bg-[#efe4d6] text-[var(--ink)]"
                        : "bg-transparent text-[var(--muted)]"
                  }`}
                >
                  {i < step ? <Check className="mr-1 inline h-3.5 w-3.5" /> : null}
                  {i + 1}. {label}
                </button>
              </li>
            ))}
          </ol>
        </div>

        <div className="space-y-6 px-4 py-6 md:px-8">
          {error ? (
            <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
              {error}
            </div>
          ) : null}

          {step === 0 && (
            <section className="grid gap-4 md:grid-cols-2">
              <Field label="Teljes név *">
                <input
                  className="field"
                  value={draft.fullName}
                  onChange={(e) => setDraft({ ...draft, fullName: e.target.value })}
                />
              </Field>
              <Field label="Telefonszám (România) *">
                <input
                  className="field"
                  value={draft.phone}
                  onChange={(e) => setDraft({ ...draft, phone: e.target.value })}
                  placeholder="+40 712 345 678"
                  inputMode="tel"
                  autoComplete="tel"
                />
                <p className="mt-1 text-xs text-[var(--muted)]">
                  Csak román szám: +40… vagy 07… / 02… / 03…
                </p>
              </Field>
              <Field label="E-mail (opcionális)">
                <input
                  className="field"
                  type="email"
                  value={draft.email}
                  onChange={(e) => setDraft({ ...draft, email: e.target.value })}
                />
              </Field>
              <Field label="Megjegyzés (opcionális)">
                <textarea
                  className="field min-h-24"
                  value={draft.comments}
                  onChange={(e) => setDraft({ ...draft, comments: e.target.value })}
                />
              </Field>
            </section>
          )}

          {step === 1 && (
            <section className="grid gap-4 md:grid-cols-2">
              <Field label="Naptár éve *">
                <select
                  className="field"
                  value={draft.calendarYear}
                  onChange={(e) =>
                    setDraft({ ...draft, calendarYear: Number(e.target.value) })
                  }
                >
                  {years.map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Nyelv">
                <select
                  className="field"
                  value={draft.calendarLanguage}
                  onChange={(e) => setDraft({ ...draft, calendarLanguage: e.target.value })}
                >
                  <option value="hu">Magyar</option>
                  <option value="ro" disabled>
                    Română (hamarosan)
                  </option>
                  <option value="en" disabled>
                    English (soon)
                  </option>
                </select>
              </Field>
              <Field label="Formátum *">
                <select
                  className="field"
                  value={draft.calendarFormatPref}
                  onChange={(e) =>
                    setDraft({ ...draft, calendarFormatPref: e.target.value })
                  }
                >
                  <option value="" disabled>
                    Válasszon formátumot
                  </option>
                  <option value="portrait">Álló (portrait) – A4</option>
                  <option value="landscape">Fekvő (landscape) – A4</option>
                </select>
              </Field>
            </section>
          )}

          {step === 2 && (
            <section className="space-y-4">
              <p className="text-sm text-[var(--muted)]">
                Havonként 1–{maxPhotos} fénykép tölthető fel (JPG, PNG, WebP). Nem kötelező minden
                hónapra fényképet adni. A feltöltés mentést indít, és egyedi szerkesztő linket generál.
              </p>
              {hu.months.map((name, idx) => {
                const month = idx + 1;
                const list = photosByMonth[month] ?? [];
                return (
                  <div
                    key={name}
                    className="rounded-2xl border border-[var(--line)] bg-white/70 p-4"
                  >
                    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                      <h3 className="text-lg">{name}</h3>
                      <span className="text-sm text-[var(--muted)]">
                        {list.length} / {maxPhotos} (1–{maxPhotos} fénykép)
                      </span>
                    </div>
                    <label
                      className={`flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-[var(--accent)]/40 bg-[#fff7ef] px-4 py-6 text-sm text-[var(--muted)] ${
                        list.length >= maxPhotos ? "pointer-events-none opacity-50" : ""
                      }`}
                      onDragOver={(e) => e.preventDefault()}
                      onDrop={(e) => onDropFiles(month, e)}
                    >
                      <Upload className="h-5 w-5 text-[var(--accent)]" />
                      {list.length >= maxPhotos
                        ? `Maximum ${maxPhotos} fénykép elérve`
                        : `Húzza ide vagy válasszon 1–${maxPhotos - list.length} fényképet`}
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        multiple
                        className="hidden"
                        disabled={list.length >= maxPhotos}
                        onChange={(e) => {
                          void onUpload(month, e.target.files);
                          e.target.value = "";
                        }}
                      />
                    </label>
                    {uploadMsg[month] ? (
                      <p className="mt-2 text-sm text-red-700">{uploadMsg[month]}</p>
                    ) : null}
                    <div className="mt-3 grid grid-cols-3 gap-2 sm:grid-cols-6">
                      {list.map((p, photoIdx) => (
                        <div
                          key={p.id}
                          draggable
                          onDragStart={(e) => e.dataTransfer.setData("text/photo-id", p.id)}
                          onDragOver={(e) => e.preventDefault()}
                          onDrop={async (e) => {
                            e.preventDefault();
                            const fromId = e.dataTransfer.getData("text/photo-id");
                            if (!fromId || fromId === p.id || !token) return;
                            const ids = list.map((x) => x.id);
                            const from = ids.indexOf(fromId);
                            const to = ids.indexOf(p.id);
                            if (from < 0 || to < 0) return;
                            ids.splice(to, 0, ids.splice(from, 1)[0]);
                            const res = await client.reorderPhotos(token, month, ids);
                            setPhotosByMonth((prev) => ({
                              ...prev,
                              [month]: res.order.photos
                                .filter((ph) => ph.month === month)
                                .sort((a, b) => a.sortOrder - b.sortOrder)
                                .map((ph) => ({
                                  id: ph.id,
                                  name: ph.originalFilename,
                                  url: client.photoUrl(token, ph.id),
                                })),
                            }));
                          }}
                          className="relative overflow-hidden rounded-lg border border-[var(--line)]"
                        >
                          <img src={p.url} alt={p.name} className="aspect-square w-full object-cover" />
                          <button
                            type="button"
                            className="absolute right-1 top-1 rounded-full bg-black/60 p-1 text-white"
                            onClick={() => removePhoto(month, p.id)}
                          >
                            <X className="h-3.5 w-3.5" />
                          </button>
                          <div className="absolute bottom-1 left-1 flex gap-1">
                            <button
                              type="button"
                              className="rounded bg-black/55 px-1 text-[10px] text-white disabled:opacity-30"
                              disabled={photoIdx === 0}
                              onClick={() => movePhoto(month, p.id, -1)}
                            >
                              ←
                            </button>
                            <button
                              type="button"
                              className="rounded bg-black/55 px-1 text-[10px] text-white disabled:opacity-30"
                              disabled={photoIdx === list.length - 1}
                              onClick={() => movePhoto(month, p.id, 1)}
                            >
                              →
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                );
              })}
            </section>
          )}

          {step === 3 && (
            <section className="space-y-4">
              <button
                type="button"
                className="btn-secondary"
                onClick={() =>
                  setDraft({
                    ...draft,
                    events: [
                      ...draft.events,
                      {
                        eventDate: `${draft.calendarYear}-01-01`,
                        description: "",
                      },
                    ],
                  })
                }
              >
                Esemény hozzáadása
              </button>
              {draft.events.map((ev, i) => (
                <div key={i} className="grid gap-3 rounded-xl border border-[var(--line)] p-3 md:grid-cols-[180px_1fr_auto]">
                  <input
                    type="date"
                    className="field"
                    value={ev.eventDate}
                    onChange={(e) => {
                      const events = [...draft.events];
                      events[i] = { ...ev, eventDate: e.target.value };
                      setDraft({ ...draft, events });
                    }}
                  />
                  <input
                    className="field"
                    placeholder="pl. Boti születésnapja"
                    value={ev.description}
                    onChange={(e) => {
                      const events = [...draft.events];
                      events[i] = { ...ev, description: e.target.value };
                      setDraft({ ...draft, events });
                    }}
                  />
                  <button
                    type="button"
                    className="btn-secondary"
                    onClick={() =>
                      setDraft({
                        ...draft,
                        events: draft.events.filter((_, j) => j !== i),
                      })
                    }
                  >
                    Törlés
                  </button>
                </div>
              ))}
            </section>
          )}

          {step === 4 && (
            <section className="space-y-4">
              <SummaryRow label="Név" value={draft.fullName} />
              <SummaryRow label="Telefon" value={draft.phone} />
              <SummaryRow label="E-mail" value={draft.email || "—"} />
              <SummaryRow label="Év" value={String(draft.calendarYear)} />
              <SummaryRow
                label="Formátum"
                value={
                  draft.calendarFormatPref === "landscape"
                    ? "Fekvő (landscape) – A4"
                    : "Álló (portrait) – A4"
                }
              />
              <SummaryRow
                label="Fényképek"
                value={String(
                  Object.values(photosByMonth).reduce((n, a) => n + a.length, 0)
                )}
              />
              <div>
                <h3 className="mb-2 font-semibold">Események</h3>
                <ul className="space-y-1 text-sm">
                  {draft.events.length === 0 ? <li>—</li> : null}
                  {[...draft.events]
                    .sort((a, b) => a.eventDate.localeCompare(b.eventDate))
                    .map((e, i) => (
                      <li key={i}>
                        {e.eventDate} — {e.description}
                      </li>
                    ))}
                </ul>
              </div>
              <label className="flex items-start gap-3 rounded-xl bg-[#f3ebe0] p-4 text-sm">
                <input
                  type="checkbox"
                  checked={draft.privacyConsent}
                  onChange={(e) =>
                    setDraft({ ...draft, privacyConsent: e.target.checked })
                  }
                  className="mt-1"
                />
                <span>{hu.privacy}</span>
              </label>
            </section>
          )}

          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--line)] pt-4">
            <button
              type="button"
              className="btn-secondary"
              disabled={step === 0 || busy}
              onClick={() => setStep((s) => Math.max(0, s - 1))}
            >
              <ChevronLeft className="mr-1 inline h-4 w-4" /> Vissza
            </button>
            {step < 4 ? (
              <button
                type="button"
                className="btn-primary"
                disabled={!canNext || busy}
                onClick={() => setStep((s) => s + 1)}
              >
                Tovább <ChevronRight className="ml-1 inline h-4 w-4" />
              </button>
            ) : (
              <button
                type="button"
                className="btn-primary"
                disabled={!canNext || busy}
                onClick={submitFinal}
              >
                {busy ? "Küldés…" : "Megrendelés elküldése"}
              </button>
            )}
          </div>
        </div>
      </div>

      <style>{`
        .field { width:100%; border:1px solid var(--line); border-radius:0.75rem; padding:0.65rem 0.8rem; background:#fff; }
        .btn-primary { background:var(--accent); color:white; border-radius:999px; padding:0.7rem 1.2rem; font-weight:600; }
        .btn-primary:disabled { opacity:0.5; }
        .btn-secondary { border:1px solid var(--line); border-radius:999px; padding:0.65rem 1rem; background:#fff; }
      `}</style>
    </main>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-1.5 text-sm">
      <span className="font-semibold text-[var(--ink)]">{label}</span>
      {children}
    </label>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 border-b border-[var(--line)] py-2 text-sm">
      <span className="text-[var(--muted)]">{label}</span>
      <span className="font-medium">{value}</span>
    </div>
  );
}
