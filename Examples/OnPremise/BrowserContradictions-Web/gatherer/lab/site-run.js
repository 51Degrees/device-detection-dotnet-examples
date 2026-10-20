// Runs a WSL browser against the proof site or the lab collector.
// node site-run.js <engine: chromium|cloak> <label> <port and path> <host ip> [persona or user agent]
//   port and path  ":5151/" or ":5151/cloud" for the proof site (on-prem or
//                  through the cloud), ":5180/" for the lab collector.
//   chromium  Playwright's full Chromium build, headless, with the automation
//             flag off so navigator.webdriver does not decide the answer.
//             The optional last argument is a user agent to claim. One that
//             claims Windows or macOS also gets matching client hints, as a
//             careful forger would send.
//   cloak     CloakBrowser with a persona: windows, macos, linux or auto.
// localhost is mapped to the Windows host so the page stays a secure
// context, otherwise the secure-context-only APIs vanish and look like
// platform differences. The proof site page posts its answer to /report and
// sets its title to "reported", the lab page sets "done".
(async () => {
  const [engine, label, path, host, extra] = process.argv.slice(2);
  const args = ['--host-resolver-rules=MAP localhost ' + host];
  let browser, page;
  if (engine === 'cloak') {
    const { launch } = require('cloakbrowser');
    // CLOAK_SEED picks the fingerprint seed, so personas can be compared across seeds.
    args.push('--fingerprint=' + (process.env.CLOAK_SEED || '12345'));
    if (extra && extra !== 'auto') args.push('--fingerprint-platform=' + extra);
    browser = await launch({ headless: true, args });
    page = await browser.newPage();
  } else {
    const { chromium } = require('playwright');
    browser = await chromium.launch({
      headless: true, channel: 'chromium',
      args: args.concat(['--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--disable-blink-features=AutomationControlled']),
    });
    const context = await browser.newContext(extra ? { userAgent: extra } : {});
    page = await context.newPage();
    const platform = extra && (/Windows/.test(extra) ? 'Windows' : /Mac OS X/.test(extra) ? 'macOS' : null);
    if (platform) {
      const major = (extra.match(/Chrome\/(\d+)/) || [])[1] || '0';
      const brands = [{ brand: 'Google Chrome', version: major }, { brand: 'Chromium', version: major }, { brand: 'Not=A?Brand', version: '24' }];
      const client = await context.newCDPSession(page);
      await client.send('Emulation.setUserAgentOverride', {
        userAgent: extra,
        userAgentMetadata: {
          brands, fullVersionList: brands.map(b => ({ brand: b.brand, version: b.version + '.0.0.0' })),
          platform, platformVersion: platform === 'Windows' ? '19.0.0' : '15.0.0', architecture: 'x86',
          model: '', mobile: false, bitness: '64', wow64: false,
        },
      });
    }
  }
  const sep = path.includes('?') ? '&' : '?';
  await page.goto('http://localhost' + path + sep + 'label=' + encodeURIComponent(label), { waitUntil: 'load' });
  // The proof site loads itself a second time (pass 2), which ends a
  // waitForFunction early, so wait again until the title says done.
  const until = Date.now() + 40000;
  while (Date.now() < until) {
    const ok = await page.waitForFunction(() => document.title === 'reported' || document.title === 'done' || document.title === 'failed', null, { timeout: 5000 }).then(() => true, () => false);
    if (ok) break;
  }
  const out = await page.textContent(path.startsWith(':5180') ? '#out' : '#client');
  console.log(out.replace(/\s+/g, ' ').slice(0, 300));
  await browser.close();
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
