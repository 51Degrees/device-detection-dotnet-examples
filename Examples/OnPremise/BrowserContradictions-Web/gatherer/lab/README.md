# Browser tests lab

Captures what real browsers answer to the gatherer, turns genuine captures
into baseline rows, and replays or drives browsers against the proof site.

- `collect.py <port> [gatherer]`: serves the lab page (gatherer v3 by
  default) and stores one JSON per run in `runs/`. `?label=` names the run
  and `?params=<json>` runs the gatherer with chosen inputs.
- Windows: launch Chrome or Edge at `http://localhost:<port>/?label=<label>`,
  headed (`--app=`, off-screen window, fresh profile), or Firefox with
  `-headless`.
- WSL: `node site-run.js <chromium|cloak> <label> <:port/path> <host ip> [ua or persona]`
  drives Playwright Chromium or CloakBrowser against the lab (`:5180/`) or
  the proof site (`:5151/` on-prem, `:5151/cloud` through the private
  cloud). `localhost` is mapped to the host with `--host-resolver-rules` so
  the page stays a secure context, otherwise the secure-context-only APIs
  (userAgentData, deviceMemory, EME, Web Share) are missing and look like
  platform differences. Playwright needs `LD_LIBRARY_PATH` pointing at the
  extracted libs. CloakBrowser (npm `cloakbrowser`, Node 20 or newer) allows
  one session at a time on the free tier.
- `rows.py <runs> <tests.json> <out prefix> [--deprecate ids]`: baseline
  payload version 2 rows from genuine runs, active tests only, with a noise
  filter for tests that disagree within one browser build.
- `probe.py <runs> <json url> [payload]`: replays captured runs as v3 and v2
  payloads against a JSON endpoint and checks no baseline leaks.
- `analyse.py`: USE means stable per browser across repeat headed runs and a
  real differing value from the Linux build claiming Windows.
- `cloak-run.js` and `wsl-run.js`: the version 2 runners.
