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
- `win-run.js <kind> <exe> <label> <url> <runs dir>`: one genuine Windows
  run, headed (`--app=` window off screen, fresh profile) or Firefox, then
  stops every process using that profile. `auto-run.js` drives the same
  page through Puppeteer or Playwright with their default options
  (`NODE_PATH` pointing at puppeteer-core and playwright-core).
- `rows.py <runs> <tests.json> <out prefix> [--deprecate ids]`: baseline
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
