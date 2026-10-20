# Browser tests lab

Captures what real browsers answer to the gatherer, turns genuine captures
into baseline rows, and replays or drives browsers against the proof site.

- `collect.py <port> [gatherer]`: serves the lab page (gatherer v3 by
  default) and stores one JSON per run in `runs/`, or in `BC_RUNS` when
  set. `?label=` names the run and `?params=<json>` runs the gatherer with
  chosen inputs. `/maths` runs `maths-probe.js` (20 Math functions over
  thousands of fixed inputs, every result stored) and `/voices` stores the
  full speech voice list.
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
- The lab page also takes `?budget=<ms>` (default 4000), `?sw=1` (runs the
  service worker tests with `../browser-tests-sw.js`) and `?only=id,id`, and
  every run stores the per test timings.
- `timing-run.js <exe> <label> <runs> <cpu rate> [budget]`: run time through
  Puppeteer with a fresh browser per run, optionally with DevTools CPU
  throttling (`4` for 4x). `BC_URL` points it at another collector.
- `win-run.js <kind> <exe> <label> <url> <runs dir> [timeout s] [user agent]`: one genuine Windows
  run, headed (`--app=` window off screen, fresh profile) or Firefox, then
  stops every process using that profile. `<runs dir>` may instead be the
  proof site's `reports.jsonl`: the run is then done once the page has
  reported its second pass (the page reloads itself once, so the page
  request carries the cookie). `BC_VISIBLE=1` adds
  `--disable-backgrounding-occluded-windows`: Chrome otherwise treats the
  off screen window as hidden, throttles its timers and the tests take 15
  to 50 seconds. Clean start conditions: `BC_EXTRA` adds switches
  (`--force-device-scale-factor=1.25`), `BC_PREFS` seeds the fresh
  profile's Preferences (a user setting), `BC_CPU_RATE=800` runs the
  browser in a job object capped at 8% of the machine through
  `slow-launch.ps1` (about 4x slower on 6 logical processors, nothing the
  page can see), and `BC_CPU=4` uses DevTools throttling instead (which
  sets navigator.webdriver, through --remote-debugging-port). The lab page
  takes `?reload=1` (runs once, reloads, stores the second run) and
  `?csp=1` (a Content Security Policy that blocks blob: workers), and
  stores the page's visibility and focus with each run. `auto-run.js` drives the same
  page through Puppeteer or Playwright with their default options
  (`NODE_PATH` pointing at puppeteer-core and playwright-core).
- `rows.py <runs> <tests.json> <out prefix> [--deprecate ids]`: baseline
  rows. NO_ROWS lists bad actor rules that never get rows (s_offscreen.1).
  The final hardening rows come from the `v5-train-` runs only, so the
  `v5-stress-` and `v5-extra-` runs replay as held out captures. Baseline
  payload version 2 rows from genuine runs, active tests only, with a noise
  filter for tests that disagree within one browser build, version
  independent `*` rows and their range names. Chrome for Testing runs give
  no rows for the build flavour tests (Google voices, Widevine, WebGL,
  platform APIs).
- `matrix.py <runs> <tests.json> <out.tsv> [prefix]`: test by browser
  matrix of short value hashes, unstable columns starred.
- `final_probe.py <runs> <tests.json> [pearl rows] [ranges]`: replays every
  capture through the element rule on rows from `rows.py` (and optionally a
  Pearl BaselineDryRun's rows) and checks each verdict. Supersedes
  `vr_probe.py`, whose Chrome 148 and 150 captures are Chrome for Testing
  and now fail the build flavour tests by design.
- `probe.py <runs> <json url> [payload]`: replays captured runs as v3 and v2
  payloads against a JSON endpoint and checks no baseline leaks.
- `analyse.py`: USE means stable per browser across repeat headed runs and a
  real differing value from the Linux build claiming Windows.
- `cloak-run.js` and `wsl-run.js`: the version 2 runners. `site-run.js`
  takes the CloakBrowser seed from `CLOAK_SEED`.
