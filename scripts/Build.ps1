param(
    [Parameter(Mandatory=$true)][string]$ComponentsFrom,
    [Parameter(Mandatory=$true)][string]$InnoCompiler,
    # Vazio usa a versão do ConverterAqui.csproj. O workflow de release passa a versão da tag.
    [string]$Version = ''
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$components=(Resolve-Path -LiteralPath $ComponentsFrom).Path
if(-not $Version) { $Version=([xml](Get-Content -Raw -LiteralPath (Join-Path $root 'src\ConverterAqui.csproj'))).Project.PropertyGroup.Version }
if($Version -notmatch '^\d+\.\d+\.\d+$') { throw ('Versão inválida: '+$Version+'. Use o formato 1.2.3.') }
$app=Join-Path $root ('dist\ConverterAqui-'+$Version+'-win-x64')
foreach($file in @('runtime\python.exe','libreoffice\program\soffice.com','ocr\tesseract.exe','ocr\tessdata\por.traineddata','ocr\tessdata\eng.traineddata')) {
    if(-not (Test-Path -LiteralPath (Join-Path $components $file))) { throw ('Componente ausente: '+$file) }
}
if(Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force }
dotnet publish (Join-Path $root 'src\ConverterAqui.csproj') -c Release -r win-x64 --self-contained true -o $app /p:DebugType=None /p:DebugSymbols=false ('/p:Version='+$Version) ('/p:AssemblyVersion='+$Version+'.0') ('/p:FileVersion='+$Version+'.0')
if($LASTEXITCODE -ne 0) { throw 'Falha na compilação' }
foreach($name in @('runtime','libreoffice','ocr','LICENCAS')) {
    Copy-Item -LiteralPath (Join-Path $components $name) -Destination (Join-Path $app $name) -Recurse -Force
}
foreach($name in @('LICENSE.txt','LEIA-ME.md','TERCEIROS.md','COMPONENTES.json')) {
    Copy-Item -LiteralPath (Join-Path $components $name) -Destination (Join-Path $app $name) -Force
}
& $InnoCompiler ('/DAppVersion='+$Version) ('/DAppFiles='+$app) ('/DReleaseOutput='+(Join-Path $root 'dist')) (Join-Path $root 'installer\ConverterAqui.iss')
if($LASTEXITCODE -ne 0) { throw 'Falha ao criar instalador' }
$setup=Join-Path $root ('dist\ConverterAqui-'+$Version+'-Setup-win-x64.exe')
$hash=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $root 'dist\SHA256SUMS.txt'), $hash+'  '+(Split-Path -Leaf $setup)+"`n")
