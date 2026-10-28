param(
    [ValidateSet('Transparent','Offscreen')][string]$WindowMode = 'Transparent',
    [ValidateRange(800,3840)][int]$Width = 1366,
    [ValidateRange(600,2160)][int]$Height = 768
)
$ErrorActionPreference = 'Stop'
# This validation player starts hidden. Transparent mode verifies alpha zero
# before logically showing its click-through, non-activating tool window at
# the bottom of the monitor's Z order. It remains invisible to the user.
# Only this helper's child process and its UnityWndClass window are touched.
Add-Type @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class AuroraValidationWindow {
    static readonly HashSet<IntPtr> reportedWindows=new HashSet<IntPtr>();
    delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out WindowRect rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", SetLastError=true)] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW", SetLastError=true)] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW", SetLastError=true)] static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    [DllImport("kernel32.dll")] static extern void SetLastError(uint error);
    [DllImport("user32.dll", SetLastError=true)] static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
    [DllImport("user32.dll", SetLastError=true)] static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
    public static void KeepInvisible(int childProcess,bool transparent) {
        Exception failure=null;
        EnumWindows((window, parameter) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner != childProcess) return true;
            var name=new StringBuilder(256); GetClassName(window,name,256);
            bool firstReport=reportedWindows.Add(window);
            if(firstReport) Console.WriteLine("Aurora owned window: pid="+owner+" hwnd="+window+" class="+name+" visible="+IsWindowVisible(window));
            if (name.ToString()!="UnityWndClass") return true;
            try {
                WindowRect rect; if(!GetWindowRect(window,out rect)) return true;
                if(firstReport) Console.WriteLine("Aurora before styles: rect="+rect.Left+","+rect.Top+","+rect.Right+","+rect.Bottom+" exstyle=0x"+GetWindowLongPtr(window,-20).ToInt64().ToString("x"));
                int x=-10000,y=-10000; IntPtr after=IntPtr.Zero; uint moveFlags=0x0010|0x0001|0x0004|0x0040|0x4000;
                bool styleChanged=false;
                if(transparent) {
                    // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW.
                    // Remove APPWINDOW so an invisible validation window cannot create a taskbar button.
                    const long required=0x00080000L|0x00000020L|0x08000000L|0x00000080L;
                    long before=GetWindowLongPtr(window,-20).ToInt64();
                    long desired=(before|required)&~0x00040000L;
                    if(before!=desired) {
                        if(firstReport) Console.WriteLine("Aurora applying non-interactive layered style.");
                        SetLastError(0);
                        IntPtr previous=SetWindowLongPtr(window,-20,new IntPtr(desired));
                        int error=Marshal.GetLastWin32Error();
                        if(previous==IntPtr.Zero && error!=0) throw new Win32Exception(error,"Could not make validation window non-interactive.");
                        styleChanged=true;
                    }
                    uint key,flags; byte alpha;
                    if(!GetLayeredWindowAttributes(window,out key,out alpha,out flags) || alpha!=0 || (flags&2)==0) {
                        if(firstReport) Console.WriteLine("Aurora applying alpha zero.");
                        if(!SetLayeredWindowAttributes(window,0,0,2))
                            throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not make validation window fully transparent; leaving it hidden.");
                    }
                    if((GetWindowLongPtr(window,-20).ToInt64()&required)!=required ||
                       !GetLayeredWindowAttributes(window,out key,out alpha,out flags) || alpha!=0 || (flags&2)==0)
                        throw new InvalidOperationException("Validation window invisibility could not be verified; it will not be shown.");
                    if(firstReport) Console.WriteLine("Aurora invisibility verified: alpha="+alpha+" flags="+flags+" exstyle=0x"+GetWindowLongPtr(window,-20).ToInt64().ToString("x"));
                    x=0; y=0; after=new IntPtr(1); // HWND_BOTTOM, no activation.
                    moveFlags=0x0010|0x0001|0x0040|0x4000; // Post positioning to the window's own thread.
                }
                if(styleChanged || rect.Left!=x || rect.Top!=y || !IsWindowVisible(window)) {
                    if(firstReport) Console.WriteLine("Aurora posting invisible positioning/show request.");
                    if(!SetWindowPos(window,after,x,y,0,0,moveFlags))
                        throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not position the invisible validation window.");
                    if(firstReport) Console.WriteLine("Aurora position request returned; visible="+IsWindowVisible(window));
                }
            } catch(Exception e) {
                // Fail closed even if Unity changed window styles during a resize.
                SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x0010|0x0001|0x0002|0x0004|0x0080|0x4000);
                failure=e; return false;
            }
            return true;
        },IntPtr.Zero);
        if(failure!=null) throw failure;
    }
}
'@
$validationDirectory = $PSScriptRoot.Replace('\','/')
$playerPath = Join-Path $PSScriptRoot 'Build/AuroraLayout.exe'
# BitBlt presentation supports the validation-only layered transparent window.
# This per-process switch leaves the project's normal rendering settings intact.
$presentationMode = if ($WindowMode -eq 'Transparent') { '-force-d3d11-bitblt-model' } else { '' }
$arguments = "-force-d3d11 $presentationMode -popupwindow -screen-fullscreen 0 -screen-width $Width -screen-height $Height -logFile `"$validationDirectory/Layout-player-${Width}x${Height}.log`" --aurora-validation-output `"$validationDirectory`" --aurora-validation-width $Width --aurora-validation-height $Height"
$child = Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds(150)
try {
    while (!$child.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        [AuroraValidationWindow]::KeepInvisible($child.Id, $WindowMode -eq 'Transparent')
        Start-Sleep -Milliseconds 30
        $child.Refresh()
    }
    if (!$child.HasExited) { throw 'Validation player exceeded launcher timeout.' }
    $nativeLog = Join-Path $PSScriptRoot "Layout-player-${Width}x${Height}.log"
    $nativePresentationWarnings = @(Select-String -LiteralPath $nativeLog -SimpleMatch 'GfxDeviceD3D11Base::PresentFrame()').Count
    $nativeReport = Join-Path $PSScriptRoot "HUD-player-results-${Width}x${Height}.txt"
    Add-Content -LiteralPath $nativeReport -Encoding UTF8 -Value "Native hidden-window D3D presentation warnings: $nativePresentationWarnings (see player log). These bypass the managed error callback; this harness does not establish focused-window latency."
    Write-Output "Validation player exited: $($child.ExitCode)"
    exit $child.ExitCode
} finally {
    $child.Refresh()
    if (!$child.HasExited) { Stop-Process -Id $child.Id -ErrorAction SilentlyContinue }
}
