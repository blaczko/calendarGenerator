import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { client } from "../../api/client";

export default function AdminLogin() {
  const navigate = useNavigate();
  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await client.adminLogin(username, password);
      navigate("/admin/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Bejelentkezési hiba");
    }
  }

  return (
    <main className="mx-auto flex min-h-[70vh] max-w-md items-center px-4">
      <form onSubmit={onSubmit} className="w-full space-y-4 rounded-3xl border border-[var(--line)] bg-[var(--card)] p-8 shadow-lg">
        <h1 className="text-2xl">Admin belépés</h1>
        <input className="w-full rounded-xl border border-[var(--line)] px-3 py-2" value={username} onChange={(e) => setUsername(e.target.value)} placeholder="Felhasználónév" />
        <input className="w-full rounded-xl border border-[var(--line)] px-3 py-2" type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Jelszó" />
        {error ? <p className="text-sm text-red-700">{error}</p> : null}
        <button type="submit" className="w-full rounded-full bg-[var(--ink)] py-2.5 text-white">
          Belépés
        </button>
      </form>
    </main>
  );
}
