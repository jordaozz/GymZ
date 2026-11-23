param([string]$OutputPath = (Join-Path $PSScriptRoot 'GymZ.exe'))

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$wpf = Join-Path $framework 'WPF'
$icon = Join-Path $PSScriptRoot 'assets\GymZ.ico'
$badge = Join-Path $PSScriptRoot 'assets\GymZ.png'
$catalog = Join-Path $PSScriptRoot 'Exercises.txt'
$markup = Join-Path $PSScriptRoot 'Main.xaml'
$art = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'assets\exercise-art') -File | ForEach-Object {
    "/resource:$($_.FullName),exercise-art.$($_.Name)"
})
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /optimize+ /codepage:65001 `
    "/out:$OutputPath" "/win32icon:$icon" "/resource:$badge,GymZ.png" `
    "/resource:$catalog,Exercises.txt" "/resource:$markup,Main.xaml" $art `
    "/reference:$wpf\PresentationFramework.dll" "/reference:$wpf\PresentationCore.dll" `
    "/reference:$wpf\WindowsBase.dll" "/reference:$wpf\UIAutomationProvider.dll" "/reference:$wpf\UIAutomationTypes.dll" `
    /reference:System.Xaml.dll /reference:System.Runtime.Serialization.dll `
    /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw "A compilação falhou ($LASTEXITCODE)." }
