# Ghid Rapid: Lansare NexWin v1.0.85 pe Netlify Free + GitHub Releases (Fără VPS)

Acest folder (`netlify/`) conține toată infrastructura cloud pentru **NexWin v1.0.85**, proiectată special să funcționeze **100% gratuit pe Netlify Free**, ocolind toate limitările de trafic și stocare:

1. **Trafic 0 GB pentru kitul de instalare**:
   - Fișierul `NexWin-Setup-v1.0.85-native.exe` se încarcă pe **GitHub Releases** (unde traficul de download este nelimitat și gratuit).
   - Netlify servește doar `version.json`, `community_goal.json` și funcția de donații (câțiva KB).
2. **Bază de date inclusă gratuit (`@netlify/blobs`)**:
   - Funcția `netlify/functions/api.mjs` salvează suma strânsă (`currentAmount`) și lista de susținători (`supporters`) în **Netlify Blobs** — nu se șterge nimic la restart.
3. **Cache CDN inteligent (`Cache-Control: s-maxage=300`)**:
   - Aplicațiile NexWin deschise citesc statusul obiectivului direct din cache-ul CDN Netlify timp de 5 minute, consumând sub 1% din limita lunară gratuită de 125.000 apeluri.

---

## Cum îl publici în 2 minute pe Netlify Free:
1. Intră pe [app.netlify.com](https://app.netlify.com/) și autentifică-te cu contul tău de GitHub.
2. Apasă **Add new site** $\rightarrow$ **Import an existing project** $\rightarrow$ alege repository-ul `NexWin`.
3. La **Base directory**, scrie: `netlify` (Netlify va detecta automat `netlify.toml`, `public` și `functions`).
4. Setează numele site-ului (Site name) în `nexwin` (ca URL-ul să devină `https://nexwin.netlify.app`).
5. *(Opțional)* Pentru ca donațiile de pe Ko-fi să se adauge singure pe bară fără să editezi nimic:
   - În Ko-fi $\rightarrow$ **Settings** $\rightarrow$ **API / Webhooks**, pune Webhook URL:
     `https://nexwin.netlify.app/.netlify/functions/api/webhook`
