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
$artFolder = Join-Path $PSScriptRoot 'assets\exercise-art'
$art = @(Get-ChildItem -LiteralPath $artFolder -File -Filter '*.png' | Where-Object { $_.Name -cmatch '^[a-z0-9-]+-[123]\.png$' } | ForEach-Object {
    "/resource:$($_.FullName),exercise-art.$($_.Name)"
})
foreach ($notice in 'Attribution.txt', 'LICENSE.txt') {
    $art += "/resource:$(Join-Path $artFolder $notice),exercise-art.$notice"
}
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /optimize+ /codepage:65001 `
    "/out:$OutputPath" "/win32icon:$icon" "/resource:$badge,GymZ.png" `
    "/resource:$catalog,Exercises.txt" "/resource:$markup,Main.xaml" $art `
    "/reference:$wpf\PresentationFramework.dll" "/reference:$wpf\PresentationCore.dll" `
    "/reference:$wpf\WindowsBase.dll" "/reference:$wpf\UIAutomationProvider.dll" "/reference:$wpf\UIAutomationTypes.dll" `
    /reference:System.Xaml.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll `
    /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw "A compilação falhou ($LASTEXITCODE)." }
