param([string]$Tag = '')   # e.g. v1.2.0, checked against the csproj version when given
$ErrorActionPreference = 'Stop'

$ver = [string](([xml](Get-Content BrowSel.csproj -Raw)).Project.PropertyGroup.Version)
if ($Tag -and $Tag -ne "v$ver") { throw "Tag $Tag does not match csproj version $ver" }

Remove-Item out-rel -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish BrowSel.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o out-rel
if ($LASTEXITCODE) { throw 'publish failed' }

$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
          "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found' }
& $iscc "/DAppVersion=$ver" installer\BrowSel.iss
if ($LASTEXITCODE) { throw 'ISCC failed' }

# The updater (T1) refuses installers without a published hash.
$setup = "dist\BrowSelSetup-$ver.exe"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()
"$hash  BrowSelSetup-$ver.exe" | Set-Content "$setup.sha256" -NoNewline
Write-Host "OK: $setup`n$hash"
