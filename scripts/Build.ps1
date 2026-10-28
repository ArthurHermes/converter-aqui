param(
    [Parameter(Mandatory=$true)][string]$ComponentsFrom,
    [Parameter(Mandatory=$true)][string]$InnoCompiler
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$components=(Resolve-Path -LiteralPath $ComponentsFrom).Path
$app=Join-Path $root 'dist\ConverterAqui-2.1.0-win-x64'
foreach($file in @('runtime\python.exe','libreoffice\program\soffice.com','ocr\tesseract.exe','ocr\tessdata\por.traineddata','ocr\tessdata\eng.traineddata')) {
    if(-not (Test-Path -LiteralPath (Join-Path $components $file))) { throw ('Componente ausente: '+$file) }
}
dotnet publish (Join-Path $root 'src\ConverterAqui.csproj') -c Release -r win-x64 --self-contained true -o $app /p:DebugType=None /p:DebugSymbols=false
if($LASTEXITCODE -ne 0) { throw 'Falha na compilação' }
foreach($name in @('runtime','libreoffice','ocr','LICENCAS')) {
    Copy-Item -LiteralPath (Join-Path $components $name) -Destination (Join-Path $app $name) -Recurse -Force
}
foreach($name in @('LICENSE.txt','LEIA-ME.md','TERCEIROS.md','COMPONENTES.json')) {
    Copy-Item -LiteralPath (Join-Path $components $name) -Destination (Join-Path $app $name) -Force
}
& $InnoCompiler ('/DAppFiles='+$app) ('/DReleaseOutput='+(Join-Path $root 'dist')) (Join-Path $root 'installer\ConverterAqui.iss')
if($LASTEXITCODE -ne 0) { throw 'Falha ao criar instalador' }
