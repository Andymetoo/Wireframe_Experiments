param(
    [string]$UnityDataPath = '',
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

# This only invokes the installed .NET C# compiler. It never starts Unity,
# imports assets, executes an Editor callback, or creates a standalone player.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
if ([string]::IsNullOrWhiteSpace($UnityDataPath)) {
    $versionFile = Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt'
    $projectVersion = [regex]::Match([System.IO.File]::ReadAllText($versionFile), '(?m)^m_EditorVersion:\s*(\S+)').Groups[1].Value
    $candidates = @(
        (Join-Path 'G:\UnityInstalls' ($projectVersion + '\Editor\Data')),
        (Join-Path 'C:\Program Files\Unity\Hub\Editor' ($projectVersion + '\Editor\Data')),
        'C:\Program Files\Unity\Hub\Editor\6000.3.5f1\Editor\Data'
    )
    $UnityDataPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($UnityDataPath)) { throw 'No local Unity reference installation found. Pass -UnityDataPath explicitly.' }
}
$UnityDataPath = (Resolve-Path -LiteralPath $UnityDataPath).Path
$sourceRoot = Join-Path $ProjectRoot 'Assets\Experiments\TestAnxiety'
$outputRoot = Join-Path $ProjectRoot 'Library\TestAnxietyValidation'
$engineRoot = Join-Path $UnityDataPath 'Managed\UnityEngine'
$standardReference = Join-Path $UnityDataPath 'NetStandard\ref\2.1.0\netstandard.dll'
$shimRoot = Join-Path $UnityDataPath 'NetStandard\compat\2.1.0\shims\netstandard'
$inputReference = Join-Path $ProjectRoot 'Library\ScriptAssemblies\Unity.InputSystem.dll'

foreach ($required in @($sourceRoot, $engineRoot, $standardReference, $shimRoot, $inputReference)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required local reference is missing: $required" }
}

$sdkLines = @(& dotnet --list-sdks)
if ($LASTEXITCODE -ne 0) { throw 'Cannot query the installed .NET SDK.' }
$sdk = $sdkLines | ForEach-Object {
    if ($_ -match '^([^ ]+) \[(.+)\]$') {
        [pscustomobject]@{ Version = [version]($Matches[1] -replace '-.*$', ''); Name = $Matches[1]; Root = $Matches[2] }
    }
} | Sort-Object Version -Descending | Select-Object -First 1
if ($null -eq $sdk) { throw 'An installed .NET SDK is required; this script does not download one.' }
$compiler = Join-Path (Join-Path $sdk.Root $sdk.Name) 'Roslyn\bincore\csc.dll'
if (-not (Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler" }

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$engineReferences = @(Get-ChildItem -LiteralPath $engineRoot -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
$standardReferences = @($standardReference) + @(Get-ChildItem -LiteralPath $shimRoot -Filter '*.dll' | ForEach-Object FullName)
$runtimeReferences = $standardReferences + $engineReferences + @($inputReference)
$runtimeSources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]|[\\/]Tests[\\/]' } | ForEach-Object FullName)
$editorSources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -Recurse |
    Where-Object { $_.FullName -match '[\\/]Editor[\\/]' -and $_.FullName -notmatch '[\\/]Tests[\\/]' } | ForEach-Object FullName)
if ($runtimeSources.Count -eq 0) { throw 'No TEST ANXIETY runtime source files found.' }

function Invoke-StaticCompile([string]$name, [string[]]$sourceFiles, [string[]]$references) {
    $outputFile = Join-Path $outputRoot ($name + '.dll')
    $responseFile = Join-Path $outputRoot ($name + '.rsp')
    $arguments = @(
        '/nologo', '/target:library', '/nostdlib+', '/langversion:9.0', '/nullable:disable',
        '/define:UNITY_6000_0_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;ENABLE_INPUT_SYSTEM',
        ('/out:"{0}"' -f $outputFile)
    )
    $arguments += @($references | Sort-Object -Unique | ForEach-Object { '/reference:"{0}"' -f $_ })
    $arguments += @($sourceFiles | Sort-Object | ForEach-Object { '"{0}"' -f $_ })
    [System.IO.File]::WriteAllLines($responseFile, $arguments, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Checking $name ($($sourceFiles.Count) source files)..."
    & dotnet $compiler ('@' + $responseFile)
    if ($LASTEXITCODE -ne 0) { throw "$name static compilation failed with exit code $LASTEXITCODE. Response file: $responseFile" }
    Write-Host "PASS: $name"
}

Write-Host "Unity reference assemblies: $UnityDataPath"
Write-Host "C# compiler: $compiler"
Invoke-StaticCompile 'TestAnxiety.Runtime' $runtimeSources $runtimeReferences
if ($editorSources.Count -gt 0) {
    $editorReferences = $runtimeReferences + @(Join-Path $outputRoot 'TestAnxiety.Runtime.dll') +
        @(Get-ChildItem -LiteralPath $engineRoot -Filter 'UnityEditor*.dll' | ForEach-Object FullName)
    Invoke-StaticCompile 'TestAnxiety.Editor' $editorSources $editorReferences
}
Write-Host 'Static C# validation passed. Unity import, scene lifecycle, rendering, audio and Play Mode were not exercised.'
