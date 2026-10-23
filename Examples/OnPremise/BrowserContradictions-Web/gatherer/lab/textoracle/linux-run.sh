#!/bin/bash
# Runs the oracle and A/B pages in Linux Chromium and CloakBrowser from WSL.
# Needs ~/bc-lab (playwright, cloakbrowser, math-run.js) and the lab server
# on the Windows host at port $1 (default 5195).
PORT=${1:-5195}
cd ~/bc-lab || exit 1
export PATH=~/node22/bin:$PATH LD_LIBRARY_PATH=~/bc-lab/libs/usr/lib/x86_64-linux-gnu
sed "s/5190/$PORT/" math-run.js > oracle-run.js
for page in ${PAGES:-oracle ab}; do
  node oracle-run.js "$page-linux-pw141" shell "" "/page/$page.html" 2>&1 | grep -v "^\s" | head -1
  node oracle-run.js "$page-linux-cloak-win" cloak windows "/page/$page.html" 2>&1 | grep -v "^\s\|Update available" | head -1
  node oracle-run.js "$page-linux-cloak-linux" cloak linux "/page/$page.html" 2>&1 | grep -v "^\s\|Update available" | head -1
  # CloakBrowser's Windows persona with its Windows font metrics option.
  CB_WIN_FONTS=1 node -e "
    const { launch } = require('cloakbrowser');
    (async () => {
      const b = await launch({ headless: true, args: ['--host-resolver-rules=MAP localhost 172.28.0.1', '--fingerprint=12345', '--fingerprint-platform=windows', '--fingerprint-windows-font-metrics'] });
      const p = await b.newPage();
      await p.goto('http://localhost:$PORT/page/$page.html?label=$page-linux-cloak-winfonts', { waitUntil: 'load' });
      await p.waitForFunction(() => document.title === 'done' || document.title === 'failed', null, { timeout: 60000 }).catch(() => {});
      console.log('$page-linux-cloak-winfonts', await p.title());
      await b.close();
    })().catch(e => { console.error('ERR', e.message); process.exit(1); });" 2>&1 | grep -v "Update available" | head -1
done
