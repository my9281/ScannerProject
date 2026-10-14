param([Parameter(Mandatory=$true)][string]$JobPath)
$ErrorActionPreference = 'Stop'
$job = Get-Content -LiteralPath $JobPath -Raw | ConvertFrom-Json
$stage = Split-Path -Parent $JobPath
$target = [IO.Path]::GetFullPath($job.Target).TrimEnd('\')
$payload = [IO.Path]::GetFullPath($job.Payload).TrimEnd('\')
$backup = Join-Path $stage 'backup'
$logDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Scanner\Updates'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$log = Join-Path $logDirectory 'update.log'
$changed = [Collections.Generic.List[object]]::new()
$success = $false
$rollbackComplete = $true
try {
    $running = Get-Process -Id $job.ProcessId -ErrorAction SilentlyContinue
    Set-Content -LiteralPath (Join-Path $stage 'ready') -Value 'ready'
    if ($running -and !$running.WaitForExit(60000)) { throw 'Application did not exit within 60 seconds.' }
    # A second instance could still hold files. Abort and retain the old application.
    if (Get-Process -Name 'Scanner.WPF' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $job.Executable }) {
        throw 'Another Scanner.WPF instance is running.'
    }
    foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File) {
        $relative = $file.FullName.Substring($payload.Length + 1)
        $destination = [IO.Path]::GetFullPath((Join-Path $target $relative))
        if (!$destination.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid target path.' }
        if ($relative -in @('Scanner.WPF.dll.config', 'Scanner.WPF.exe.config') -and (Test-Path -LiteralPath $destination)) { continue }
        # Do not follow a junction/symlink in an existing installation.
        $parent = $destination
        while ($parent -and $parent.Length -ge $target.Length) {
            if ((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Update target contains a reparse point.'
            }
            $parent = Split-Path -Parent $parent
        }
        $saved = Join-Path $backup $relative
        $existed = Test-Path -LiteralPath $destination
        if ($existed) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $saved
        }
        $changed.Add([pscustomobject]@{ Destination=$destination; Saved=$saved; Existed=$existed })
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
    $success = $true
    Add-Content -LiteralPath $log -Value "$(Get-Date -Format o) Update installed."
} catch {
    Add-Content -LiteralPath $log -Value "$(Get-Date -Format o) Update failed: $_"
    for ($index = $changed.Count - 1; $index -ge 0; $index--) {
        $item = $changed[$index]
        try {
            if ($item.Existed) { Copy-Item -LiteralPath $item.Saved -Destination $item.Destination -Force }
            elseif (Test-Path -LiteralPath $item.Destination) { Remove-Item -LiteralPath $item.Destination -Force }
        } catch {
            $rollbackComplete = $false
            Add-Content -LiteralPath $log -Value "Rollback failed; backup retained at ${backup}: $_"
        }
    }
}
if ($job.Restart -and ($success -or $rollbackComplete)) {
    try { Start-Process -FilePath $job.Executable -ArgumentList '--updated' -WorkingDirectory $target -WindowStyle Hidden }
    catch { Add-Content -LiteralPath $log -Value "Restart failed: $_"; $success = $false }
}
# Only delete this helper's temporary staging directory, never the installation.
if ($success) {
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'Scanner.WPF.Update')) + '\'
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if ($resolvedStage.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -and
        $resolvedStage -ne $target -and $resolvedStage -ne $payload) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
if (!$success) { exit 1 }
