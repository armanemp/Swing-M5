[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "Medium")]
param(
    [string]$IndicatorsDirectory = (Join-Path $env:USERPROFILE "Documents\cAlgo\Sources\Indicators")
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceFile = Join-Path $repoRoot "src\SwingM5\SwingM5.cs"
$targetDirectory = Join-Path $IndicatorsDirectory "SwingM5"
$targetFile = Join-Path $targetDirectory "SwingM5.cs"

if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
    throw "Swing-M5 source file was not found: $sourceFile"
}

if ($PSCmdlet.ShouldProcess($targetFile, "Copy Swing-M5 into cTrader's local Indicators source folder")) {
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null

    if (Test-Path -LiteralPath $targetFile -PathType Leaf) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupFile = "$targetFile.bak-$stamp"
        Copy-Item -LiteralPath $targetFile -Destination $backupFile
        Write-Host "Existing source backed up to: $backupFile" -ForegroundColor Yellow
    }

    Copy-Item -LiteralPath $sourceFile -Destination $targetFile -Force
    Write-Host ""
    Write-Host "Swing-M5 source copied to:" -ForegroundColor Green
    Write-Host "  $targetFile"
    Write-Host ""
    Write-Host "Next steps in cTrader Desktop:" -ForegroundColor Cyan
    Write-Host "  1. Open Algo -> Indicators."
    Write-Host "  2. Select or refresh SwingM5."
    Write-Host "  3. In Settings -> Algo, select Embedded compiler if you want to avoid NuGet."
    Write-Host "  4. Click Build and confirm a successful compile in the cTrader log."
    Write-Host ""
    Write-Host "This script copies source only; it does not claim that cTrader has compiled it."
}
