param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\wpf'),
    [string]$UpdateBaseUrl = 'https://wms.ymforever.com/updates/wpf/packages/',
    [switch]$SelfContained
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$baseUri = [Uri]$UpdateBaseUrl
if (!$baseUri.IsAbsoluteUri -or $baseUri.Scheme -ne 'https' -or $baseUri.UserInfo) { throw 'UpdateBaseUrl must be an absolute HTTPS URL.' }
# A new output folder prevents old dependencies from leaking into a release.
$release = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
$publish = Join-Path $release 'publish'
New-Item -ItemType Directory -Path $publish -Force | Out-Null
dotnet publish (Join-Path $repo 'Scanner.WPF\Scanner.WPF.csproj') -m:1 -p:PublishProfile=Lean --self-contained $SelfContained.IsPresent.ToString().ToLowerInvariant() -o $publish
if ($LASTEXITCODE -ne 0) { throw 'WPF publish failed.' }
$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $publish 'Scanner.WPF.dll')).Version.ToString()
$packageName = "Scanner.WPF-$version-win-x64.zip"
$zip = Join-Path $release $packageName
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$manifest = [ordered]@{
    version = $version
    packageUrl = ([Uri]::new([Uri]($UpdateBaseUrl.TrimEnd('/') + '/'), $packageName)).AbsoluteUri
    sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
}
$json = $manifest | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $release 'latest.json'), $json, [Text.UTF8Encoding]::new($false))
$files = Get-ChildItem -LiteralPath $publish -Recurse -File
[pscustomobject]@{ Version=$version; Files=$files.Count; PublishMB=[Math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 2); ZipMB=[Math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 2); Output=$release }
