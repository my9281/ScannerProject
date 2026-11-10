[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Render','Print')][string]$Action,
    [Parameter(Mandatory)][string]$BuildDirectory,
    [string]$SourcePdf,
    [string]$OutputPrefix,
    [string]$Rma,
    [string]$Model,
    [string]$Serial,
    [string]$ImagePath,
    [string]$PrinterName,
    [string]$JobName,
    [string]$DotNet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $skillRoot 'assets/label-tool/OmsLabelCli.csproj'
$buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
if ($Action -eq 'Render') {
    if (!(Test-Path -LiteralPath $SourcePdf -PathType Leaf) -or !$OutputPrefix -or !$Rma -or !$Model -or !$Serial) { throw 'Render requires SourcePdf, OutputPrefix, Rma, Model and Serial.' }
    $prefix = [IO.Path]::GetFullPath($OutputPrefix)
    if (!(Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($prefix)) -PathType Container)) { throw 'Create the output directory first.' }
} else {
    if (!(Test-Path -LiteralPath $ImagePath -PathType Leaf) -or !$PrinterName -or !$JobName) { throw 'Print requires ImagePath, PrinterName and JobName.' }
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDUSESERVER = '0'
$objRoot = Join-Path $buildRoot 'obj/'
$binRoot = Join-Path $buildRoot 'bin/'
& $DotNet build $project --disable-build-servers --ignore-failed-sources -m:1 "-p:BaseIntermediateOutputPath=$objRoot" "-p:OutputPath=$binRoot" -p:NuGetAudit=false -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Label tool build failed.' }
$dll = Join-Path $binRoot 'OmsLabelCli.dll'
if ($Action -eq 'Render') {
    & $DotNet $dll render ([IO.Path]::GetFullPath($SourcePdf)) $prefix $Rma $Model $Serial
} else {
    & $DotNet $dll print ([IO.Path]::GetFullPath($ImagePath)) $PrinterName $JobName
}
if ($LASTEXITCODE -ne 0) { throw 'Label operation failed. Do not retry a print without checking the receipt and queue.' }
