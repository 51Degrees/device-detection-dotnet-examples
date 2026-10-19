// Plain automation on a genuine Windows box: the lab page through Puppeteer
// or Playwright with their defaults, nothing hidden, to see which tests
// catch automation by itself.
//   node auto-run.js <puppeteer|playwright> <exe> <label> <url> [headless|shell|headed]
// puppeteer-core and playwright-core resolve from NODE_PATH.
//   headless  the library's default headless mode (new headless)
//   shell     Puppeteer's headless: 'shell' (chrome-headless-shell exe)
//   headed    a visible window, still driven
(async () => {
  const [lib, exe, label, url, mode = 'headless'] = process.argv.slice(2);
  const full = url + (url.includes('?') ? '&' : '?') + 'label=' + encodeURIComponent(label);
  let browser, page;
  if (lib === 'puppeteer') {
    const puppeteer = require('puppeteer-core');
    browser = await puppeteer.launch({ executablePath: exe, headless: mode === 'headed' ? false : mode === 'shell' ? 'shell' : true });
    page = await browser.newPage();
  } else {
    const { chromium } = require('playwright-core');
    browser = await chromium.launch({ executablePath: exe, headless: mode !== 'headed' });
    page = await (await browser.newContext()).newPage();
  }
  await page.goto(full, { waitUntil: 'load' });
  await page.waitForFunction(() => document.title === 'done' || document.title === 'failed', { timeout: 30000 }).catch(() => {});
  console.log(label, (await page.evaluate(() => document.getElementById('out').textContent)).slice(0, 120));
  await browser.close();
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
