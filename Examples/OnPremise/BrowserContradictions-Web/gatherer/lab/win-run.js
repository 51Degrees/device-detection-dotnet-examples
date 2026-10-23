// Runs one genuine Windows browser against the lab collector and waits for
// its run to be stored.
//   node win-run.js <kind> <exe> <label> <url> <runs dir> [timeout s] [user agent]
// kind
//   chromium   Chrome or Edge, headed, fresh profile, an off-screen --app=
//              window, nothing else changed.
//   firefox    Firefox, headed, fresh profile.
//   firefox-headless  Firefox with -headless.
//   headless   Chrome or Edge with --headless=new and no driver at all. A
//              user agent argument swaps the HeadlessChrome one, as
//              scrapers do.
// runs is a folder of lab runs, or the proof site's reports.jsonl, where the
// run is done once the page's second pass has reported under the label.
// The profile is deleted afterwards and the whole process tree is stopped.
// Environment, Chrome and Edge headed only:
//   BC_VISIBLE=1  keeps the off-screen window visible (see below)
//   BC_EXTRA      extra switches, space separated, for example
//                 --force-device-scale-factor=1.25
//   BC_CPU=<n>    slows the page n times through DevTools CPU throttling,
//                 as a slow device would be. Chrome starts on about:blank
//                 with --remote-debugging-port=0, and one DevTools session
//                 on the page sends only Emulation.setCPUThrottlingRate and
//                 Page.navigate: no Runtime domain and none of the drivers'
//                 switches. Chrome 155 still sets navigator.webdriver under
//                 --remote-debugging-port, so p_webdriver.1 reads true.
//   BC_PREFS      JSON written as the fresh profile's Default/Preferences
//                 before the start, for a user setting such as
//                 {"webkit":{"webprefs":{"encrypted_media_enabled":false}}}
//   BC_CPU_RATE=<n> starts the browser in a job object hard capped at n
//                 hundredths of a percent of the machine (slow-launch.ps1),
//                 with nothing the page can see: on 6 logical processors
//                 417 is a quarter of one processor, about 4x slower.
const { spawn, execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const [kind, exe, label, url, runs, timeoutS, userAgent] = process.argv.slice(2);
const temp = process.env.BC_PROFILES || 'C:\\bc-temp';
const profile = path.join(temp, 'prof-' + label + '-' + Date.now());
fs.mkdirSync(profile, { recursive: true });
// BC_FFPREFS is written as the fresh Firefox profile's user.js, for example
// user_pref("privacy.resistFingerprinting", true);
if (process.env.BC_FFPREFS) fs.writeFileSync(path.join(profile, 'user.js'), process.env.BC_FFPREFS);
if (process.env.BC_PREFS) {
  fs.mkdirSync(path.join(profile, 'Default'), { recursive: true });
  fs.writeFileSync(path.join(profile, 'Default', 'Preferences'), process.env.BC_PREFS);
}
const full = url + (url.includes('?') ? '&' : '?') + 'label=' + encodeURIComponent(label);
let args;
if (kind === 'firefox' || kind === 'firefox-headless') {
  args = ['-no-remote', '-new-instance', '-profile', profile].concat(kind === 'firefox-headless' ? ['-headless'] : ['-width', '1024', '-height', '768']).concat([full]);
} else {
  args = ['--user-data-dir=' + profile, '--no-first-run', '--no-default-browser-check'].concat(userAgent ? ['--user-agent=' + userAgent] : []);
  // Chrome marks a window off screen as occluded and the page as hidden,
  // which throttles its timers to one a second, so the tests take 15 to 50
  // seconds instead of one. BC_VISIBLE=1 keeps the page visible, as it is in
  // front of a person, and changes nothing the tests read.
  if (process.env.BC_VISIBLE === '1' && kind !== 'headless') args.push('--disable-backgrounding-occluded-windows');
  if (process.env.BC_EXTRA && kind !== 'headless') args = args.concat(process.env.BC_EXTRA.split(' ').filter(Boolean));
  if (process.env.BC_CPU && kind !== 'headless') args.push('--remote-debugging-port=0');
  args = kind === 'headless' ? args.concat(['--headless=new', full]) : args.concat(['--window-position=-2400,-2400', '--window-size=1024,768', '--app=' + (process.env.BC_CPU ? 'about:blank' : full)]);
}
// DevTools CPU throttling, then the lab page (BC_CPU).
async function throttle() {
  const portFile = path.join(profile, 'DevToolsActivePort');
  const until = Date.now() + 20000;
  while (!fs.existsSync(portFile) && Date.now() < until) await new Promise(r => setTimeout(r, 200));
  const port = fs.readFileSync(portFile, 'utf8').split(/\s+/)[0];
  let target = null;
  while (!target && Date.now() < until) {
    try { target = (await (await fetch('http://127.0.0.1:' + port + '/json/list')).json()).find(t => t.type === 'page'); } catch (e) { }
    if (!target) await new Promise(r => setTimeout(r, 200));
  }
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
  let id = 0;
  const send = (method, params) => new Promise(res => {
    const my = ++id;
    ws.addEventListener('message', function on(m) { const d = JSON.parse(m.data); if (d.id === my) { ws.removeEventListener('message', on); res(d); } });
    ws.send(JSON.stringify({ id: my, method, params }));
  });
  const t = await send('Emulation.setCPUThrottlingRate', { rate: Number(process.env.BC_CPU) });
  if (t.error) console.error('throttle', JSON.stringify(t.error));
  await send('Page.navigate', { url: full });
}
const isLog = runs.endsWith('.jsonl');
const before = isLog ? null : new Set(fs.readdirSync(runs));
function stored() {
  if (!isLog) return fs.readdirSync(runs).filter(f => !before.has(f) && f.startsWith(label + '-'));
  if (!fs.existsSync(runs)) return [];
  return fs.readFileSync(runs, 'utf8').split('\n').filter(l => {
    try { const o = JSON.parse(l); return o.label === label && o.pass === '2'; } catch (e) { return false; }
  });
}
const started = Date.now();
let child;
if (process.env.BC_CPU_RATE) {
  const list = path.join(temp, 'slow-' + label + '-' + Date.now() + '.json');
  fs.writeFileSync(list, JSON.stringify([exe].concat(args)));
  child = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(__dirname, 'slow-launch.ps1'), list, process.env.BC_CPU_RATE], { stdio: 'ignore' });
  child.on('exit', () => { try { fs.unlinkSync(list); } catch (e) { } });
} else child = spawn(exe, args, { stdio: 'ignore', detached: false });
if (process.env.BC_CPU && kind !== 'headless') throttle().catch(e => console.error('throttle failed', e.message));
const limit = (Number(timeoutS) || 40) * 1000;
const timer = setInterval(() => {
  const fresh = stored();
  const late = Date.now() - started > limit;
  if (fresh.length || late) {
    clearInterval(timer);
    try { execSync('taskkill /T /F /PID ' + child.pid, { stdio: 'ignore' }); } catch (e) { }
    // Edge and Firefox hand over to processes outside the launcher's tree,
    // so stop every browser process whose command line names this profile.
    const ps = "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*" + profile.replace(/'/g, "''") + "*' -and $_.Name -match '^(msedge|chrome|firefox|chrome-headless-shell)\.exe$' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }";
    try { execSync('powershell.exe -NoProfile -EncodedCommand ' + Buffer.from(ps, 'utf16le').toString('base64'), { stdio: 'ignore' }); } catch (e) { }
    setTimeout(() => {
      try { fs.rmSync(profile, { recursive: true, force: true }); } catch (e) { }
      console.log((fresh.length ? 'stored ' + (isLog ? label : fresh[0]) : 'TIMEOUT ' + label) + ' in ' + (Date.now() - started) + 'ms');
      process.exit(fresh.length ? 0 : 1);
    }, 1500);
  }
}, 250);
