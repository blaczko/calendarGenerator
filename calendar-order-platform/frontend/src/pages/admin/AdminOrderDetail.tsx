import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { client, type Order } from "../../api/client";

const STATUSES = ["NEW", "IN_PROGRESS", "WAITING_FOR_CUSTOMER", "COMPLETED", "CANCELLED"];

export default function AdminOrderDetail() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const [order, setOrder] = useState<Order | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    client
      .adminMe()
      .then(() => client.adminOrder(id))
      .then((r) => setOrder(r.order))
      .catch(() => navigate("/admin"));
  }, [id, navigate]);

  if (!order) return <main className="p-8">Betöltés…</main>;

  return (
    <main className="mx-auto max-w-4xl space-y-6 px-4 py-8">
      <Link to="/admin/dashboard" className="text-sm text-[var(--accent-deep)]">
        ← Vissza
      </Link>
      <h1 className="text-3xl">{order.publicOrderId}</h1>
      <div className="grid gap-2 rounded-2xl border border-[var(--line)] bg-white p-5 text-sm md:grid-cols-2">
        <div>Név: {order.fullName}</div>
        <div>Telefon: {order.phone}</div>
        <div>E-mail: {order.email || "—"}</div>
        <div>Év: {order.calendarYear}</div>
        <div>Státusz: {order.status}</div>
        <div>Szerkesztő link: {order.editTokenRevoked ? "visszavonva" : "aktív"}</div>
      </div>

      <div className="flex flex-wrap gap-2">
        <select
          className="rounded-xl border border-[var(--line)] px-3 py-2"
          value={order.status}
          onChange={async (e) => {
            const res = await client.adminSetStatus(order.id, e.target.value);
            setOrder(res.order);
          }}
        >
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
        <a className="rounded-full border border-[var(--line)] px-4 py-2" href={`/api/admin/orders/${order.id}/export.json`}>
          JSON export
        </a>
        <a className="rounded-full border border-[var(--line)] px-4 py-2" href={`/api/admin/orders/${order.id}/export.csv`}>
          CSV export
        </a>
        <button
          type="button"
          className="rounded-full border border-[var(--line)] px-4 py-2"
          onClick={async () => {
            const res = await client.adminRevoke(order.id);
            setOrder(res.order);
          }}
        >
          Szerkesztő link visszavonása
        </button>
        <button
          type="button"
          className="rounded-full bg-red-700 px-4 py-2 text-white"
          onClick={async () => {
            if (!confirm("Biztosan törli a megrendelést?")) return;
            await client.adminDelete(order.id);
            navigate("/admin/dashboard");
          }}
        >
          Törlés
        </button>
      </div>
      {error ? <p className="text-red-700">{error}</p> : null}

      <section>
        <h2 className="mb-2 text-xl">Események</h2>
        <ul className="space-y-1 text-sm">
          {order.events.map((e) => (
            <li key={e.id}>
              {String(e.eventDate).slice(0, 10)} — {e.description}
            </li>
          ))}
        </ul>
      </section>

      <section>
        <h2 className="mb-2 text-xl">Fényképek</h2>
        <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
          {order.photos.map((p) => (
            <a key={p.id} href={client.adminPhotoUrl(order.id, p.id)} target="_blank" rel="noreferrer">
              <img
                src={client.adminPhotoUrl(order.id, p.id)}
                alt={p.originalFilename}
                className="aspect-square rounded-lg object-cover"
              />
            </a>
          ))}
        </div>
      </section>
    </main>
  );
}
