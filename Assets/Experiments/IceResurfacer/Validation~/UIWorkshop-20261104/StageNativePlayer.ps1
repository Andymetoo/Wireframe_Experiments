param([switch]$CopyPlayer)
$ErrorActionPreference='Stop'
$experimentRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$playerRoot=Join-Path $PSScriptRoot 'NativePlayer'
if($CopyPlayer -or !(Test-Path (Join-Path $playerRoot 'AuroraLayout.exe'))) {
    New-Item -ItemType Directory -Force $playerRoot | Out-Null
    Copy-Item -Path (Join-Path $experimentRoot 'Validation~/Build/*') -Destination $playerRoot -Recurse -Force
}
$stagedSource=Join-Path $PSScriptRoot 'Source'
New-Item -ItemType Directory -Force $stagedSource | Out-Null
foreach($folder in @('Core','Presentation')) {
    $destination=Join-Path $stagedSource $folder
    New-Item -ItemType Directory -Force $destination | Out-Null
    Get-ChildItem (Join-Path $experimentRoot $folder) -Filter '*.cs' -File |
        Where-Object { $_.Name -notlike 'IceLayoutPlayerProbe*' } |
        Copy-Item -Destination $destination -Force
}
$managed=Join-Path $playerRoot 'AuroraLayout_Data/Managed'
$response=@('/nologo','/target:library','/langversion:latest','/nostdlib+','/optimize+',
    '/define:DEVELOPMENT_BUILD;UNITY_STANDALONE_WIN;UNITY_6000_4_OR_NEWER;ENABLE_INPUT_SYSTEM',
    ('/out:"'+(Join-Path $managed 'IceResurfacer.Runtime.dll')+'"'))
$response+=Get-ChildItem $managed -Filter '*.dll' -File |
    Where-Object { $_.Name -notlike 'IceResurfacer.*' -and $_.Name -notlike 'Assembly-CSharp*' } |
    ForEach-Object { '/reference:"'+$_.FullName+'"' }
$response+=Get-ChildItem $stagedSource -Filter '*.cs' -File -Recurse | ForEach-Object { '"'+$_.FullName+'"' }
$response+='"'+(Join-Path $PSScriptRoot 'UIWorkshopPlayerProbe.cs')+'"'
$rsp=Join-Path $PSScriptRoot 'native-compile.rsp'
[IO.File]::WriteAllLines($rsp,$response)
& dotnet 'G:/UnityInstalls/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll' ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'Native player runtime compilation failed.'}
& dotnet run --project (Join-Path $PSScriptRoot 'SerializationCompatibility.csproj') -- (Join-Path $experimentRoot 'Validation~/Build/AuroraLayout_Data/Managed/IceResurfacer.Runtime.dll') (Join-Path $managed 'IceResurfacer.Runtime.dll') (Join-Path $PSScriptRoot 'native-schema-compatibility.txt')
if($LASTEXITCODE -ne 0){throw 'Packaged scene serialization compatibility failed.'}
Get-ChildItem $stagedSource -Filter '*.cs' -File -Recurse |
    Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash |
    ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'native-source-manifest.json') -Encoding UTF8
Write-Output "Disposable native player updated: $playerRoot"
