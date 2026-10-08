param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+$') { throw 'Version must look like 0.7' }
$project = Split-Path -Parent $PSScriptRoot
$target = Join-Path $project ("运行预览\ver" + $Version)
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $target
if ($LASTEXITCODE -ne 0) { throw 'Preview build failed' }
Copy-Item -LiteralPath (Join-Path $project '使用说明.md') -Destination (Join-Path $target '使用说明.md') -Force
Write-Output $target
