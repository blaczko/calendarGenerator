import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { client, type Order } from "../../api/client";

const STATUSES = ["NEW", "IN_PROGRESS", "WAITING_FOR_CUSTOMER", "COMPLETED", "CANCELLED"];

export default function AdminDashboard() {
  const navigate = useNavigate();
  const [orders, setOrders] = useState<Order[]>([]);
  const [stats, setStats] = useState<{ total: number; counts: Record<string, number> } | null>(null);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [year, setYear] = useState("");
  const [error, setError] = useState("");

  async function load() {
    try {
      await client.adminMe();
      const q = new URLSearchParams();
      if (search) q.set("search", search);
      if (status) q.set("status", status);
      if (year) q.set("year", year);
      const [list, st] = await Promise.all([client.adminOrders(q), client.adminStats()]);
      setOrders(list.orders);
      setStats(st);
    } catch {
      navigate("/admin");
    }
  }

  useEffect(() => {
    load().catch(() => navigate("/admin"));
  }, []);

  return (
    <main className="mx-auto max-w-6xl space-y-6 px-4 py-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-3xl">Admin irányítópult</h1>
        <button
          type="button"
          className="rounded-full border border-[var(--line)] px-4 py-2 text-sm"
          onClick={async () => {
            await client.adminLogout();
            navigate("/admin");
          }}
        >
          Kijelentkezés
        </button>
      </div>

      {stats ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Stat label="Összes" value={stats.total} />
          <Stat label="Új" value={stats.counts.NEW ?? 0} />
          <Stat label="Folyamatban" value={stats.counts.IN_PROGRESS ?? 0} />
          <Stat label="Várakozik" value={stats.counts.WAITING_FOR_CUSTOMER ?? 0} />
          <Stat label="Kész" value={stats.counts.COMPLETED ?? 0} />
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2">
        <input
          className="rounded-xl border border-[var(--line)] px-3 py-2"
          placeholder="Keresés név / telefon"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select className="rounded-xl border border-[var(--line)] px-3 py-2" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">Minden státusz</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
        <input
          className="w-28 rounded-xl border border-[var(--line)] px-3 py-2"
          placeholder="Év"
          value={year}
          onChange={(e) => setYear(e.target.value)}
        />
        <button type="button" className="rounded-full bg-[var(--ink)] px-4 py-2 text-white" onClick={() => load()}>
          Szűrés
        </button>
      </div>

      {error ? <p className="text-red-700">{error}</p> : null}

      <div className="overflow-x-auto rounded-2xl border border-[var(--line)] bg-white">
        <table className="min-w-full text-left text-sm">
          <thead className="bg-[#f3ebe0]">
            <tr>
              <th className="px-3 py-2">Azonosító</th>
              <th className="px-3 py-2">Név</th>
              <th className="px-3 py-2">Telefon</th>
              <th className="px-3 py-2">Év</th>
              <th className="px-3 py-2">Beküldve</th>
              <th className="px-3 py-2">Módosítva</th>
              <th className="px-3 py-2">Státusz</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((o) => (
              <tr key={o.id} className="border-t border-[var(--line)] hover:bg-[#fff8f0]">
                <td className="px-3 py-2">
                  <Link className="text-[var(--accent-deep)] underline" to={`/admin/orders/${o.id}`}>
                    {o.publicOrderId}
                  </Link>
                </td>
                <td className="px-3 py-2">{o.fullName}</td>
                <td className="px-3 py-2">{o.phone}</td>
                <td className="px-3 py-2">{o.calendarYear}</td>
                <td className="px-3 py-2">{new Date(o.createdAt).toLocaleString()}</td>
                <td className="px-3 py-2">{new Date(o.updatedAt).toLocaleString()}</td>
                <td className="px-3 py-2">{o.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </main>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-[var(--line)] bg-[var(--card)] p-4">
      <div className="text-sm text-[var(--muted)]">{label}</div>
      <div className="text-2xl font-semibold">{value}</div>
    </div>
  );
}
