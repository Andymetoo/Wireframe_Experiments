param([string]$UnityDataPath = 'G:\UnityInstalls\6000.4.1f1\Editor\Data')
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$sourceRoot = Join-Path $repositoryRoot 'Assets/Experiments/IceResurfacer'
$outputRoot = Join-Path $PSScriptRoot 'ManagedCompile'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$compiler = 'C:\Program Files\dotnet\sdk\9.0.312\Roslyn\bincore\csc.dll'
$engineRoot = Join-Path $UnityDataPath 'Managed/UnityEngine'
$references = @((Join-Path $UnityDataPath 'NetStandard/ref/2.1.0/netstandard.dll'))
$references += @(Get-ChildItem -LiteralPath (Join-Path $UnityDataPath 'NetStandard/compat/2.1.0/shims/netstandard') -Filter '*.dll' | ForEach-Object FullName)
$references += @(Get-ChildItem -LiteralPath $engineRoot -Filter '*.dll' | ForEach-Object FullName)
foreach ($assembly in @('Unity.InputSystem.dll', 'UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.Collections.dll')) {
    $references += Join-Path $repositoryRoot ('Library/ScriptAssemblies/' + $assembly)
}
foreach ($required in @($compiler) + $references) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required local compiler/reference is missing: $required" }
}
$sources = @(& rg --files $sourceRoot -g '*.cs' -g '!**/*~/**')
if ($LASTEXITCODE -ne 0 -or $sources.Count -eq 0) { throw 'Cannot enumerate experiment C# source files.' }
$runtimeSources = @($sources | Where-Object { $_ -notmatch '[\\/]Editor[\\/]' })
$editorSources = @($sources | Where-Object { $_ -match '[\\/]Editor[\\/]' })
function Invoke-SourceCompile([string]$name, [string[]]$sourceFiles, [string[]]$assemblyReferences) {
    $responseFile = Join-Path $outputRoot ($name + '.rsp')
    $arguments = @('/nologo', '/target:library', '/nostdlib+', '/langversion:9.0', '/nullable:disable', '/unsafe+',
        '/define:UNITY_6000_0_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_6000_4_OR_NEWER;UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;ENABLE_INPUT_SYSTEM',
        ('/out:"{0}"' -f (Join-Path $outputRoot ($name + '.dll'))))
    $arguments += @($assemblyReferences | Sort-Object -Unique | ForEach-Object { '/reference:"{0}"' -f $_ })
    $arguments += @($sourceFiles | Sort-Object -Unique | ForEach-Object { '"{0}"' -f $_ })
    [IO.File]::WriteAllLines($responseFile, $arguments, [Text.UTF8Encoding]::new($false))
    $heading = "Static C# compilation using installed Unity 6000.4.1f1 API references. $name sources: $($sourceFiles.Count)"
    Write-Output $heading
    $compilerOutput = @(& dotnet $compiler ('@' + $responseFile) 2>&1)
    $compileExit = $LASTEXITCODE
    [IO.File]::WriteAllLines((Join-Path $outputRoot ($name + '.log')), @($heading) + $compilerOutput + @("Compiler exit: $compileExit"), [Text.UTF8Encoding]::new($false))
    $compilerOutput | Write-Output
    if ($compileExit -ne 0) { throw "Static compilation failed, exit $compileExit. Response: $responseFile" }
}
Invoke-SourceCompile 'IceResurfacer.Runtime' $runtimeSources $references
Invoke-SourceCompile 'IceResurfacer.Editor' $editorSources ($references + @(Join-Path $outputRoot 'IceResurfacer.Runtime.dll'))
Write-Output 'PASS static C# compilation only. Unity import, serialization, rendering, interaction, and Play Mode were not exercised.'
