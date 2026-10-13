// node wsl-run.js <label> <mode: shell|full> [userAgent]
const { chromium } = require('playwright');
(async () => {
  const [label, mode, ua] = process.argv.slice(2);
  const args = ['--enable-unsafe-webgpu', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'];
  const opts = { headless: true, args };
  if (mode === 'full') opts.channel = 'chromium';
  const browser = await chromium.launch(opts);
  const ctx = await browser.newContext(ua ? { userAgent: ua } : {});
  const page = await ctx.newPage();
  await page.goto('http://HOST:5180/?label=' + label, { waitUntil: 'load' });
  await page.waitForFunction(() => document.title === 'done' || document.title === 'failed', null, { timeout: 20000 }).catch(() => {});
  console.log(label, await page.textContent('#out'), '|', (await browser.version()));
  await browser.close();
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
