// Gatherer run time under Puppeteer, optionally with DevTools CPU
// throttling, one fresh browser and profile per run.
//   node timing-run.js <exe> <label> <runs> <cpu rate, 1 = none> [budget ms]
// The lab page posts each run (with per test timings) to the collector as
// usual, labelled <label>. BC_URL moves the page (default
// http://localhost:5180/). puppeteer-core resolves from NODE_PATH.
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');
(async () => {
  const [exe, label, runs, rate, budget = '15000'] = process.argv.slice(2);
  for (let i = 1; i <= Number(runs); i++) {
    const dir = path.join(process.env.BC_PROFILES || 'C:\bc-temp', 'timing-' + label + '-' + i);
    const browser = await puppeteer.launch({ executablePath: exe, headless: false, userDataDir: dir,
      args: ['--window-position=-2400,-2400', '--window-size=1024,768'] });
    const page = await browser.newPage();
    if (Number(rate) > 1) await page.emulateCPUThrottling(Number(rate));
    await page.goto((process.env.BC_URL || 'http://localhost:5180/') + '?budget=' + budget + '&label=' + encodeURIComponent(label), { waitUntil: 'load' });
    await page.waitForFunction(() => document.title === 'done' || document.title === 'failed', { timeout: 120000 }).catch(() => {});
    console.log(label, i, await page.evaluate(() => document.getElementById('out').textContent));
    await browser.close();
    fs.rmSync(dir, { recursive: true, force: true });
  }
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
