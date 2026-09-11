$ErrorActionPreference = 'Stop'
$repoPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$msbuildPath = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin'
& "$msbuildPath/MSBuild.exe" "$repoPath/Scanner/Scanner.csproj" /t:Build /p:Configuration=Release /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Scanner build failed' }
$frameworkPath = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2'
$references = @('System', 'System.Core', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'System.Xml.Linq') | ForEach-Object { '/reference:' + $frameworkPath + '/' + $_ + '.dll' }
& "$msbuildPath/Roslyn/csc.exe" /nologo /target:exe "/out:$repoPath/Scanner/bin/Release/VerifyDailyReport.exe" "/reference:$repoPath/Scanner/bin/Release/Scanner.exe" $references "$PSScriptRoot/Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'Daily report test compilation failed' }
& "$repoPath/Scanner/bin/Release/VerifyDailyReport.exe"
if ($LASTEXITCODE -ne 0) { throw 'Daily report test failed' }
