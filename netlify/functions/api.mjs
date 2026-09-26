import { getStore } from "@netlify/blobs";

const DEFAULT_GOAL = {
  schema: "nexwin-community-goal-v3",
  version: "1.0.87",
  goalId: "nexwin-community-goal-v3",
  titleRo: "Susține Proiectul",
  titleEn: "Support the Project",
  subtitleRo: "Susține dezvoltarea NexWin",
  subtitleEn: "Support NexWin development",
  descriptionRo: "Dacă aplicația îți este utilă, poți contribui cu orice sumă dorești pentru a susține dezvoltarea continuă și poți lăsa un mesaj.",
  descriptionEn: "If the app helps you, you can contribute any amount you wish to support ongoing development and leave a message.",
  currentAmount: 0.0,
  targetAmount: 100.0,
  currency: "EUR",
  apiEndpoint: "https://nexwin-164.netlify.app/.netlify/functions/api",
  donateUrl: "https://ko-fi.com/luci3alin",
  revolutUrl: "https://revolut.me/luci3alin",
  paypalUrl: "https://paypal.me/luci3alin",
  discordUrl: "https://discord.com/users/luci3alin",
  supporters: []
};

async function recordClientHeartbeat(req, store) {
  try {
    const installId = (req.headers.get("x-install-id") || "").trim();
    if (!installId) return;

    const appVer = (req.headers.get("x-app-version") || "1.0.85").trim();
    const appLang = (req.headers.get("x-app-lang") || "ro").trim().toLowerCase();
    const nowIso = new Date().toISOString();

    const telemetry = (await store.get("telemetry_v1", { type: "json" })) || { clients: {} };
    if (!telemetry.clients) telemetry.clients = {};

    const existing = telemetry.clients[installId];
    telemetry.clients[installId] = {
      firstSeen: existing?.firstSeen || nowIso,
      lastSeen: nowIso,
      version: appVer,
      lang: appLang,
      launches: (existing?.launches || 0) + 1
    };

    await store.setJSON("telemetry_v1", telemetry);
  } catch {
    // Non-blocking telemetry
  }
}

export default async (req, context) => {
  const url = new URL(req.url);
  const path = url.pathname;
  const store = getStore("nexwin-community-store");

  // Handle CORS preflight
  if (req.method === "OPTIONS") {
    return new Response(null, {
      status: 204,
      headers: {
        "Access-Control-Allow-Origin": "*",
        "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type, X-Install-Id, X-App-Version, X-App-Lang, X-Webhook-Secret"
      }
    });
  }

  // Record anonymous install & active session heartbeat whenever the desktop app connects
  await recordClientHeartbeat(req, store);

  let goalState = await store.get("goal_v3", { type: "json" });
  if (!goalState || goalState.schema !== "nexwin-community-goal-v3") {
    goalState = { ...DEFAULT_GOAL };
  }

  // 0. GET /stats -> Live Analytics & Telemetry for the Admin Dashboard
  if (req.method === "GET" && path.endsWith("/stats")) {
    const telemetry = (await store.get("telemetry_v1", { type: "json" })) || { clients: {} };
    const clientsObj = telemetry.clients || {};
    const entries = Object.entries(clientsObj);
    const nowMs = Date.now();

    let onlineNow = 0;
    let activeToday = 0;
    const versions = {};
    const languages = { ro: 0, en: 0 };
    const recentClients = [];

    for (const [id, info] of entries) {
      const lastMs = Date.parse(info.lastSeen || "") || 0;
      const diffMinutes = (nowMs - lastMs) / 60000;
      const isOnline = diffMinutes <= 15;
      if (isOnline) onlineNow++;
      if (diffMinutes <= 1440) activeToday++;

      const v = info.version || "1.0.85";
      versions[v] = (versions[v] || 0) + 1;

      const l = info.lang === "en" ? "en" : "ro";
      languages[l] = (languages[l] || 0) + 1;

      recentClients.push({
        id: id.slice(0, 8) + "...",
        version: v,
        lang: l.toUpperCase(),
        firstSeen: info.firstSeen,
        lastSeen: info.lastSeen,
        isOnline
      });
    }

    recentClients.sort((a, b) => (Date.parse(b.lastSeen) || 0) - (Date.parse(a.lastSeen) || 0));

    return new Response(
      JSON.stringify({
        totalInstalls: entries.length,
        onlineNow,
        activeToday,
        versions,
        languages,
        recentClients: recentClients.slice(0, 25),
        goal: {
          currentAmount: goalState.currentAmount,
          targetAmount: goalState.targetAmount,
          currency: goalState.currency,
          supportersCount: (goalState.supporters || []).length,
          supporters: (goalState.supporters || []).slice(0, 10)
        },
        serverTime: new Date().toISOString()
      }),
      {
        status: 200,
        headers: {
          "Content-Type": "application/json; charset=utf-8",
          "Access-Control-Allow-Origin": "*",
          "Cache-Control": "no-cache, no-store, must-revalidate"
        }
      }
    );
  }

  // 1. GET /goal -> Read current Community Support Goal
  if (req.method === "GET" && (path.endsWith("/goal") || path.endsWith("/api"))) {
    return new Response(JSON.stringify(goalState), {
      status: 200,
      headers: {
        "Content-Type": "application/json; charset=utf-8",
        "Access-Control-Allow-Origin": "*",
        "Cache-Control": "no-cache, no-store, must-revalidate"
      }
    });
  }

  // 1b. POST /reset -> Clear test webhook entries and reset goal to initial state
  if (req.method === "POST" && path.endsWith("/reset")) {
    goalState.currentAmount = 0;
    goalState.supporters = [];
    await store.setJSON("goal_v3", goalState);
    await store.setJSON("pending_drafts", []);
    return new Response(JSON.stringify({ ok: true, status: "reset_complete", goal: goalState }), {
      status: 200,
      headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
    });
  }

  // 2. POST /pending-donation -> Save donor's draft (name, amount, message) BEFORE payment confirmation
  if (req.method === "POST" && path.endsWith("/pending-donation")) {
    try {
      const body = await req.json();
      const pendingList = (await store.get("pending_drafts", { type: "json" })) || [];
      pendingList.unshift({
        name: String(body.name || "Anonim").slice(0, 24),
        amount: Math.max(1, Number(body.amount) || 5),
        message: String(body.message || "").slice(0, 120),
        machineId: String(body.machineId || ""),
        savedAt: new Date().toISOString()
      });
      await store.setJSON("pending_drafts", pendingList.slice(0, 50));
      return new Response(JSON.stringify({ ok: true, status: "pending_saved" }), {
        status: 200,
        headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
      });
    } catch {
      return new Response(JSON.stringify({ ok: false }), { status: 400 });
    }
  }

  // 3. POST /webhook -> Verified Payment Webhook (Ko-fi / Stripe / PayPal) increments goal & adds supporter
  if (req.method === "POST" && (path.endsWith("/webhook") || path.endsWith("/donate"))) {
    try {
      const contentType = req.headers.get("content-type") || "";
      let payload = {};
      if (contentType.includes("application/x-www-form-urlencoded")) {
        const formData = await req.formData();
        const rawData = formData.get("data");
        if (rawData) payload = JSON.parse(rawData);
      } else {
        payload = await req.json();
      }

      const expectedSecret = process.env.WEBHOOK_SECRET;
      const providedToken = payload.verification_token || req.headers.get("x-webhook-secret");
      if (expectedSecret && providedToken !== expectedSecret) {
        return new Response(JSON.stringify({ error: "Unauthorized webhook token" }), { status: 401 });
      }

      const amountNum = Math.max(1, Math.round((Number(payload.amount) || 5) * 100) / 100);
      let donorName = String(payload.from_name || payload.name || "").trim();
      let donorMsg = String(payload.message || "").trim();

      const pendingList = (await store.get("pending_drafts", { type: "json" })) || [];
      const matchedDraft = pendingList.find(d => Math.abs(Number(d.amount) - amountNum) < 0.5);
      if (!donorName && matchedDraft) donorName = matchedDraft.name;
      if (!donorMsg && matchedDraft) donorMsg = matchedDraft.message;
      if (!donorName) donorName = "Anonim";

      goalState.currentAmount = Math.round((Number(goalState.currentAmount || 0) + amountNum) * 100) / 100;
      goalState.supporters = goalState.supporters || [];
      goalState.supporters.unshift({
        name: donorName.slice(0, 24),
        amount: `${amountNum} EUR`,
        badge: amountNum >= 25 ? "Sponsor" : "Supporter",
        message: donorMsg.slice(0, 120)
      });

      await store.setJSON("goal_v3", goalState);

      return new Response(JSON.stringify(goalState), {
        status: 200,
        headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
      });
    } catch (err) {
      return new Response(JSON.stringify({ error: String(err) }), { status: 400 });
    }
  }

  return new Response(JSON.stringify(goalState), {
    status: 200,
    headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" }
  });
};
