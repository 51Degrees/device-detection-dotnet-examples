# Runs the oracle and A/B pages in genuine Windows browsers under a matrix of
# conditions. Fresh profile per run under C:\bc-temp, deleted afterwards.
#   pwsh run-win.ps1 [-Port 5195] [-Page oracle|ab|ab2|both|all]
param([int]$Port = 5195, [string]$Page = "both")
$res = Join-Path $PSScriptRoot "results"
New-Item -ItemType Directory -Force $res | Out-Null
$chrome = "C:\Program Files\Google\Chrome\Application\chrome.exe"
$browsers = @(
  @{ b = "chrome155"; p = $chrome },
  @{ b = "beta156"; p = "C:\Program Files\Google\Chrome Beta\Application\chrome.exe" },
  @{ b = "edge"; p = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" },
  @{ b = "cft148"; p = "C:\Users\jamesr\.cache\puppeteer\chrome\win64-148.0.7778.97\chrome-win64\chrome.exe" },
  @{ b = "cft150"; p = "C:\Users\jamesr\.cache\puppeteer\chrome\win64-150.0.7871.24\chrome-win64\chrome.exe" })
# Conditions, run on Chrome 155 only except "default", which every browser runs.
$conditions = @(
  @{ c = "default"; args = @("--headless=new", "--disable-gpu") },
  @{ c = "gpu"; args = @("--headless=new") },
  @{ c = "scale150"; args = @("--headless=new", "--disable-gpu", "--force-device-scale-factor=1.5") },
  @{ c = "small"; args = @("--headless=new", "--disable-gpu", "--window-size=800,600") },
  @{ c = "headed"; args = @("--disable-gpu", "--window-size=1200,900") },
  @{ c = "lang-de"; args = @("--headless=new", "--disable-gpu", "--lang=de-DE") },
  @{ c = "nohint"; args = @("--headless=new", "--disable-gpu", "--font-render-hinting=none") },
  @{ c = "userfonts"; args = @("--headless=new", "--disable-gpu"); prefs = $true })
function Run-One($b, $exe, $cond, $page) {
  $label = "$page-win-$b-$($cond.c)"
  $prof = "C:\bc-temp\oracle-$label"
  if (Test-Path $prof) { Remove-Item $prof -Recurse -Force }
  if ($cond.prefs) {
    New-Item -ItemType Directory -Force "$prof\Default" | Out-Null
    # A user who changed Chrome's default fonts.
    '{"webkit":{"webprefs":{"fonts":{"standard":{"Zyyy":"Verdana"},"sansserif":{"Zyyy":"Verdana"},"serif":{"Zyyy":"Georgia"},"fixed":{"Zyyy":"Consolas"}}}}}' |
      Set-Content -Path "$prof\Default\Preferences" -Encoding utf8
  }
  $out = "$res\$label.json"
  if (Test-Path $out) { Remove-Item $out }
  $args = $cond.args + @("--user-data-dir=$prof", "--no-first-run", "http://localhost:$Port/page/$page.html?label=$label")
  Start-Process -FilePath $exe -ArgumentList $args | Out-Null
  $deadline = (Get-Date).AddSeconds(90)
  while ((Get-Date) -lt $deadline -and -not (Test-Path $out)) { Start-Sleep -Milliseconds 500 }
  Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*$prof*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep -Milliseconds 700
  Remove-Item $prof -Recurse -Force -ErrorAction SilentlyContinue
  "{0}: {1}" -f $label, (Test-Path $out)
}
$pages = if ($Page -eq "both") { @("oracle", "ab") } elseif ($Page -eq "all") { @("oracle", "ab", "ab2") } else { @($Page) }
foreach ($page in $pages) {
  foreach ($br in $browsers) { Run-One $br.b $br.p $conditions[0] $page }
  foreach ($cond in $conditions[1..($conditions.Count - 1)]) { Run-One "chrome155" $chrome $cond $page }
  # Firefox: a genuine browser with its own text stack, for reference.
  $label = "$page-win-firefox-default"; $ffprof = "C:\bc-temp\oracle-$label"
  New-Item -ItemType Directory -Force $ffprof | Out-Null
  $out = "$res\$label.json"; if (Test-Path $out) { Remove-Item $out }
  Start-Process -FilePath "C:\Program Files\Mozilla Firefox\firefox.exe" -ArgumentList "-headless", "-no-remote", "-profile", $ffprof, "http://localhost:$Port/page/$page.html?label=$label" | Out-Null
  $deadline = (Get-Date).AddSeconds(90); while ((Get-Date) -lt $deadline -and -not (Test-Path $out)) { Start-Sleep -Milliseconds 500 }
  Get-CimInstance Win32_Process -Filter "Name='firefox.exe'" | Where-Object { $_.CommandLine -like "*$ffprof*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
  Start-Sleep 1; Remove-Item $ffprof -Recurse -Force -ErrorAction SilentlyContinue
  "{0}: {1}" -f $label, (Test-Path $out)
}
