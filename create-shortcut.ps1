# 创建快捷方式（幂等：已存在则跳过）
# 1. ResSwitcher.lnk      → 运行程序（dist\ResSwitcher.exe），可双击/固定到快速访问
# 2. ResSwitcher-Build.lnk → 运行发布脚本 build-release.ps1
# 快捷方式丢失或损坏后可重新运行本脚本恢复。

$ws = $PSScriptRoot

# --- 程序快捷方式 ---
$appLnk = Join-Path $ws "ResSwitcher.lnk"
if (Test-Path $appLnk) {
    Write-Host "程序快捷方式已存在，跳过：$appLnk" -ForegroundColor Yellow
} else {
    $sc = (New-Object -ComObject WScript.Shell).CreateShortcut($appLnk)
    $sc.TargetPath = (Join-Path $ws "dist\ResSwitcher.exe")
    $sc.WorkingDirectory = (Join-Path $ws "dist")
    $sc.Description = "ResSwitcher 分辨率切换悬浮工具"
    $sc.WindowStyle = 7   # 最小化启动（程序本身无窗口，此设置无副作用）
    $sc.Save()
    Write-Host "已创建：$appLnk" -ForegroundColor Green
}

# --- 发布脚本快捷方式 ---
$buildLnk = Join-Path $ws "ResSwitcher-Build.lnk"
if (Test-Path $buildLnk) {
    Write-Host "发布快捷方式已存在，跳过：$buildLnk" -ForegroundColor Yellow
    exit 0
}

$sc = (New-Object -ComObject WScript.Shell).CreateShortcut($buildLnk)
$sc.TargetPath = "powershell.exe"
$sc.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$ws\build-release.ps1`""
$sc.WorkingDirectory = $ws
$sc.WindowStyle = 1
$sc.Description = "Build+Test+Publish ResSwitcher"
$sc.Save()

Write-Host "已创建：$buildLnk" -ForegroundColor Green

