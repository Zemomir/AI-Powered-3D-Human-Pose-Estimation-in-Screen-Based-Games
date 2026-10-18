param(
    [string]$Surum,
    [string]$Python
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'oyun_derleme'
$exe = Join-Path $build 'FizyoOyun.exe'
$distRoot = Join-Path $root 'dist'
$stage = Join-Path $distRoot 'FizyoOyun'
$bridge = Join-Path $stage 'kopru'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Oyun derlemesi bulunamadi: $exe" }
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
New-Item -ItemType Directory -Path $bridge -Force | Out-Null
function Copy-GameContent([string]$Source, [string]$Destination) {
    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        if ($item.PSIsContainer) {
            if ($item.Name -in @('.venv', 'olcumler', '__pycache__') -or
                $item.Name -like '*_BurstDebugInformation_DoNotShip') { continue }
            $target = Join-Path $Destination $item.Name
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            Copy-GameContent $item.FullName $target
        } elseif ($item.Extension -ne '.log') {
            Copy-Item -LiteralPath $item.FullName -Destination $Destination -Force
        }
    }
}
Copy-GameContent $build $stage
foreach ($name in @('hareket_yayini.py', 'kamera_test.py', 'requirements.txt')) {
    $source = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Gerekli kopru dosyasi bulunamadi: $source" }
    Copy-Item -LiteralPath $source -Destination $bridge
}
$venv = Join-Path $root '.venv'
$sitePackages = Join-Path $venv 'Lib\site-packages'
if (-not (Test-Path -LiteralPath $venv -PathType Container)) { throw "Gelistirme .venv klasoru bulunamadi: $venv" }
if (-not (Test-Path -LiteralPath $sitePackages -PathType Container)) { throw "Gerekli site-packages klasoru bulunamadi: $sitePackages" }
if ([string]::IsNullOrWhiteSpace($Python)) {
    $cfg = Join-Path $venv 'pyvenv.cfg'
    if (-not (Test-Path -LiteralPath $cfg -PathType Leaf)) { throw "Python belirtilmedi ve pyvenv.cfg bulunamadi: $cfg" }
    $homeLine = Get-Content -LiteralPath $cfg | Where-Object { $_ -match '^\s*home\s*=' } | Select-Object -First 1
    if (-not $homeLine) { throw "pyvenv.cfg icinde home satiri yok: $cfg" }
    $Python = ($homeLine -replace '^\s*home\s*=\s*', '').Trim().Trim('"')
}
$Python = (Resolve-Path -LiteralPath $Python).ProviderPath.TrimEnd('\')
$pythonLib = Join-Path $Python 'Lib'
$pythonDlls = Join-Path $Python 'DLLs'
if (-not (Test-Path -LiteralPath (Join-Path $Python 'python.exe') -PathType Leaf)) { throw "Tasinabilir Python bulunamadi: $Python" }
$bundledPython = Join-Path $bridge 'python'
New-Item -ItemType Directory -Path $bundledPython -Force | Out-Null
function Copy-RuntimeTree([string]$Source, [string]$Destination, [string]$Mode = 'runtime') {
    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        if ($item.PSIsContainer) {
            if ($item.Name -eq '__pycache__') { continue }
            if ($Mode -eq 'runtime' -and $Source -eq $Python -and $item.Name -in @('tcl','include','libs','Scripts')) { continue }
            if ($Mode -eq 'runtime' -and $Source -eq $pythonLib -and $item.Name -in @('test','idlelib','tkinter','turtledemo','ensurepip')) { continue }
            if ($Mode -eq 'packages' -and ($item.Name -eq 'pip' -or $item.Name -like 'pip-*.dist-info')) { continue }
            $target = Join-Path $Destination $item.Name
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            Copy-RuntimeTree $item.FullName $target $Mode
        } elseif ($Mode -eq 'runtime' -and $Source -eq $pythonDlls -and
            ($item.Name -in @('_tkinter.pyd','tcl86t.dll','tk86t.dll') -or $item.Name -like '_test*.pyd')) { continue
        } elseif ($item.Extension -ne '.pyc') { Copy-Item -LiteralPath $item.FullName -Destination $Destination -Force }
    }
}
Copy-RuntimeTree $Python $bundledPython
$bundledSite = Join-Path $bundledPython 'Lib\site-packages'
New-Item -ItemType Directory -Path $bundledSite -Force | Out-Null
Copy-RuntimeTree $sitePackages $bundledSite 'packages'
if (-not (Test-Path -LiteralPath (Join-Path $bundledPython 'python.exe') -PathType Leaf)) { throw 'Paket python.exe kopyalanamadi.' }
if (-not (Test-Path -LiteralPath (Join-Path $bundledSite 'mediapipe') -PathType Container)) { throw 'Paket site-packages icinde mediapipe bulunamadi.' }
$model = Join-Path $root 'models\pose_landmarker_full.task'
if (-not (Test-Path -LiteralPath $model -PathType Leaf)) { throw "Poz modeli bulunamadi: $model (once kurulum.bat calistirin)" }
New-Item -ItemType Directory -Path (Join-Path $bridge 'models') -Force | Out-Null
Copy-Item -LiteralPath $model -Destination (Join-Path $bridge 'models')
if ([string]::IsNullOrWhiteSpace($Surum)) { $zipName = 'FizyoOyun-{0}.zip' -f (Get-Date -Format 'yyyyMMdd') }
else { $zipName = 'FizyoOyun-{0}.zip' -f $Surum }
$zipPath = Join-Path $distRoot $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path $stage -DestinationPath $zipPath -Force
$zip = Get-Item -LiteralPath $zipPath
Write-Host "Paket: $($zip.FullName)"
Write-Host ('Boyut: {0:N2} MB' -f ($zip.Length / 1MB))

