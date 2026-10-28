# Converter Aqui 2.1.0 — instalador completo para Windows x64

Baixe **ConverterAqui-2.1.0-Setup-win-x64.exe** nos arquivos deste release e execute. O instalador instala apenas para seu usuário e oferece integração com o botão direito.

**Sem instalações adicionais:** inclui runtime .NET, Python, bibliotecas de imagem/PDF, FFmpeg, LibreOffice e Tesseract com português e inglês. Não requer Microsoft Office. Depois de baixar, a instalação e as conversões funcionam offline.

## Incluído

- Interface organizada por conversão, PDF e OCR, com fila de vários arquivos.
- PNG, JPG, WebP, HEIC, BMP, GIF e TIFF; SVG como entrada.
- PDF: juntar, separar, selecionar páginas, extrair imagens/texto e OCR para TXT.
- Áudio/vídeo e extração de áudio.
- Word e Excel para PDF por um mecanismo incluído.
- Atalho no Iniciar, menu de contexto opcional e desinstalador.

## Requisitos

Windows 10 build 19041+ ou Windows 11, 64 bits. Reserve 2 GB para instalar, além do espaço dos seus arquivos e temporários. A renderização de PDF usa o PowerShell 5.1 fornecido pelo Windows. Políticas que impeçam a execução de programas/scripts podem limitar o app.

## Verificação

46 verificações funcionais de conversão e 10 verificações da interface passaram nesta máquina. As conversões foram executadas com PATH sem ferramentas externas, DOTNET_ROOT apontando para uma pasta vazia e TESSDATA_PREFIX vazio. Foram analisadas as importações de 852 binários PE para conferir dependências nativas. Os resultados específicos de instalação/desinstalação estão em VERIFICACAO-RELEASE.json.

Isso não substitui um teste em uma máquina virtual limpa. A compatibilidade com todo arquivo ou codec possível não é garantida.

## Observações

O instalador não tem assinatura digital de editor; o Windows pode mostrar confirmação de segurança. O SHA256 do arquivo está em SHA256SUMS.txt. OCR gera TXT e precisa de revisão. SVG é entrada rasterizada. Originais são preservados.

Código e instruções de compilação no repositório. Componentes terceiros mantêm suas próprias licenças e fontes de origem, listadas no pacote.
