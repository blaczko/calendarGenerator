/**
 * Cloudflare Pages Function: browser → this → Google Apps Script doPost.
 *
 * Set project env var:
 *   GAS_EXEC_URL = https://script.google.com/macros/s/DEPLOYMENT_ID/exec
 *
 * Dashboard: Workers & Pages → your project → Settings → Environment variables
 */

const JSON_HEADERS = {
  'Content-Type': 'application/json; charset=utf-8',
  'Cache-Control': 'no-store'
};

function json(data, status) {
  return new Response(JSON.stringify(data), {
    status: status || 200,
    headers: JSON_HEADERS
  });
}

export async function onRequestGet(context) {
  const gasUrl = context.env.GAS_EXEC_URL;
  if (!gasUrl) {
    return json({
      ok: false,
      error: 'GAS_EXEC_URL nincs beállítva a Cloudflare projektben.'
    }, 500);
  }

  try {
    const pingUrl = gasUrl.replace(/\?.*$/, '') + '?api=ping';
    const res = await fetch(pingUrl, { method: 'GET', redirect: 'follow' });
    const text = await res.text();
    let data;
    try {
      data = JSON.parse(text);
    } catch (e) {
      return json({
        ok: false,
        error: 'Apps Script nem JSON-t adott vissza. Ellenőrizze a deploymentet (Anyone access).',
        preview: text.slice(0, 200)
      }, 502);
    }
    return json({ ok: true, gas: data, gasUrlConfigured: true });
  } catch (err) {
    return json({ ok: false, error: String(err && err.message || err) }, 502);
  }
}

export async function onRequestPost(context) {
  const gasUrl = context.env.GAS_EXEC_URL;
  if (!gasUrl) {
    return json({
      ok: false,
      error: 'GAS_EXEC_URL nincs beállítva a Cloudflare projektben.'
    }, 500);
  }

  let incoming;
  try {
    incoming = await context.request.text();
    JSON.parse(incoming);
  } catch (e) {
    return json({ ok: false, error: 'Érvénytelen JSON törzs.' }, 400);
  }

  try {
    const res = await fetch(gasUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      redirect: 'follow',
      body: incoming
    });
    const text = await res.text();

    // Apps Script sometimes returns HTML login/error pages.
    try {
      JSON.parse(text);
    } catch (e) {
      return json({
        ok: false,
        error: 'Apps Script nem JSON választ adott (HTTP ' + res.status + ').',
        preview: text.slice(0, 240)
      }, 502);
    }

    return new Response(text, {
      status: res.ok ? 200 : res.status,
      headers: JSON_HEADERS
    });
  } catch (err) {
    return json({ ok: false, error: String(err && err.message || err) }, 502);
  }
}
