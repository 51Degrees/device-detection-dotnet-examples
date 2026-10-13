// Runs CloakBrowser against the lab collector or the proof site.
// node cloak-run.js <label> <platform: windows|macos|linux> <target: lab|site> [headless true|false]
const { launch } = require('cloakbrowser');
(async () => {
  const [label, platform, target, headlessArg] = process.argv.slice(2);
  const headless = headlessArg !== 'false';
  const args = ['--host-resolver-rules=MAP localhost HOST', '--fingerprint=12345'];
  if (platform && platform !== 'auto') args.push('--fingerprint-platform=' + platform);
  if (process.env.CB_WIN_FONTS) args.push('--fingerprint-windows-font-metrics');
  const browser = await launch({ headless, args });
  const page = await browser.newPage();
  const url = target === 'site' ? 'http://localhost:5150/' : 'http://localhost:5180/?label=' + label;
  await page.goto(url, { waitUntil: 'load' });
  if (target === 'site') {
    await page.waitForFunction(() => !document.getElementById('client').textContent.startsWith('waiting'), null, { timeout: 25000 }).catch(() => {});
    console.log(label, 'UA:', await page.evaluate(() => navigator.userAgent));
    console.log(await page.textContent('#client'));
  } else {
    await page.waitForFunction(() => document.title === 'done' || document.title === 'failed', null, { timeout: 25000 }).catch(() => {});
    console.log(label, await page.textContent('#out'), '| UA:', await page.evaluate(() => navigator.userAgent));
  }
  await browser.close();
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
