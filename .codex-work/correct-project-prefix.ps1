$ErrorActionPreference = 'Stop'
$root = 'D:\GitRepos\ScannerProject'
$tracked = @(git ls-files)
foreach ($pair in @(@('Scan.MaUI','Scanner.MaUI'),@('WmsUploadSite','Scanner.Web'))) {
    $source = Join-Path $root $pair[0]
    $target = Join-Path $root $pair[1]
    if ((Split-Path $source -Parent) -ne $root -or (Test-Path $target)) { throw 'Unexpected rename target' }
    New-Item -ItemType Directory -Path $target | Out-Null
    foreach ($file in $tracked | Where-Object { $_.StartsWith($pair[0]+'/') }) {
        $destination = Join-Path $target $file.Substring($pair[0].Length+1)
        New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
        Move-Item -LiteralPath (Join-Path $root $file) -Destination $destination
    }
    Rename-Item -LiteralPath (Join-Path $target ($pair[0]+'.csproj')) -NewName ($pair[1]+'.csproj')
}
foreach ($file in $tracked) {
    if ($file.StartsWith('.codex-work/') -or $file.StartsWith('outputs/')) { continue }
    $path = Join-Path $root ($file.Replace('Scan.MaUI','Scanner.MaUI').Replace('WmsUploadSite','Scanner.Web'))
    if (!(Test-Path -LiteralPath $path) -or $path -notmatch '\.(cs|xaml|csproj|slnx|ps1|md|json|config|manifest|yml|yaml|props|targets)$') { continue }
    $old = [IO.File]::ReadAllText($path)
    $new = $old.Replace('Scan.MaUI','Scanner.MaUI').Replace('WmsUploadSite','Scanner.Web')
    if ($new -ne $old) { [IO.File]::WriteAllText($path,$new,[Text.UTF8Encoding]::new($false)) }
}
