$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$issues = @()
$count = 0
[xml]$solution = Get-Content (Join-Path $root 'Scanner.slnx')
foreach ($project in $solution.Solution.Project) {
    $projectPath = Join-Path $root $project.Path
    if (!(Test-Path $projectPath)) { $issues += "Missing project: $projectPath"; continue }
    $directory = Split-Path $projectPath -Parent
    $projectName = [IO.Path]::GetFileNameWithoutExtension($projectPath)
    [xml]$xml = Get-Content $projectPath
    foreach ($reference in $xml.SelectNodes("//*[local-name()='ProjectReference']")) {
        if (!(Test-Path (Join-Path $directory $reference.Include))) { $issues += "Missing reference: $projectName / $($reference.Include)" }
    }
    foreach ($compile in $xml.SelectNodes("//*[local-name()='Compile']")) {
        if ($compile.Include -match '^\.\.') { $issues += "External source compile: $projectName / $($compile.Include)" }
    }
    foreach ($file in Get-ChildItem $directory -Recurse -Filter *.cs -File) {
        if ($file.FullName -match '\\(bin|obj|packages|\.vs)\\') { continue }
        $count++
        $relative = $file.FullName.Substring($directory.Length+1)
        $folder = Split-Path $relative -Parent
        $expected = $projectName
        if ($folder) { $expected += '.' + $folder.Replace('\','.') }
        if ($projectName -eq 'Scan.MaUI' -and $relative -like 'Platforms\*') {
            $expected = if ($relative -like 'Platforms\Windows\*') { 'Scan.MaUI.WinUI' } else { 'Scan.MaUI' }
        }
        $text = Get-Content $file.FullName -Raw
        $namespaces = [regex]::Matches($text,'(?m)^\s*namespace\s+([\w.]+)')
        if (!$namespaces.Count -and $relative -ne 'Program.cs' -and $file.Name -ne 'AssemblyInfo.cs') { $issues += "Missing namespace: $projectName/$relative" }
        foreach ($ns in $namespaces) { if ($ns.Groups[1].Value -ne $expected) { $issues += "$projectName/$relative : $($ns.Groups[1].Value) expected $expected" } }
    }
}
if ($issues.Count) { $issues | Write-Output; exit 1 }
Write-Output "PASS: $count C# files; project references, namespace ownership and no external source compilation."
