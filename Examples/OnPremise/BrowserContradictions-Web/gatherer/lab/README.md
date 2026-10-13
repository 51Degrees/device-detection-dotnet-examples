# Browser tests lab

Captures what real browsers answer to `browser-tests-v2.js` and classifies
each test by whether a Linux Chromium claiming Windows answers differently
from genuine Windows Chrome.

- `collect.py <port>`: serves the lab page and stores one JSON per run in `runs/`.
- Windows: launch Chrome or Edge at `http://localhost:<port>/?label=<label>`,
  headed (`--app=`, off-screen window) or `--headless=new --virtual-time-budget=15000 --dump-dom`.
- WSL: `node wsl-run.js <label> full|shell [userAgent]` with Playwright Chromium.
  Replace `WINDOWS_HOST_IP` with the host address from `ip route`, so that
  `localhost` stays a secure context. Without that the secure-context-only
  APIs (userAgentData, deviceMemory, EME, Web Share) are missing and look like
  platform differences.
- `analyse.py`: USE means stable per browser across repeat headed runs and a
  real differing value from the Linux build claiming Windows.
- `cloak-run.js <label> <windows|macos|auto> <lab|site>`: runs CloakBrowser
  (npm `cloakbrowser`, needs Node 20 or newer, binary in `~/.cloakbrowser`)
  with a persona against the lab or the proof site. The free tier allows
  one session at a time. The proof site must listen on all interfaces for
  WSL to reach it.
