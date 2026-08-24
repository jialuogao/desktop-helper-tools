# ResSwitcher 发布脚本：构建 + 测试 + 发布单文件 exe
# 用法：双击根目录的 ResSwitcher-Build.lnk 快捷方式，或在终端执行 .\build-release.ps1
# 产物：dist\ResSwitcher.exe（每次覆盖）
# 说明：本脚本幂等——重复运行安全；快捷方式已存在时无需重建。

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "==> [1/3] 构建 Release..." -ForegroundColor Cyan
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { Write-Host "构建失败" -ForegroundColor Red; exit 1 }

Write-Host "==> [2/3] 运行测试..." -ForegroundColor Cyan
dotnet test tests/ResSwitcher.Tests -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "测试未全部通过，停止发布" -ForegroundColor Red; exit 1 }

Write-Host "==> [3/3] 发布单文件 exe..." -ForegroundColor Cyan
# 若旧 exe 正在运行则先结束，避免文件锁定
Get-Process ResSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

dotnet publish src/ResSwitcher -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:SelfContained=false `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist
if ($LASTEXITCODE -ne 0) { Write-Host "发布失败" -ForegroundColor Red; exit 1 }

$exe = Join-Path $PSScriptRoot "dist\ResSwitcher.exe"
$sizeKB = [int]((Get-Item $exe).Length / 1KB)
Write-Host ""
Write-Host "完成！产物：$exe ($sizeKB KB)" -ForegroundColor Green
