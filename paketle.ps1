param(
    [string]$Surum
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
foreach ($name in @('hareket_yayini.py', 'kamera_test.py', 'requirements.txt', 'kurulum.bat')) {
    $source = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Gerekli kopru dosyasi bulunamadi: $source" }
    Copy-Item -LiteralPath $source -Destination $bridge
}
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

