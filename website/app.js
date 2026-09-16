// TrueTime Interactive Showcase Engine

document.addEventListener('DOMContentLoaded', () => {
  initClockWidget();
  initBenchmarkSimulator();
});

// 1. Live Dual Clock & Offset Widget
function initClockWidget() {
  const localTimeEl = document.getElementById('live-local-time');
  const utcTimeEl = document.getElementById('live-utc-time');
  const offsetEl = document.getElementById('live-offset');
  const jitterEl = document.getElementById('live-jitter');
  const driftEl = document.getElementById('live-drift');

  let simulatedOffset = 0.38;
  let simulatedJitter = 0.24;
  let simulatedPpm = 1.4;

  function tick() {
    const now = new Date();
    
    // Format Local Time (hh:mm:ss.fff AM/PM)
    const hours = now.getHours();
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');
    const ms = String(now.getMilliseconds()).padStart(3, '0');
    const ampm = hours >= 12 ? 'PM' : 'AM';
    const displayHours = String(hours % 12 || 12).padStart(2, '0');
    
    localTimeEl.textContent = `${displayHours}:${minutes}:${seconds}.${ms.slice(0, 2)} ${ampm}`;

    // Format UTC Time
    const utcHours = String(now.getUTCHours()).padStart(2, '0');
    const utcMinutes = String(now.getUTCMinutes()).padStart(2, '0');
    const utcSeconds = String(now.getUTCSeconds()).padStart(2, '0');
    utcTimeEl.textContent = `${utcHours}:${utcMinutes}:${utcSeconds}.${ms.slice(0, 2)} UTC`;

    // Subtle micro-fluctuations in live statistics
    simulatedOffset = +(simulatedOffset + (Math.random() * 0.04 - 0.02)).toFixed(2);
    simulatedJitter = +(simulatedJitter + (Math.random() * 0.02 - 0.01)).toFixed(2);
    
    if (offsetEl) offsetEl.textContent = `${simulatedOffset >= 0 ? '+' : ''}${simulatedOffset} ms`;
    if (jitterEl) jitterEl.textContent = `${simulatedJitter} ms`;
    if (driftEl) driftEl.textContent = `+${simulatedPpm} PPM`;

    requestAnimationFrame(tick);
  }

  requestAnimationFrame(tick);
}

// 2. Interactive NTP Benchmark Simulator
const ntpServers = [
  { host: 'time.cloudflare.com', org: 'Cloudflare Anycast', stratum: 1, basePing: 8.4 },
  { host: 'time.google.com', org: 'Google Public NTP', stratum: 1, basePing: 12.1 },
  { host: 'time.apple.com', org: 'Apple Inc.', stratum: 1, basePing: 15.6 },
  { host: 'time.windows.com', org: 'Microsoft Corporation', stratum: 1, basePing: 22.8 },
  { host: 'pool.ntp.org', org: 'NTP Pool Project', stratum: 2, basePing: 18.2 },
  { host: 'time.nist.gov', org: 'NIST Atomic Clock', stratum: 1, basePing: 28.5 }
];

function initBenchmarkSimulator() {
  const container = document.getElementById('server-test-list');
  const runBtn = document.getElementById('btn-run-benchmark');
  const summaryEl = document.getElementById('benchmark-summary');

  if (!container) return;

  renderServers(container);

  if (runBtn) {
    runBtn.addEventListener('click', () => {
      runBenchmark(container, runBtn, summaryEl);
    });
  }
}

function renderServers(container) {
  container.innerHTML = '';
  ntpServers.forEach((s, idx) => {
    const row = document.createElement('div');
    row.className = 'server-test-row';
    row.id = `ntp-row-${idx}`;
    row.innerHTML = `
      <div class="server-info-col">
        <span class="badge badge-emerald">● Online</span>
        <div>
          <div class="server-host">${s.host}</div>
          <div style="font-size: 0.75rem; color: var(--text-muted);">${s.org} (Stratum ${s.stratum})</div>
        </div>
      </div>
      <div class="latency-bar-container">
        <div class="latency-bar-fill" id="bar-${idx}" style="width: ${(s.basePing / 40) * 100}%;"></div>
      </div>
      <div class="server-metrics-col">
        <span id="ping-${idx}" style="color: #60a5fa;">${s.basePing.toFixed(1)} ms</span>
        <span id="offset-${idx}" style="color: var(--text-muted); font-size: 0.8rem;">±0.4 ms</span>
      </div>
    `;
    container.appendChild(row);
  });
}

function runBenchmark(container, button, summary) {
  button.disabled = true;
  button.textContent = '⚡ Querying NTP Pools...';
  if (summary) summary.textContent = 'Concurrent SNTP queries active across 6 international stratums...';

  let completed = 0;
  let fastest = { ping: 9999, host: '' };

  ntpServers.forEach((s, idx) => {
    const bar = document.getElementById(`bar-${idx}`);
    const pingEl = document.getElementById(`ping-${idx}`);
    const offsetEl = document.getElementById(`offset-${idx}`);

    if (bar) bar.style.width = '10%';

    // Simulate concurrent UDP round trip times
    const delay = 400 + Math.random() * 800;
    setTimeout(() => {
      const ping = +(s.basePing + (Math.random() * 4 - 2)).toFixed(1);
      const offset = +((Math.random() * 1.2 - 0.6)).toFixed(2);

      if (bar) bar.style.width = `${Math.min(100, (ping / 40) * 100)}%`;
      if (pingEl) pingEl.textContent = `${ping} ms`;
      if (offsetEl) offsetEl.textContent = `${offset >= 0 ? '+' : ''}${offset} ms`;

      if (ping < fastest.ping) {
        fastest = { ping, host: s.host };
      }

      completed++;
      if (completed === ntpServers.length) {
        button.disabled = false;
        button.textContent = '🔄 Re-test Benchmark';
        if (summary) {
          summary.innerHTML = `✓ Benchmark complete. Fastest authoritative source: <strong style="color:#34d399;">${fastest.host}</strong> (${fastest.ping} ms). Estimated statistical pool jitter: <strong style="color:#60a5fa;">0.31 ms</strong>.`;
        }
      }
    }, delay);
  });
}

// 3. Copy to Clipboard Utility
function copyText(text, message) {
  navigator.clipboard.writeText(text).then(() => {
    showToast(message || 'Copied to clipboard!');
  }).catch(() => {
    const input = document.createElement('textarea');
    input.value = text;
    document.body.appendChild(input);
    input.select();
    document.execCommand('copy');
    document.body.removeChild(input);
    showToast(message || 'Copied to clipboard!');
  });
}

function showToast(message) {
  let toast = document.getElementById('toast');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'toast';
    toast.className = 'toast';
    document.body.appendChild(toast);
  }
  toast.textContent = `✓ ${message}`;
  toast.style.display = 'block';
  setTimeout(() => {
    toast.style.display = 'none';
  }, 2500);
}
