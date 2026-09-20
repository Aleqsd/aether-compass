param(
    [string]$Dotnet = 'dotnet',
    [string]$DalamudHome = "$env:APPDATA/XIVLauncher/addon/Hooks/dev"
)
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
if (-not (Test-Path -LiteralPath "$DalamudHome/Dalamud.dll")) { throw 'Dalamud.dll introuvable. Fournir -DalamudHome vers une installation API 15.' }
$manifest = Get-Content -LiteralPath "$repoRoot/src/AetherCompass.json" -Raw | ConvertFrom-Json
if ($manifest.DalamudApiLevel -ne 15) { throw 'Niveau API inattendu' }
& $Dotnet build "$repoRoot/src/AetherCompass.csproj" -c Release "-p:DalamudHome=$DalamudHome" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Compilation échouée' }
& $Dotnet run --project "$repoRoot/tests/AetherCompass.Core.Tests.csproj" -c Release --no-launch-profile
if ($LASTEXITCODE -ne 0) { throw 'Tests métier échoués' }
$buildPath = "$repoRoot/src/bin/Release/net10.0-windows"
$version = ([version]$manifest.AssemblyVersion).ToString(3)
if ([Reflection.AssemblyName]::GetAssemblyName("$buildPath/AetherCompass.dll").Version.ToString() -ne $manifest.AssemblyVersion) { throw 'Version DLL différente du manifeste' }
$packagePath = "$repoRoot/releases/$version"
New-Item -ItemType Directory -Force "$repoRoot/plugin", $packagePath | Out-Null
$files = @('AetherCompass.dll', 'AetherCompass.Core.dll', 'AetherCompass.deps.json', 'AetherCompass.json')
foreach ($file in $files) {
    foreach ($target in @("$repoRoot/plugin", $packagePath)) {
        Copy-Item -LiteralPath "$buildPath/$file" -Destination "$target/$file" -Force
        if ((Get-FileHash -LiteralPath "$target/$file").Hash -ne (Get-FileHash -LiteralPath "$buildPath/$file").Hash) { throw "Copie différente : $file" }
    }
}
Copy-Item -LiteralPath "$repoRoot/LICENSE" -Destination $packagePath -Force
$archive = "$repoRoot/releases/AetherCompass-$version.zip"
$packageFiles = @($files | ForEach-Object { "$packagePath/$_" }) + "$packagePath/LICENSE"
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archive -Force
$hashLines = @($archive, "$packagePath/AetherCompass.dll", "$packagePath/AetherCompass.Core.dll") | ForEach-Object { "$((Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_))" }
$hashLines | Set-Content -LiteralPath "$repoRoot/releases/SHA256SUMS.txt" -Encoding utf8NoBOM
Write-Output "Paquet prêt : $archive"
