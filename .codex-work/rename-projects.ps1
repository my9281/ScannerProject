$ErrorActionPreference = 'Stop'
$root = 'D:\GitRepos\ScannerProject'
foreach ($pair in @(@('Scanner','Scanner.WPF'),@('MaUIScanner','Scan.MaUI'))) {
    $source = Join-Path $root $pair[0]
    $target = Join-Path $root $pair[1]
    if ((Split-Path $source -Parent) -ne $root) { throw 'Unexpected rename target' }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($tracked in (& git ls-files -- ($pair[0]+'/'))) {
        $destination = Join-Path $root ($tracked -replace ('^'+$pair[0]+'/'),($pair[1]+'/'))
        New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
        if (Test-Path -LiteralPath (Join-Path $root $tracked)) { Move-Item -LiteralPath (Join-Path $root $tracked) -Destination $destination }
    }
    Rename-Item -LiteralPath (Join-Path $target ($pair[0]+'.csproj')) -NewName ($pair[1]+'.csproj')
}
$files = & git ls-files
foreach ($old in $files) {
    $rel = $old -replace '^Scanner/','Scanner.WPF/' -replace '^MaUIScanner/','Scan.MaUI/'
    $rel = $rel -replace '/Scanner.csproj$','/Scanner.WPF.csproj' -replace '/MaUIScanner.csproj$','/Scan.MaUI.csproj'
    $path = Join-Path $root $rel
    if (!(Test-Path -LiteralPath $path) -or $path -notmatch '\.(cs|xaml|csproj|slnx|ps1|md|json|config|resx|settings|yml|yaml)$') { continue }
    $s = [IO.File]::ReadAllText($path)
    $s = $s.Replace('MaUIScanner.Models','Scanner.Models.MauiContracts').Replace('MaUIScanner','Scan.MaUI')
    $s = $s.Replace('Scanner.PlatformControllers','Scanner.WPF.Controllers').Replace('Scanner.Presentation','Scanner.ViewModels')
    $s = $s.Replace('Scanner.Services','Scanner.WPF.Services')
    if ($rel.StartsWith('Scanner.Helpers/')) { $s = $s.Replace('Scanner.WPF.Services','Scanner.Helpers.Services') }
    if ($rel.StartsWith('Scanner.WPF/')) {
        $s = $s.Replace('Scanner.ViewModels','Scanner.WPF.ViewModels').Replace('Scanner.Helpers','Scanner.WPF.Helpers').Replace('Scanner.Properties','Scanner.WPF.Properties')
        $s = $s -replace 'namespace Scanner(\s|\r|\n|;|\{)', 'namespace Scanner.WPF$1'
        $s = $s.Replace('x:Class="Scanner.','x:Class="Scanner.WPF.').Replace('clr-namespace:Scanner"','clr-namespace:Scanner.WPF"')
        $s = $s.Replace('<RootNamespace>Scanner</RootNamespace>','<RootNamespace>Scanner.WPF</RootNamespace>').Replace('<AssemblyName>Scanner</AssemblyName>','<AssemblyName>Scanner.WPF</AssemblyName>')
        $s = $s.Replace('Scanner.WPF.Helpers.csproj','Scanner.Helpers.csproj').Replace('../Scanner.WPF.Helpers/','../Scanner.Helpers/').Replace('..\Scanner.WPF.Helpers\','..\Scanner.Helpers\')
        $s = $s.Replace('Scanner.WPF.ViewModels.csproj','Scanner.ViewModels.csproj').Replace('../Scanner.WPF.ViewModels/','../Scanner.ViewModels/').Replace('..\Scanner.WPF.ViewModels\','..\Scanner.ViewModels\')
        if ($path.EndsWith('.cs')) { $s = "using Scanner.Helpers;`nusing Scanner.Helpers.Services;`n" + $s }
    }
    if ($rel.StartsWith('Scan.MaUI/') -or $rel.StartsWith('tests/')) { $s = $s.Replace('using Scanner.WPF.Services;','using Scanner.Helpers.Services;') }
    $s = $s.Replace('Scanner/Scanner.csproj','Scanner.WPF/Scanner.WPF.csproj').Replace('Scanner\Scanner.csproj','Scanner.WPF\Scanner.WPF.csproj')
    $s = $s.Replace('Scanner/','Scanner.WPF/').Replace('Scanner\','Scanner.WPF\')
    $s = $s.Replace('Scan.MaUI/MaUIScanner.csproj','Scan.MaUI/Scan.MaUI.csproj')
    [IO.File]::WriteAllText($path,$s,[Text.UTF8Encoding]::new($false))
}
