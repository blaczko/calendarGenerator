import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { Upload, X } from "lucide-react";
import { client, type Order, type OrderEvent } from "../api/client";
import { hu } from "../i18n/hu";

export default function EditOrderPage() {
  const { token = "" } = useParams();
  const [order, setOrder] = useState<Order | null>(null);
  const [error, setError] = useState("");
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [calendarYear, setCalendarYear] = useState(2027);
  const [years, setYears] = useState<number[]>([]);
  const [maxPhotos, setMaxPhotos] = useState(6);
  const [comments, setComments] = useState("");
  const [events, setEvents] = useState<OrderEvent[]>([]);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    client.config().then((c) => {
      setYears(c.supportedYears);
      setMaxPhotos(c.maxPhotosPerMonth);
    });
    client
      .getOrderByToken(token)
      .then((r) => {
        setOrder(r.order);
        setFullName(r.order.fullName);
        setPhone(r.order.phone);
        setEmail(r.order.email ?? "");
        setCalendarYear(r.order.calendarYear);
        setComments(r.order.comments ?? "");
        setEvents(
          r.order.events.map((e) => ({
            eventDate: String(e.eventDate).slice(0, 10),
            description: e.description,
          }))
        );
      })
      .catch((e) => setError(e.message));
  }, [token]);

  async function save() {
    setError("");
    setSaved(false);
    setBusy(true);
    try {
      const res = await client.updateOrder(token, {
        fullName,
        phone,
        email: email || null,
        calendarYear,
        comments,
        events,
        privacyConsent: true,
      });
      setOrder(res.order);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Mentési hiba");
    } finally {
      setBusy(false);
    }
  }

  async function onUpload(month: number, files: FileList | null) {
    if (!files?.length || !order) return;
    setError("");
    try {
      let current = order.photos.filter((p) => p.month === month).length;
      let latest = order;
      for (let i = 0; i < files.length; i++) {
        if (current >= maxPhotos) break;
        const res = await client.uploadPhoto(token, month, files[i]);
        latest = res.order;
        current = latest.photos.filter((p) => p.month === month).length;
      }
      setOrder(latest);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Feltöltési hiba");
    }
  }

  async function removePhoto(photoId: string) {
    const res = await client.deletePhoto(token, photoId);
    setOrder(res.order);
  }

  if (error && !order) {
    return (
      <main className="mx-auto max-w-lg px-4 py-16">
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-red-800">{error}</div>
        <Link to="/" className="mt-4 inline-block text-[var(--accent)]">
          Új megrendelés
        </Link>
      </main>
    );
  }

  if (!order) return <main className="p-8 text-center">Betöltés…</main>;

  return (
    <main className="mx-auto max-w-3xl space-y-6 px-4 py-10">
      <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950">
        {hu.editWarning}
      </div>
      <h1 className="text-3xl">Megrendelés szerkesztése</h1>
      <p className="text-[var(--muted)]">Azonosító: {order.publicOrderId}</p>

      <div className="grid gap-4 rounded-2xl border border-[var(--line)] bg-[var(--card)] p-6 md:grid-cols-2">
        <label className="text-sm">
          Név
          <input
            className="mt-1 w-full rounded-xl border border-[var(--line)] px-3 py-2"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
          />
        </label>
        <label className="text-sm">
          Telefon (România)
          <input
            className="mt-1 w-full rounded-xl border border-[var(--line)] px-3 py-2"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="+40 712 345 678"
            inputMode="tel"
            autoComplete="tel"
          />
        </label>
        <label className="text-sm">
          E-mail
          <input
            className="mt-1 w-full rounded-xl border border-[var(--line)] px-3 py-2"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label className="text-sm">
          Naptár év
          <select
            className="mt-1 w-full rounded-xl border border-[var(--line)] px-3 py-2"
            value={calendarYear}
            onChange={(e) => setCalendarYear(Number(e.target.value))}
          >
            {(years.length ? years : [calendarYear]).map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm md:col-span-2">
          Megjegyzés
          <textarea
            className="mt-1 w-full rounded-xl border border-[var(--line)] px-3 py-2"
            value={comments}
            onChange={(e) => setComments(e.target.value)}
          />
        </label>
        <button
          type="button"
          disabled={busy}
          onClick={save}
          className="rounded-full bg-[var(--accent)] px-5 py-2 text-white disabled:opacity-60"
        >
          Mentés
        </button>
        {saved ? <p className="text-sm text-[var(--ok)]">Mentve.</p> : null}
        {error ? <p className="text-sm text-red-700 md:col-span-2">{error}</p> : null}
      </div>

      <section className="space-y-4 rounded-2xl border border-[var(--line)] bg-white/70 p-6">
        <h2 className="text-xl">Fényképek ({order.photos.length})</h2>
        {hu.months.map((name, idx) => {
          const month = idx + 1;
          const list = order.photos
            .filter((p) => p.month === month)
            .sort((a, b) => a.sortOrder - b.sortOrder);
          return (
            <div key={name} className="rounded-xl border border-[var(--line)] p-3">
              <div className="mb-2 flex items-center justify-between text-sm">
                <strong>{name}</strong>
                <span>
                  {list.length} / {maxPhotos} (1–{maxPhotos})
                </span>
              </div>
              <label
                className={`mb-2 flex cursor-pointer items-center gap-2 rounded-lg border border-dashed border-[var(--accent)]/40 px-3 py-2 text-sm text-[var(--muted)] ${
                  list.length >= maxPhotos ? "pointer-events-none opacity-50" : ""
                }`}
              >
                <Upload className="h-4 w-4" />
                {list.length >= maxPhotos
                  ? `Maximum ${maxPhotos} fénykép`
                  : `Feltöltés (1–${maxPhotos - list.length})`}
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
              <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
                {list.map((p) => (
                  <div key={p.id} className="relative">
                    <img
                      src={client.photoUrl(token, p.id)}
                      alt={p.originalFilename}
                      className="aspect-square w-full rounded-lg object-cover"
                    />
                    <button
                      type="button"
                      className="absolute right-1 top-1 rounded-full bg-black/60 p-1 text-white"
                      onClick={() => removePhoto(p.id)}
                    >
                      <X className="h-3.5 w-3.5" />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </section>

      <section className="space-y-3 rounded-2xl border border-[var(--line)] bg-white/70 p-6">
        <div className="flex items-center justify-between">
          <h2 className="text-xl">Események</h2>
          <button
            type="button"
            className="rounded-full border border-[var(--line)] px-3 py-1 text-sm"
            onClick={() =>
              setEvents([
                ...events,
                { eventDate: `${calendarYear}-01-01`, description: "" },
              ])
            }
          >
            Hozzáadás
          </button>
        </div>
        {events.map((ev, i) => (
          <div key={i} className="grid gap-2 md:grid-cols-[160px_1fr_auto]">
            <input
              type="date"
              className="rounded-xl border border-[var(--line)] px-3 py-2"
              value={ev.eventDate}
              onChange={(e) => {
                const next = [...events];
                next[i] = { ...ev, eventDate: e.target.value };
                setEvents(next);
              }}
            />
            <input
              className="rounded-xl border border-[var(--line)] px-3 py-2"
              value={ev.description}
              placeholder="Leírás"
              onChange={(e) => {
                const next = [...events];
                next[i] = { ...ev, description: e.target.value };
                setEvents(next);
              }}
            />
            <button
              type="button"
              className="rounded-full border border-[var(--line)] px-3 py-2 text-sm"
              onClick={() => setEvents(events.filter((_, j) => j !== i))}
            >
              Törlés
            </button>
          </div>
        ))}
        <button
          type="button"
          disabled={busy}
          onClick={save}
          className="rounded-full bg-[var(--accent)] px-5 py-2 text-white"
        >
          Események mentése
        </button>
      </section>
    </main>
  );
}
