param(
    [string]$ManagedDirectory = $env:CSII_MANAGEDPATH,
    [string]$Compiler = $env:CSC_PATH,
    [switch]$CompileOnly
)
$ErrorActionPreference = 'Stop'
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) { throw 'Pass -Compiler with the path to a .NET Framework-compatible Roslyn csc.exe (C# 9 or later), or set CSC_PATH.' }
$build = Join-Path $PSScriptRoot 'build'
$raw = Join-Path $build 'raw'
New-Item -ItemType Directory -Force -Path $raw | Out-Null
$refs = @('mscorlib','System','System.Core','netstandard','System.Memory','Game','Colossal.Core','Colossal.IO.AssetDatabase','Colossal.Localization','Colossal.Logging','Colossal.Mathematics','Colossal.Collections','Unity.Entities','Unity.Collections','Unity.Mathematics','Unity.InputSystem','UnityEngine','UnityEngine.CoreModule','UnityEngine.InputLegacyModule','UnityEngine.AnimationModule')
$compileArgs = @('/nologo','/nostdlib+','/target:library','/langversion:9.0','/optimize+','/debug:portable','/deterministic+',('/out:'+(Join-Path $raw 'GPTZoomToCursor.dll')))
foreach($name in $refs) {
    $path = Join-Path $ManagedDirectory ($name+'.dll')
    if(-not (Test-Path -LiteralPath $path)) { throw "Missing assembly: $name" }
    $compileArgs += '/reference:' + $path
}
$compileArgs += @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object Name -ne 'ZoomMathTests.cs' | ForEach-Object FullName)
& $Compiler @compileArgs
if($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
if($CompileOnly) { Write-Output 'Raw compile passed; post-processing has not run.'; return }
$package = Join-Path $build 'package'
New-Item -ItemType Directory -Force -Path $package | Out-Null
Copy-Item -LiteralPath (Join-Path $raw 'GPTZoomToCursor.dll') -Destination $package
Copy-Item -LiteralPath (Join-Path $raw 'GPTZoomToCursor.pdb') -Destination $package
$postArgs = @('PostProcess',(Join-Path $package 'GPTZoomToCursor.dll'),'-u',$env:CSII_UNITYMODPROJECTPATH)
foreach($name in $refs) { $postArgs += @('-r',(Join-Path $ManagedDirectory ($name+'.dll'))) }
$postArgs += @('-p','Windows','-d','-v')
& $env:CSII_MODPOSTPROCESSORPATH @postArgs *> (Join-Path $build 'postprocess.log')
if($LASTEXITCODE -ne 0) { throw 'Official post-processing failed; see build/postprocess.log.' }
$manifest = [ordered]@{
    name = "GPT's Zoom to Cursor by X_Bean_X"
    version = '0.3.0'
    author = 'X_Bean_X'
    defaults = @{ EnableMod = $true; ZoomOutFromCursor = $false; EnableAutomaticCameraTilt = $false }
    gameAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $ManagedDirectory 'Game.dll')).Hash
    packageFiles = @(Get-ChildItem -LiteralPath $package -File | ForEach-Object { @{ name=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash; bytes=$_.Length } })
    builtUtc = [DateTime]::UtcNow.ToString('o')
    runtimeValidation = 'pending'
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $build 'build-manifest.json')
Write-Output 'Build and official post-processing passed. Local package only; not installed or published.'
