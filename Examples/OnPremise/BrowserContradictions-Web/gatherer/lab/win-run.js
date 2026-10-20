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
// The profile is deleted afterwards and the whole process tree is stopped.
const { spawn, execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const [kind, exe, label, url, runs, timeoutS, userAgent] = process.argv.slice(2);
const temp = process.env.BC_PROFILES || 'C:\\bc-temp';
const profile = path.join(temp, 'prof-' + label + '-' + Date.now());
fs.mkdirSync(profile, { recursive: true });
const full = url + (url.includes('?') ? '&' : '?') + 'label=' + encodeURIComponent(label);
let args;
if (kind === 'firefox' || kind === 'firefox-headless') {
  args = ['-no-remote', '-new-instance', '-profile', profile].concat(kind === 'firefox-headless' ? ['-headless'] : ['-width', '1024', '-height', '768']).concat([full]);
} else {
  args = ['--user-data-dir=' + profile, '--no-first-run', '--no-default-browser-check'].concat(userAgent ? ['--user-agent=' + userAgent] : []);
  args = kind === 'headless' ? args.concat(['--headless=new', full]) : args.concat(['--window-position=-2400,-2400', '--window-size=1024,768', '--app=' + full]);
}
const before = new Set(fs.readdirSync(runs));
const started = Date.now();
const child = spawn(exe, args, { stdio: 'ignore', detached: false });
const limit = (Number(timeoutS) || 40) * 1000;
const timer = setInterval(() => {
  const fresh = fs.readdirSync(runs).filter(f => !before.has(f) && f.startsWith(label + '-'));
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
      console.log((fresh.length ? 'stored ' + fresh[0] : 'TIMEOUT ' + label) + ' in ' + (Date.now() - started) + 'ms');
      process.exit(fresh.length ? 0 : 1);
    }, 1500);
  }
}, 250);
