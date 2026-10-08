param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot 'bin' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$refs = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Security.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $output 'Linling.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),('/win32icon:' + (Join-Path $PSScriptRoot 'qingling.ico')),('/resource:' + (Join-Path $PSScriptRoot 'character05.png') + ',qingling.png'),('/resource:' + (Join-Path $PSScriptRoot 'qingling.ico') + ',qingling.ico'))
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'drag.png') + ',drag.png'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'edges.png') + ',edges.png'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'motion24.png') + ',motion24.png'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'gaze-breath-v10.png') + ',gaze-breath-v10.png'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'sleep24.png') + ',sleep24.png'
$arguments += $refs | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$arguments += (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs').FullName
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup><runtime><AppContextSwitchOverrides value="Switch.System.Net.DontEnableSystemDefaultTlsVersions=false"/></runtime></configuration>
'@ | Set-Content -LiteralPath (Join-Path $output 'Linling.exe.config') -Encoding utf8
Write-Output (Join-Path $output 'Linling.exe')
