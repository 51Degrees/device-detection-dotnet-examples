# Starts a browser inside a Windows job object whose CPU rate is hard
# capped, as a slow device would run it, with no DevTools session and no
# switch the page can see. win-run.js uses it when BC_CPU_RATE is set.
#   slow-launch.ps1 <args json file> <cpu rate>
# The JSON file holds [exe, arg, arg, ...]. The rate is in hundredths of a
# percent of the whole machine: on 6 logical processors 417 is a quarter of
# one processor, so one busy thread runs about 4x slower and every process
# of the browser shares that. Prints the browser's process id.
param([string]$ArgsFile, [int]$Rate)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SlowJob {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO { public int cb; public string r; public string d; public string t; public int x, y, w, h, cx, cy, fill, flags; public short show, r2; public IntPtr r3, i, o, e; }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int pid, tid; }
    [StructLayout(LayoutKind.Sequential)]
    struct CPU_RATE { public uint ControlFlags; public uint CpuRate; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateJobObject(IntPtr a, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr job, int cls, ref CPU_RATE info, int len);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proc);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string app, System.Text.StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint ResumeThread(IntPtr t);
    public static int Start(string exe, string cmd, uint rate) {
        IntPtr job = CreateJobObject(IntPtr.Zero, null);
        var info = new CPU_RATE { ControlFlags = 0x1 | 0x4, CpuRate = rate };
        if (!SetInformationJobObject(job, 15, ref info, Marshal.SizeOf(info))) throw new Exception("rate " + Marshal.GetLastWin32Error());
        var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(si);
        PROCESS_INFORMATION pi;
        if (!CreateProcess(exe, new System.Text.StringBuilder(cmd), IntPtr.Zero, IntPtr.Zero, false, 0x4, IntPtr.Zero, null, ref si, out pi)) throw new Exception("start " + Marshal.GetLastWin32Error());
        if (!AssignProcessToJobObject(job, pi.hProcess)) throw new Exception("assign " + Marshal.GetLastWin32Error());
        ResumeThread(pi.hThread);
        return pi.pid;
    }
}
'@
$list = Get-Content -Raw $ArgsFile | ConvertFrom-Json
$exe = $list[0]
$quoted = @($list | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } })
[SlowJob]::Start($exe, ($quoted -join ' '), [uint32]$Rate)
