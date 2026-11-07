# =============================================================
#  Dong goi DCD_WatchNoAds
#     powershell -ExecutionPolicy Bypass -File build.ps1            # build + cai vao may nay
#     powershell -ExecutionPolicy Bypass -File build.ps1 -NoInstall # chi build
#
#  Ra MOT file dist\DCD_WatchNoAds.exe tu chua .NET runtime. File nay vua la
#  app vua la bo cai: gui cho nguoi khac, ho nhay dup la hien bang Cai dat.
# =============================================================
param([switch]$NoInstall)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src\DCD_WatchNoAds\DCD_WatchNoAds.csproj'
$dist = Join-Path $root 'dist'
$name = 'DCD_WatchNoAds'

function Say([string]$m) { Write-Host $m -ForegroundColor Cyan }
function Ok ([string]$m) { Write-Host ('  OK  ' + $m) -ForegroundColor Green }

Say 'Build DCD_WatchNoAds (ban tu chua .NET runtime)...'
$out = Join-Path $env:TEMP ('dcdw_' + [guid]::NewGuid().ToString('N').Substring(0, 8))
& dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o $out --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build that bai' }

New-Item -ItemType Directory -Path $dist -Force | Out-Null
$exe = Join-Path $dist "$name.exe"
Copy-Item (Join-Path $out "$name.exe") $exe -Force
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
Ok ("$exe  " + [math]::Round((Get-Item $exe).Length / 1MB, 1) + ' MB')

if ($NoInstall) { return }

# chinh exe tu cai: chep vao %LOCALAPPDATA%\Programs, tao shortcut, ghi muc go cai dat
Say 'Cai vao may...'
$p = Start-Process $exe -ArgumentList '--install' -PassThru
$p.WaitForExit()
Ok (Join-Path $env:LOCALAPPDATA "Programs\$name\$name.exe")
