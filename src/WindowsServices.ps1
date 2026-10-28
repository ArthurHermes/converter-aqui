param(
    [Parameter(Mandatory=$true)][ValidateSet('Render','OCR')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$CancelFile = ''
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding
function Check-Cancel { if ($CancelFile -and (Test-Path -LiteralPath $CancelFile)) { throw 'Cancelado pelo usuário.' } }
function Await-Result($operation, [Type]$resultType) {
    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
    $task = $method.MakeGenericMethod($resultType).Invoke($null, @($operation))
    $task.GetAwaiter().GetResult()
}
function Await-Action($operation) {
    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and -not $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } | Select-Object -First 1
    $task = $method.Invoke($null, @($operation))
    $task.GetAwaiter().GetResult()
}
try {
    Check-Cancel
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
    $file = Await-Result ([Windows.Storage.StorageFile]::GetFileFromPathAsync($InputPath)) ([Windows.Storage.StorageFile])
    if ($Mode -eq 'Render') {
        $null = [Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType=WindowsRuntime]
        $null = [Windows.Storage.Streams.InMemoryRandomAccessStream, Windows.Storage.Streams, ContentType=WindowsRuntime]
        $pdf = Await-Result ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
        for ($i = 0; $i -lt $pdf.PageCount; $i++) {
            Check-Cancel
            $page = $pdf.GetPage($i)
            $stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
            try {
                $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
                $options.DestinationWidth = [uint32][Math]::Min(8192, [Math]::Max(1, $page.Size.Width * 2))
                Await-Action ($page.RenderToStreamAsync($stream, $options))
                $stream.Seek(0)
                $reader = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($stream)
                $target = [System.IO.File]::Open((Join-Path $OutputPath ('pagina-{0:D6}.png' -f ($i+1))), [System.IO.FileMode]::CreateNew)
                try { $reader.CopyTo($target) } finally { $target.Dispose(); $reader.Dispose() }
            } finally { $stream.Dispose(); $page.Dispose() }
        }
    } else {
        $null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
        $null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime]
        $null = [Windows.Storage.FileAccessMode, Windows.Storage, ContentType=WindowsRuntime]
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
        if ($null -eq $engine) { throw 'Nenhum idioma de OCR disponível. Adicione um idioma em Configurações do Windows > Hora e idioma.' }
        $stream = Await-Result ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
        try {
            $decoder = Await-Result ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
            $transform = New-Object Windows.Graphics.Imaging.BitmapTransform
            $limit = [double][Windows.Media.Ocr.OcrEngine]::MaxImageDimension
            $ratio = [Math]::Min(1, $limit / [Math]::Max($decoder.PixelWidth, $decoder.PixelHeight))
            $transform.ScaledWidth = [uint32][Math]::Max(1, [Math]::Floor($decoder.PixelWidth * $ratio))
            $transform.ScaledHeight = [uint32][Math]::Max(1, [Math]::Floor($decoder.PixelHeight * $ratio))
            $bitmap = Await-Result ($decoder.GetSoftwareBitmapAsync([Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8, [Windows.Graphics.Imaging.BitmapAlphaMode]::Premultiplied, $transform, [Windows.Graphics.Imaging.ExifOrientationMode]::RespectExifOrientation, [Windows.Graphics.Imaging.ColorManagementMode]::DoNotColorManage)) ([Windows.Graphics.Imaging.SoftwareBitmap])
            try {
                $recognized = Await-Result ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
                $text = ($recognized.Lines | ForEach-Object { $_.Text }) -join [Environment]::NewLine
                [System.IO.File]::WriteAllText($OutputPath, $text, (New-Object System.Text.UTF8Encoding $true))
            } finally { $bitmap.Dispose() }
        } finally { $stream.Dispose() }
    }
} catch {
    [Console]::Error.WriteLine($_.Exception.Message + ' [' + $_.InvocationInfo.ScriptLineNumber + ']')
    exit 1
}
