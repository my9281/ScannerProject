$ErrorActionPreference = 'Stop'
$sdk = 'C:\Program Files (x86)\Android\android-sdk'
$jdk = 'C:\Program Files\Android\openjdk\jdk-21.0.8'
$bt = Join-Path $sdk 'build-tools\36.0.0'
$androidJar = Join-Path $sdk 'platforms\android-36\android.jar'
$build = Join-Path $PSScriptRoot 'build'
$signing = Join-Path $PSScriptRoot 'signing'
$output = Join-Path $PSScriptRoot '..\output\NullKeyboard'
$env:JAVA_HOME = $jdk
$env:PATH = "$jdk\bin;$env:PATH"
New-Item -ItemType Directory -Force -Path $build,"$build\classes","$build\dex",$signing,$output | Out-Null
function Run([string] $exe, [string[]] $arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "Build tool failed: $exe ($LASTEXITCODE)" }
}
Run "$bt\aapt2.exe" @('compile','--dir',"$PSScriptRoot\res",'-o',"$build\resources.zip")
Run "$bt\aapt2.exe" @('link','-o',"$build\unsigned.apk",'-I',$androidJar,'--manifest',"$PSScriptRoot\AndroidManifest.xml","$build\resources.zip")
$sources = @(Get-ChildItem "$PSScriptRoot\src" -Recurse -Filter '*.java' | ForEach-Object FullName)
Run "$jdk\bin\javac.exe" (@('-encoding','UTF-8','--release','8','-classpath',$androidJar,'-d',"$build\classes") + $sources)
$classes = @(Get-ChildItem "$build\classes" -Recurse -Filter '*.class' | ForEach-Object FullName)
Run "$bt\d8.bat" (@('--lib',$androidJar,'--min-api','24','--output',"$build\dex") + $classes)
Push-Location "$build\dex"
try { Run "$jdk\bin\jar.exe" @('uf',"$build\unsigned.apk",'classes.dex') } finally { Pop-Location }
Run "$bt\zipalign.exe" @('-f','4',"$build\unsigned.apk","$build\aligned.apk")
$key = Join-Path $signing 'nullkeyboard.jks'
# Local distribution signing key. Keep this file for future APK updates.
if (-not (Test-Path $key)) {
    Run "$jdk\bin\keytool.exe" @('-genkeypair','-keystore',$key,'-storepass','nullkeyboard-local','-keypass','nullkeyboard-local','-alias','nullkeyboard','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=Scanner Null Keyboard')
}
$apk = Join-Path $output 'NullKeyboard-1.1.apk'
Run "$bt\apksigner.bat" @('sign','--ks',$key,'--ks-key-alias','nullkeyboard','--ks-pass','pass:nullkeyboard-local','--out',$apk,"$build\aligned.apk")
Run "$bt\apksigner.bat" @('verify','--verbose',$apk)
Run "$bt\aapt2.exe" @('dump','badging',$apk)
Write-Output "APK: $apk"
