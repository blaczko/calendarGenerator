export type OrderEvent = { id?: string; eventDate: string; description: string };
export type OrderPhoto = {
  id: string;
  month: number;
  sortOrder: number;
  originalFilename: string;
  mimeType: string;
  sizeBytes: number;
};

export type Order = {
  id: string;
  publicOrderId: string;
  fullName: string;
  phone: string;
  email: string | null;
  calendarYear: number;
  calendarLanguage: string;
  calendarFormatPref: string | null;
  comments: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
  photos: OrderPhoto[];
  events: OrderEvent[];
  editTokenRevoked?: boolean;
};

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(url, {
      credentials: "include",
      ...init,
      headers: {
        ...(init?.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
        ...init?.headers,
      },
    });
  } catch {
    throw new Error(
      "Nem elérhető a szerver (API). Indítsa újra: npm run dev a calendar-order-platform mappában."
    );
  }
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    const details = data.details
      ? data.details.map((d: { message: string }) => d.message).join(" ")
      : "";
    throw new Error(data.error ? `${data.error}${details ? ": " + details : ""}` : "Kérés sikertelen");
  }
  return data as T;
}

export const client = {
  config: () =>
    api<{
      supportedYears: number[];
      maxPhotosPerMonth: number;
      maxFileSizeMb: number;
      maxEventsPerOrder: number;
    }>("/api/config"),
  createOrder: (body: unknown) =>
    api<{ orderId: string; editToken: string; editUrl: string; order: Order }>(
      "/api/orders",
      { method: "POST", body: JSON.stringify(body) }
    ),
  getOrderByToken: (token: string) =>
    api<{ order: Order }>(`/api/orders/edit/${token}`),
  updateOrder: (token: string, body: unknown) =>
    api<{ order: Order }>(`/api/orders/edit/${token}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  uploadPhoto: async (token: string, month: number, file: File) => {
    const fd = new FormData();
    fd.append("photo", file);
    return api<{ order: Order }>(`/api/orders/edit/${token}/photos/${month}`, {
      method: "POST",
      body: fd,
    });
  },
  deletePhoto: (token: string, photoId: string) =>
    api<{ order: Order }>(`/api/orders/edit/${token}/photos/${photoId}`, {
      method: "DELETE",
    }),
  reorderPhotos: (token: string, month: number, photoIds: string[]) =>
    api<{ order: Order }>(`/api/orders/edit/${token}/photos/${month}/reorder`, {
      method: "PUT",
      body: JSON.stringify({ photoIds }),
    }),
  photoUrl: (token: string, photoId: string) =>
    `/api/orders/edit/${token}/photos/file/${photoId}`,
  adminLogin: (username: string, password: string) =>
    api<{ ok: boolean }>("/api/admin/login", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    }),
  adminLogout: () => api<{ ok: boolean }>("/api/admin/logout", { method: "POST" }),
  adminMe: () => api<{ username: string }>("/api/admin/me"),
  adminStats: () =>
    api<{ total: number; counts: Record<string, number> }>("/api/admin/stats"),
  adminOrders: (q: URLSearchParams) =>
    api<{ orders: Order[] }>(`/api/admin/orders?${q.toString()}`),
  adminOrder: (id: string) => api<{ order: Order }>(`/api/admin/orders/${id}`),
  adminSetStatus: (id: string, status: string) =>
    api<{ order: Order }>(`/api/admin/orders/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ status }),
    }),
  adminDelete: (id: string) =>
    api<{ ok: boolean }>(`/api/admin/orders/${id}`, { method: "DELETE" }),
  adminRevoke: (id: string) =>
    api<{ order: Order }>(`/api/admin/orders/${id}/revoke-edit-link`, {
      method: "POST",
    }),
  adminPhotoUrl: (orderId: string, photoId: string) =>
    `/api/admin/orders/${orderId}/photos/${photoId}`,
};
