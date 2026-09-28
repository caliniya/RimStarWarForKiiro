# RimKiior 打包部署脚本
# 用法:
#   powershell -ExecutionPolicy Bypass -File build.ps1          # 部署到游戏 Mods 目录
#   powershell -ExecutionPolicy Bypass -File build.ps1 -Zip     # 同时在 dist/ 下生成 zip 分发包
param(
    [switch]$Zip
)

$ModName     = "RimKiior"
$ProjectRoot = $PSScriptRoot
$GameModsDir = "E:\app\rimworld\RimWorld16\Mods"

# 不应进入模组成品的开发文件
$ExcludeDirs  = @(".git", ".vs", "obj", "bin", "dist", ".vscode")
$ExcludeFiles = @("build.ps1", "build.bat", "DESIGN.md", ".gitignore")

# ---- 1. 打包到干净的暂存目录 ----
$Staging = Join-Path $env:TEMP "$ModName-package"
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
New-Item $Staging -ItemType Directory -Force | Out-Null

$excludeDirArgs  = $ExcludeDirs  | ForEach-Object { "/XD"; Join-Path $ProjectRoot $_ }
$excludeFileArgs = $ExcludeFiles | ForEach-Object { "/XF"; Join-Path $ProjectRoot $_ }

robocopy $ProjectRoot $Staging /MIR /NFL /NDL /NJH /NJS $excludeDirArgs $excludeFileArgs | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Error "打包失败 (robocopy 代码 $LASTEXITCODE)"; exit 1 }

# ---- 2. 部署到游戏 Mods 目录 ----
$Target = Join-Path $GameModsDir $ModName
robocopy $Staging $Target /MIR /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Error "部署失败 (robocopy 代码 $LASTEXITCODE)"; exit 1 }
Write-Host "已部署到 $Target"

# ---- 3. 可选:生成 zip 分发包 ----
if ($Zip) {
    $DistDir = Join-Path $ProjectRoot "dist"
    New-Item $DistDir -ItemType Directory -Force | Out-Null
    $ZipPath = Join-Path $DistDir "$ModName-$(Get-Date -Format 'yyyyMMdd-HHmm').zip"
    Compress-Archive -Path $Staging -DestinationPath $ZipPath -Force
    Write-Host "已生成分发包 $ZipPath"
}

exit 0
