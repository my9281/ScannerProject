$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('ScannerUpdateTests-' + [Guid]::NewGuid().ToString('N'))
$script = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Scanner.WPF\Services\ApplyUpdate.ps1'))
function Run-Job($case, $locked) {
    $stage = Join-Path $root $case
    $target = Join-Path $stage 'target'
    $payload = Join-Path $stage 'payload'
    New-Item -ItemType Directory -Path $target,$payload -Force | Out-Null
    foreach ($name in @('a.dll', 'b.dll', 'Scanner.WPF.dll.config')) {
        [IO.File]::WriteAllText((Join-Path $target $name), 'old')
        [IO.File]::WriteAllText((Join-Path $payload $name), 'new')
    }
    $job = Join-Path $stage 'job.json'
    @{ ProcessId=2147483647; Target=$target; Payload=$payload; Executable=(Join-Path $target 'Scanner.WPF.exe'); Restart=$false } | ConvertTo-Json | Set-Content -LiteralPath $job
    $lock = $null
    try {
        if ($locked) { $lock = [IO.File]::Open((Join-Path $target 'b.dll'), 'Open', 'Read', 'Read') }
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $script -JobPath $job
        $code = $LASTEXITCODE
    } finally { if ($lock) { $lock.Dispose() } }
    $expected = if ($locked) { 'old' } else { 'new' }
    foreach ($name in @('a.dll', 'b.dll')) {
        if ([IO.File]::ReadAllText((Join-Path $target $name)) -ne $expected) { throw "$case $name content incorrect" }
    }
    if ([IO.File]::ReadAllText((Join-Path $target 'Scanner.WPF.dll.config')) -ne 'old') { throw 'Local configuration was overwritten' }
    if (($locked -and $code -ne 1) -or (!$locked -and $code -ne 0)) { throw "Unexpected helper exit: $code" }
}
try {
    Run-Job 'success' $false
    Run-Job 'rollback' $true
    Write-Output 'PASS: apply, preserve configuration, locked-file failure and rollback.'
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $allowed = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
