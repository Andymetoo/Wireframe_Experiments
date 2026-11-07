$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceFolder = Join-Path $workspace 'Assets/Experiments/IceResurfacer/Validation~'
$runFolder = Join-Path $PSScriptRoot 'CoreRegression'
New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
[xml]$project = Get-Content -LiteralPath (Join-Path $sourceFolder 'IceResurfacer.Validation.csproj') -Raw
foreach ($compile in $project.Project.ItemGroup.Compile) {
    $compile.SetAttribute('Include', [IO.Path]::Combine($sourceFolder, [string]$compile.Include))
}
$project.Save((Join-Path $runFolder 'CoreRegression.csproj'))
[IO.File]::WriteAllText((Join-Path $runFolder 'global.json'), '{"sdk":{"version":"9.0.312","rollForward":"latestPatch"}}')
[IO.File]::WriteAllText((Join-Path $runFolder 'NuGet.Config'), '<configuration><packageSources><clear /></packageSources></configuration>')
$fixtureRelative = 'Assets/Experiments/IceResurfacer/Validation~/RouteRecording/Deterministic-fixture.route.json'
$fixtureSource = Join-Path $workspace $fixtureRelative
$fixtureTarget = Join-Path $runFolder $fixtureRelative
New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureTarget) -Force | Out-Null
Copy-Item -LiteralPath $fixtureSource -Destination $fixtureTarget
if ((Get-FileHash -LiteralPath $fixtureSource).Hash -ne (Get-FileHash -LiteralPath $fixtureTarget).Hash) { throw 'Fixture copy mismatch.' }
$env:DOTNET_CLI_HOME = Join-Path $runFolder '.cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Push-Location $runFolder
try {
    dotnet run --project CoreRegression.csproj --configuration Release 2>&1 | Tee-Object -FilePath (Join-Path $PSScriptRoot 'Core-regression-latest.log')
    if ($LASTEXITCODE -ne 0) { throw "Existing simulation regression failed: $LASTEXITCODE" }
}
finally { Pop-Location }
