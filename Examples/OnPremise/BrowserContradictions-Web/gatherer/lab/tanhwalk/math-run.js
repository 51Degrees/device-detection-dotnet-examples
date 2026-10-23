// node math-run.js <label> <kind: shell|full|cloak> [platform for cloak] [page path]
// Copy to ~/bc-lab in WSL (playwright and cloakbrowser installed there).
// Opens the maths lab page on the Windows host (port 5190) and waits for it to post.
const [label, kind, platform, pagePath] = process.argv.slice(2);
const MAP = '--host-resolver-rules=MAP localhost 172.28.0.1';
const path = pagePath || '/';
(async () => {
  let browser;
  if (kind === 'cloak') {
    const { launch } = require('cloakbrowser');
    const args = [MAP, '--fingerprint=12345'];
    if (platform) args.push('--fingerprint-platform=' + platform);
    browser = await launch({ headless: true, args });
  } else {
    const { chromium } = require('playwright');
    const opts = { headless: true, args: [MAP] };
    if (kind === 'full') opts.channel = 'chromium';
    browser = await chromium.launch(opts);
  }
  const page = await browser.newPage();
  const sep = path.includes('?') ? '&' : '?';
  await page.goto('http://localhost:5190' + path + sep + 'label=' + encodeURIComponent(label), { waitUntil: 'load' });
  await page.waitForFunction(() => document.title === 'done' || document.title === 'failed', null, { timeout: 60000 }).catch(() => {});
  console.log(label, await page.title(), '|', await page.evaluate(() => navigator.userAgent));
  await browser.close();
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
