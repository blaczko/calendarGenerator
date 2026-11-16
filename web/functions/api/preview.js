/**
 * GET /api/preview?orderId=…&token=…
 * Proxies preview_strip.jpg through Cloudflare so the browser never hits
 * Drive / Apps Script iframe URLs (broken on desktop multi-login Chrome).
 */

function json(data, status) {
  return new Response(JSON.stringify(data), {
    status: status || 200,
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'no-store'
    }
  });
}

function base64ToBytes(b64) {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

export async function onRequestGet(context) {
  const gasUrl = context.env.GAS_EXEC_URL;
  if (!gasUrl) {
    return json({ ok: false, error: 'GAS_EXEC_URL nincs beállítva.' }, 500);
  }

  const url = new URL(context.request.url);
  const orderId = (url.searchParams.get('orderId') || '').trim();
  const token = (url.searchParams.get('token') || '').trim();
  if (!orderId && !token) {
    return json({ ok: false, error: 'Hiányzik a orderId vagy token.' }, 400);
  }

  const action = token ? 'getPreviewImageByToken' : 'getPreviewImageByOrderId';
  const args = token ? [token] : [orderId];

  try {
    const res = await fetch(gasUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      redirect: 'follow',
      body: JSON.stringify({ action: action, args: args })
    });
    const text = await res.text();
    let data;
    try {
      data = JSON.parse(text);
    } catch (e) {
      return json({
        ok: false,
        error: 'Apps Script nem JSON-t adott (előnézet).',
        preview: text.slice(0, 200)
      }, 502);
    }
    if (!data.ok || !data.result || !data.result.base64) {
      return json({
        ok: false,
        error: (data && data.error) || 'Nincs előnézet.'
      }, 404);
    }

    const bytes = base64ToBytes(data.result.base64);
    return new Response(bytes, {
      status: 200,
      headers: {
        'Content-Type': data.result.mimeType || 'image/jpeg',
        'Cache-Control': 'private, max-age=120',
        'Content-Disposition': 'inline; filename="' + (data.result.filename || 'preview.jpg') + '"'
      }
    });
  } catch (err) {
    return json({ ok: false, error: String(err && err.message || err) }, 502);
  }
}
