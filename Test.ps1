param([string]$Compiler = $env:CSC_PATH)
$ErrorActionPreference = 'Stop'
$bin=Join-Path $PSScriptRoot 'tests\bin'
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$argsList=@('/nologo','/nostdlib+','/target:exe','/langversion:9.0',('/out:'+(Join-Path $bin 'ZoomMathTests.exe')))
foreach($name in @('mscorlib','System','System.Core','netstandard','Unity.Mathematics')) { $argsList += '/reference:'+(Join-Path $env:CSII_MANAGEDPATH ($name+'.dll')) }
$argsList += @((Join-Path $PSScriptRoot 'ZoomMath.cs'),(Join-Path $PSScriptRoot 'TiltMotion.cs'),(Join-Path $PSScriptRoot 'ZoomMathTests.cs'))
& $Compiler @argsList
if($LASTEXITCODE -ne 0) {throw 'Test compilation failed'}
Copy-Item -LiteralPath (Join-Path $env:CSII_MANAGEDPATH 'Unity.Mathematics.dll') -Destination $bin
& (Join-Path $bin 'ZoomMathTests.exe')
if($LASTEXITCODE -ne 0) {throw 'Geometry tests failed'}
