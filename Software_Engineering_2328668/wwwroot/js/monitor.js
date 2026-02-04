(() => {
    const root = document.getElementById('monitor-root');
    if (!root) return;

    // --- DOM refs ---
    const dateSelect = document.getElementById('dateSelect');
    const durSelect = document.getElementById('durSelect');

    // ORIGINAL (kept): separate Play and Pause buttons
    const btnPlay = document.getElementById('btnPlay');
    const btnPause = document.getElementById('btnPause');

    // NEW: single toggle ▶ / ⏸ + time label + download + spinner (works only if present in the HTML)
    const btnToggle = document.getElementById('btnToggle');    // single toggle play/pause
    const lblTime = document.getElementById('lblTime');        // 00:00 / 00:00
    const btnDownload = document.getElementById('btnDownload'); // Download PNG
    const spinner = document.getElementById('spinner');        // legacy: small spinner
    const loadingOv = document.getElementById('loading');      // UPDATED: overlay spinner container (preferred)

    const slider = document.getElementById('timeSlider');
    const canvas = document.getElementById('heatmap');
    const legendCv = document.getElementById('legend');

    // old bottom stats if they still exist
    const statPeak = document.getElementById('statPeak');
    const statAvg = document.getElementById('statAvg');
    const statContact = document.getElementById('statContact');
    const statCov = document.getElementById('statCov');
    const statPpi = document.getElementById('statPpi');

    // UPDATED: New DOM stats (right of legend)
    const statPeakVal = document.getElementById('statPeakVal');
    const statAvgVal = document.getElementById('statAvgVal');
    const statContactVal = document.getElementById('statContactVal');
    const statCovVal = document.getElementById('statCovVal');
    const statPpiVal = document.getElementById('statPpiVal');

    // --- State ---
    const pid = parseInt(root.dataset.pid, 10);
    let currentDate = root.dataset.defaultDate || dateSelect.value;
    let durationSec = parseInt(root.dataset.defaultDuration || "300", 10); // default 5m

    // Query params support (?date=...&duration=...&seek=...)
    const url = new URL(window.location.href);
    if (url.searchParams.get('date')) currentDate = url.searchParams.get('date');
    if (url.searchParams.get('duration')) durationSec = parseInt(url.searchParams.get('duration'), 10);

    // Dataset meta
    let meta = null; // {datasetId, fps, frames, durationSec, width, height, ...}
    // Playback
    let playing = false;
    let rafId = 0;

    // Window (subset of frames determined by durationSec)
    let windowLen = 0;        // frames in the window
    let baseOffset = 0;       // absolute frame index of window start
    let cursor = 0;           // index within window [0..windowLen-1]
    let seekAbsolute = url.searchParams.get('seek') ? parseInt(url.searchParams.get('seek'), 10) : null;

    // Chunk cache: we fetch frames/metrics in CHUNK-sized blocks
    const CHUNK = 150;
    let chunk = { offset: -1, count: 0, frames: [], metrics: [] }; // frames: int[frame][1024]

    // UPDATED: generation token to cancel/ignore stale async loads
    let loadToken = 0;


    // Canvas contexts
    const ctx = canvas.getContext('2d');
    const lg = legendCv.getContext('2d');

    // Color mapping (VIBGYOR 5->850, white <5)
    // ----------------------------------------------------------------
    // UPDATED: Switch to B-G-Y-O-R (blue→green→yellow→orange→red), white for < 5 AU.
    // (Keeping the original comment line above, as requested.)
    const minAU = 5, maxAU = 850;
    const stops = [
        { v: minAU, color: [0, 0, 255] },     // B  #0000FF
        { v: 360, color: [0, 255, 0] },     // G  #00FF00
        { v: 500, color: [255, 255, 0] },   // Y  #FFFF00
        { v: 675, color: [255, 127, 0] },   // O  #FF7F00
        { v: maxAU, color: [255, 0, 0] }      // R  #FF0000
    ];

    function lerp(a, b, t) { return a + (b - a) * t; }
    function lerpRGB(c1, c2, t) { return [Math.round(lerp(c1[0], c2[0], t)), Math.round(lerp(c1[1], c2[1], t)), Math.round(lerp(c1[2], c2[2], t))]; }
    function auToColor(v) {
        if (v < minAU) return [255, 255, 255]; // white
        if (v >= maxAU) return [255, 0, 0];    // clamp to red
        // find surrounding stops
        for (let i = 0; i < stops.length - 1; i++) {
            const a = stops[i], b = stops[i + 1];
            if (v >= a.v && v <= b.v) {
                const t = (v - a.v) / (b.v - a.v);
                return lerpRGB(a.color, b.color, t);
            }
        }
        return [255, 255, 255];
    }

    // Draw legend vertical gradient
    // ----------------------------------------------------------------
    // UPDATED: use inner padding so labels never clip (top/bottom/right),
    // draw gradient bar on the left, ticks+labels on the right inside the canvas.
    function drawLegend() {
        const w = legendCv.width, h = legendCv.height;
        lg.clearRect(0, 0, w, h);

        const padL = 8, padR = 36, padT = 8, padB = 8;
        const barW = 32;
        const barX = padL;
        const barY = padT;
        const barH = h - padT - padB;

        // gradient (low at bottom, high at top)
        const img = lg.createImageData(barW, barH);
        for (let y = 0; y < barH; y++) {
            const frac = 1 - (y / (barH - 1));
            const val = minAU + frac * (maxAU - minAU);
            const col = auToColor(val);
            for (let x = 0; x < barW; x++) {
                const idx = (y * barW + x) * 4;
                img.data[idx + 0] = col[0];
                img.data[idx + 1] = col[1];
                img.data[idx + 2] = col[2];
                img.data[idx + 3] = 255;
            }
        }
        lg.putImageData(img, barX, barY);

        // ticks + labels on the right
        lg.strokeStyle = "#2d3644";
        lg.fillStyle = "#2d3644";
        lg.font = "12px system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif";
        lg.textAlign = "left";
        lg.textBaseline = "middle";

        const tickX = barX + barW; // at the bar’s right edge
        const ticks = [5, 100, 250, 400, 600, 850];
        ticks.forEach(v => {
            const frac = (v - minAU) / (maxAU - minAU);
            const y = barY + (1 - frac) * (barH - 1);
            // tick line
            lg.beginPath();
            lg.moveTo(tickX, y);
            lg.lineTo(tickX + 6, y);
            lg.stroke();
            // label
            lg.fillText(v.toString(), tickX + 10, y);
        });

        // AU label bottom-right
        lg.textAlign = "right";
        lg.fillText("AU", w - 4, h - 10);
    }

    // Render one 32x32 frame to 512x512 canvas
    function drawFrame(frame1024, w, h) {
        // Create image data at canvas size; nearest-neighbour scale
        const CW = canvas.width, CH = canvas.height;
        const img = ctx.createImageData(CW, CH);

        const scaleX = CW / w, scaleY = CH / h;
        for (let cy = 0; cy < CH; cy++) {
            const sy = Math.floor(cy / scaleY);
            for (let cx = 0; cx < CW; cx++) {
                const sx = Math.floor(cx / scaleX);
                const v = frame1024[sy * w + sx];
                const col = auToColor(v);
                const idx = (cy * CW + cx) * 4;
                img.data[idx + 0] = col[0];
                img.data[idx + 1] = col[1];
                img.data[idx + 2] = col[2];
                img.data[idx + 3] = 255;
            }
        }
        ctx.putImageData(img, 0, 0);
    }

    // NEW: draw stats overlay INSIDE the canvas (right-side panel)
    // ----------------------------------------------------------------
    // REMOVED per new requirements. Stats now live in DOM boxes to the right of the legend.
    // (Keeping this section header to preserve your original comment.)

    // Fetch helpers
    async function getJSON(url) { const r = await fetch(url, { cache: 'no-store' }); if (!r.ok) throw new Error(await r.text()); return await r.json(); }

    // NEW: small helper to show/hide spinner if present
    function showSpinner(on) {
        // Prefer the overlay; fall back to the small spinner if overlay is not present.
        if (loadingOv) {
            loadingOv.style.display = on ? "flex" : "none";
            return;
        }
        if (spinner) {
            spinner.style.display = on ? "block" : "none";
        }
    }

    async function loadMeta() {
        // UPDATED: start a new load generation (cancels stale work)
        const myToken = ++loadToken;

        // stop playback, clear RAF
        playing = false;
        cancelAnimationFrame(rafId);
        rafId = 0;

        // show spinner immediately and reset caches/UI
        showSpinner(true);
        chunk = { offset: -1, count: 0, frames: [], metrics: [] };

        // clear the canvas so old frames aren't visible
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // reset stat values while loading
        if (typeof statPeakVal !== 'undefined' && statPeakVal) statPeakVal.textContent = '—';
        if (typeof statAvgVal !== 'undefined' && statAvgVal) statAvgVal.textContent = '—';
        if (typeof statContactVal !== 'undefined' && statContactVal) statContactVal.textContent = '—';
        if (typeof statCovVal !== 'undefined' && statCovVal) statCovVal.textContent = '—';
        if (typeof statPpiVal !== 'undefined' && statPpiVal) statPpiVal.textContent = '—';

        // fetch meta for the selected date
        const u = new URL(`/api/monitor/meta`, window.location.origin);
        u.searchParams.set('pid', pid);
        u.searchParams.set('date', currentDate);
        const metaResp = await getJSON(u.toString());

        // if another load started meanwhile, abort
        if (myToken !== loadToken) return;

        meta = metaResp;

        // compute windowLen (frames)
        windowLen = Math.max(1, Math.round(durationSec * Number(meta.fps)));
        if (windowLen > meta.frames) windowLen = meta.frames;

        // center on seek if provided
        if (seekAbsolute !== null) {
            baseOffset = Math.max(0, Math.min(seekAbsolute - Math.floor(windowLen / 2), meta.frames - windowLen));
            cursor = seekAbsolute - baseOffset;
        } else {
            baseOffset = 0;
            cursor = 0;
        }

        slider.min = 0;
        slider.max = Math.max(0, windowLen - 1);
        slider.value = cursor;

        drawLegend();

        // preload first chunk (guard with token)
        await ensureChunkFor(baseOffset + cursor, myToken);
        if (myToken !== loadToken) return;

        renderCurrent();
        if (lblTime) updateClock();

        // hide spinner when first frame for the new date is ready
        showSpinner(false);
    }


    function chunkContains(absIdx) {
        return chunk.offset >= 0 && absIdx >= chunk.offset && absIdx < (chunk.offset + chunk.count);
    }

    async function ensureChunkFor(absIdx, tokenOverride) {
        if (chunkContains(absIdx)) return;

        // use current token if not provided
        const myToken = tokenOverride ?? loadToken;

        showSpinner(true);

        const newOffset = Math.max(0, Math.min(absIdx, meta.frames - CHUNK));
        const newCount = Math.min(CHUNK, meta.frames - newOffset);

        try {
            // frames
            {
                const u = new URL(`/api/monitor/frames`, window.location.origin);
                u.searchParams.set('datasetId', meta.datasetId);
                u.searchParams.set('offset', newOffset);
                u.searchParams.set('count', newCount);
                const json = await getJSON(u.toString());

                // drop results if a newer load started
                if (myToken !== loadToken) return;

                chunk.frames = json.frames;
            }
            // metrics
            {
                const u = new URL(`/api/monitor/metrics`, window.location.origin);
                u.searchParams.set('datasetId', meta.datasetId);
                u.searchParams.set('offset', newOffset);
                u.searchParams.set('count', newCount);
                const metrics = await getJSON(u.toString());

                if (myToken !== loadToken) return;

                chunk.metrics = metrics;
            }

            // commit only if still current
            if (myToken !== loadToken) return;

            chunk.offset = newOffset;
            chunk.count = newCount;
        } finally {
            // If this was a normal chunk fetch during playback (no tokenOverride),
            // hide the spinner now. For the initial load (with tokenOverride),
            // loadMeta() will hide it after the first render.
            if (tokenOverride == null) {
                showSpinner(false);
            }
        }
    }

    function updateClock() {
        if (!lblTime || !meta) return;
        const absIdx = baseOffset + cursor;
        const curS = absIdx / Number(meta.fps);
        const totalS = windowLen / Number(meta.fps);
        const fmt = (sec) => {
            sec = Math.max(0, Math.floor(sec));
            const m = Math.floor(sec / 60), s = sec % 60;
            return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
        };
        lblTime.textContent = `${fmt(curS)} / ${fmt(totalS)}`;
    }

    function renderCurrent() {
        const absIdx = baseOffset + cursor;
        if (!chunkContains(absIdx)) return; // will be drawn after ensureChunkFor

        const rel = absIdx - chunk.offset;
        const frame = chunk.frames[rel];
        drawFrame(frame, meta.width, meta.height);

        // metrics: find by i == absIdx
        const m = chunk.metrics.find(o => o.i === absIdx);

        // UPDATED: update DOM stat boxes (right of legend)
        if (statPeakVal) statPeakVal.textContent = (m?.peak ?? '—').toString();
        if (statAvgVal) statAvgVal.textContent = m?.avg != null ? (m.avg.toFixed ? m.avg.toFixed(1) : String(m.avg)) : '—';
        if (statContactVal) statContactVal.textContent = m?.contactPct != null ? m.contactPct.toFixed(1) + '%' : '—';
        if (statCovVal) statCovVal.textContent = m?.cov != null ? m.cov.toFixed(2) + '%' : '—';
        if (statPpiVal) statPpiVal.textContent = m?.ppi != null ? (m.ppi.toFixed ? m.ppi.toFixed(1) : String(m.ppi)) : '—';

        // LEGACY: keep old bottom stats updated if they still exist
        if (m) {
            statPeak && (statPeak.textContent = (m.peak ?? '—').toString());
            statAvg && (statAvg.textContent = (m.avg ?? '—').toFixed ? m.avg.toFixed(1) : m.avg);
            statContact && (statContact.textContent = (m.contactPct ?? '—').toFixed ? m.contactPct.toFixed(1) + '%' : m.contactPct + '%');
            statCov && (statCov.textContent = (m.cov ?? '—').toFixed ? m.cov.toFixed(2) + '%' : m.cov + '%');
            statPpi && (statPpi.textContent = (m.ppi ?? '—').toFixed ? m.ppi.toFixed(1) : m.ppi);
        } else {
            if (statPeak) statPeak.textContent = '—';
            if (statAvg) statAvg.textContent = '—';
            if (statContact) statContact.textContent = '—';
            if (statCov) statCov.textContent = '—';
            if (statPpi) statPpi.textContent = '—';
        }

        slider.value = cursor;
        updateClock();
        // Ensure spinner goes away once a frame is drawn
        showSpinner(false);

    }

    async function tick() {
        if (!playing) return;
        const absIdx = baseOffset + cursor;
        await ensureChunkFor(absIdx);
        renderCurrent();

        cursor++;
        if (cursor >= windowLen) {
            // stop at window end
            playing = false;
            if (btnToggle) btnToggle.textContent = "▶";
            return;
        }
        rafId = requestAnimationFrame(tickAtFps);
    }

    function tickAtFps() {
        // ~15 fps → ~66.7ms per frame; requestAnimationFrame ~60, so we just advance every frame (close enough)
        tick();
    }

    // --- Events ---
    dateSelect.addEventListener('change', async () => {
        currentDate = dateSelect.value;
        playing = false; cancelAnimationFrame(rafId);
        seekAbsolute = null; // reset seek when date changes
        await loadMeta();
        if (btnToggle) btnToggle.textContent = "▶";
    });

    durSelect.addEventListener('change', async () => {
        durationSec = parseInt(durSelect.value, 10);
        playing = false; cancelAnimationFrame(rafId);

        // recompute window with same absolute center if possible
        const abs = baseOffset + cursor;
        windowLen = Math.max(1, Math.round(durationSec * Number(meta.fps)));
        if (windowLen > meta.frames) windowLen = meta.frames;
        baseOffset = Math.max(0, Math.min(abs - Math.floor(windowLen / 2), meta.frames - windowLen));
        cursor = abs - baseOffset;

        slider.min = 0;
        slider.max = Math.max(0, windowLen - 1);
        slider.value = cursor;

        await ensureChunkFor(baseOffset + cursor)
        renderCurrent();
        if (btnToggle) btnToggle.textContent = "▶";
    });

    // ORIGINAL handlers (kept): if your old Play/Pause buttons exist, these still work.
    if (btnPlay) {
        btnPlay.addEventListener('click', async () => {
            if (!meta) return;
            playing = true;
            cancelAnimationFrame(rafId);
            rafId = requestAnimationFrame(tickAtFps);
            if (btnToggle) btnToggle.textContent = "⏸";
        });
    }
    if (btnPause) {
        btnPause.addEventListener('click', () => {
            playing = false;
            cancelAnimationFrame(rafId);
            if (btnToggle) btnToggle.textContent = "▶";
        });
    }

    // NEW: single toggle play/pause
    if (btnToggle) {
        btnToggle.addEventListener('click', async () => {
            if (!meta) return;
            if (playing) {
                playing = false;
                cancelAnimationFrame(rafId);
                btnToggle.textContent = "▶";
            } else {
                playing = true;
                cancelAnimationFrame(rafId);
                btnToggle.textContent = "⏸";
                rafId = requestAnimationFrame(tickAtFps);
            }
        });
    }

    slider.addEventListener('input', async () => {
        playing = false;
        cancelAnimationFrame(rafId);
        if (btnToggle) btnToggle.textContent = "▶";
        cursor = parseInt(slider.value, 10);
        await ensureChunkFor(baseOffset + cursor);
        renderCurrent();
    });

    function pad2(n) { return String(n).padStart(2, '0'); }
    function fmtStampUTC(d) {
        // YYYYMMDD_HHMMSS in UTC for stable filenames
        return `${d.getUTCFullYear()}${pad2(d.getUTCMonth() + 1)}${pad2(d.getUTCDate())}_` +
            `${pad2(d.getUTCHours())}${pad2(d.getUTCMinutes())}${pad2(d.getUTCSeconds())}`;
    }

    // NEW: Download PNG (heatmap + legend + DOM stat boxes)
    if (btnDownload) {
        btnDownload.addEventListener('click', () => {
            if (!meta) return;

            const pad = 12;
            const heatW = canvas.width;
            const heatH = canvas.height;
            const legendW = legendCv.width;
            const statsW = 180; // export width for stats column
            const panelW = heatW + pad + legendW + pad + statsW;
            const panelH = Math.max(heatH, legendCv.height);

            const off = document.createElement('canvas');
            off.width = panelW;
            off.height = panelH;
            const ox = off.getContext('2d');

            // white background
            ox.fillStyle = "#ffffff";
            ox.fillRect(0, 0, panelW, panelH);

            // draw heatmap
            ox.drawImage(canvas, 0, 0);

            // draw legend to the right
            ox.drawImage(legendCv, heatW + pad, 0);

            // draw stat boxes (programmatically, using current DOM values)
            const sx = heatW + pad + legendW + pad;
            const sPad = 10;
            const boxW = statsW;
            const boxH = 84; // height per box
            const gap = 8;
            const names = ["Peak", "Avg", "Contact", "CoV", "PPI (10s)"];
            const vals = [
                (statPeakVal?.textContent || "—"),
                (statAvgVal?.textContent || "—"),
                (statContactVal?.textContent || "—"),
                (statCovVal?.textContent || "—"),
                (statPpiVal?.textContent || "—")
            ];
            for (let i = 0; i < 5; i++) {
                const y = i * (boxH + gap);
                // rounded panel
                ox.fillStyle = "#f7f9fc";
                ox.strokeStyle = "#e5edf6";
                ox.lineWidth = 1;
                roundRect(ox, sx, y, boxW, boxH, 10); ox.fill(); ox.stroke();

                // name
                ox.fillStyle = "#4e5d74";
                ox.font = "12px system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif";
                ox.textAlign = "left";
                ox.textBaseline = "top";
                ox.fillText(names[i], sx + sPad, y + sPad);

                // value centered
                ox.fillStyle = "#0e1d2f";
                ox.font = "bold 20px system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif";
                ox.textAlign = "center";
                ox.textBaseline = "middle";
                ox.fillText(vals[i], sx + boxW / 2, y + boxH / 2 + 6);
            }

            // filename: Patient_PID_date_frame.png
            // filename with real timestamp from startUtc + (absIdx/fps)
            const absIdx = baseOffset + cursor;
            const start = meta?.startUtc ? new Date(meta.startUtc) : null;
            let stamp = `f${absIdx}`; // fallback
            if (start && isFinite(start.getTime())) {
                const ms = Math.round((absIdx / Number(meta.fps)) * 1000);
                const ts = new Date(start.getTime() + ms);
                stamp = fmtStampUTC(ts); // e.g., 20251011_150037
            }
            const fname = `Patient_${pid}_${stamp}.png`;

            const link = document.createElement('a');
            link.href = off.toDataURL('image/png');
            link.download = fname;
            link.click();

            function roundRect(ctx, x, y, w, h, r) {
                ctx.beginPath();
                ctx.moveTo(x + r, y);
                ctx.arcTo(x + w, y, x + w, y + h, r);
                ctx.arcTo(x + w, y + h, x, y + h, r);
                ctx.arcTo(x, y + h, x, y, r);
                ctx.arcTo(x, y, x + w, y, r);
                ctx.closePath();
            }
        });
    }

    // --- init ---
    (async () => {
        // Select currentDate in the dropdown if provided via query
        if (currentDate) {
            for (const opt of dateSelect.options) {
                opt.selected = (opt.value === currentDate);
            }
        }
        // Select duration if provided via query
        if (durationSec) {
            for (const opt of durSelect.options) {
                opt.selected = (parseInt(opt.value, 10) === durationSec);
            }
        }

        if (lblTime) lblTime.textContent = "00:00 / 00:00";
        await loadMeta();
    })();

})();
