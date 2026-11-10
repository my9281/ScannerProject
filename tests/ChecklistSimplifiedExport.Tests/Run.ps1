$ErrorActionPreference = 'Stop'
$repoPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$msbuildPath = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin'
& "$msbuildPath/MSBuild.exe" "$repoPath/Scanner.WPF/Scanner.WPF.csproj" /t:Build /p:Configuration=Release /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Scanner build failed' }
$frameworkPath = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2'
$references = @('System', 'System.Core') | ForEach-Object { '/reference:' + $frameworkPath + '/' + $_ + '.dll' }
& "$msbuildPath/Roslyn/csc.exe" /nologo /target:exe "/out:$repoPath/Scanner.WPF/bin/Release/VerifyChecklistSimplifiedExport.exe" "/reference:$repoPath/Scanner.WPF/bin/Release/Scanner.WPF.exe" $references "$PSScriptRoot/Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'Simplified export test compilation failed' }
& "$repoPath/Scanner.WPF/bin/Release/VerifyChecklistSimplifiedExport.exe"
if ($LASTEXITCODE -ne 0) { throw 'Simplified export test failed' }
