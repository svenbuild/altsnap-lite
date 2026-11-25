param([switch]$FrameworkDependent, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskProject = Join-Path $PSScriptRoot 'src\AltSnapLite\AltSnapLite.csproj'
$taskOutput = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot 'dist' }
dotnet publish $taskProject -c Release -r win-x64 --self-contained ($FrameworkDependent.IsPresent -eq $false).ToString().ToLowerInvariant() -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $taskOutput
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
Write-Output "Fertig: $taskOutput\AltSnapLite.exe"
