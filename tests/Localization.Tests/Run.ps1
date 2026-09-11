$ErrorActionPreference = 'Stop'
$repoPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$msbuildPath = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin'
& "$msbuildPath/MSBuild.exe" "$repoPath/Scanner.WPF/Scanner.WPF.csproj" /t:Build /p:Configuration=Release /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Scanner build failed' }
$frameworkPath = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2'
$references = @('PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Xaml', 'System.Xml.Linq') | ForEach-Object { '/reference:' + $frameworkPath + '/' + $_ + '.dll' }
& "$msbuildPath/Roslyn/csc.exe" /nologo /target:exe "/out:$repoPath/Scanner.WPF/bin/Release/VerifyLocalization.exe" "/reference:$repoPath/Scanner.WPF/bin/Release/Scanner.WPF.exe" $references "$PSScriptRoot/VerifyLocalization.cs"
if ($LASTEXITCODE -ne 0) { throw 'Localization test compilation failed' }
& "$repoPath/Scanner.WPF/bin/Release/VerifyLocalization.exe" $repoPath "$PSScriptRoot/bin/previews"
if ($LASTEXITCODE -ne 0) { throw 'Localization verification failed' }
