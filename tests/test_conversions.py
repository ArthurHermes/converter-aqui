import os
import hashlib
import json
import math
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
import wave
import zipfile

BASE = Path(os.environ['CONVERTER_AQUI_APP']).resolve()
sys.path.insert(0, str(BASE))
import converter_backend as app
from PIL import Image, ImageDraw, ImageFont
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject, NameObject, DecodedStreamObject

ROOT = Path(__file__).resolve().parent / 'generated'
ROOT.mkdir(exist_ok=True)
passed = []
failures = []
def check(ok, name):
    if not ok:
        raise AssertionError(name)
    passed.append(name)
    print('PASS:', name, flush=True)

def job(inputs, target='', action='convert', **kwargs):
    dest = ROOT / ('saida-%03d' % (len(list(ROOT.glob('saida-*'))) + 1))
    spec = dict(inputs=[str(p) for p in inputs], output=str(dest), target=target, action=action, **kwargs)
    result = app.execute(spec)
    if not result['ok']:
        raise AssertionError(result)
    return [Path(p) for p in result['files']]

def test(name, fn):
    print('\nTEST:', name, flush=True)
    try:
        fn()
    except Exception as error:
        import traceback
        failures.append((name, str(error)))
        traceback.print_exc()

image = ROOT / 'imagem ação.png'
with Image.new('RGBA', (240, 140), (0, 0, 0, 0)) as img:
    ImageDraw.Draw(img).rectangle((20,20,220,120),fill='cornflowerblue')
    img.save(image)
original_hash = hashlib.sha256(image.read_bytes()).hexdigest()

def images():
    for fmt in sorted(app.IMAGE_TARGETS | {'PDF'}):
        file = job([image],fmt)[0]
        if fmt == 'PDF':
            check(len(PdfReader(file).pages) == 1, 'Imagem -> PDF válido')
        else:
            with Image.open(file) as loaded:
                check(loaded.size == (240,140), 'Imagem -> ' + fmt)
                if fmt == 'JPG': check(loaded.convert('RGB').getpixel((0,0))[0] > 240, 'JPG fundo branco')
            check(bool(job([file],'PNG')[0].is_file()), fmt + ' -> PNG')
    svg = ROOT / 'desenho.svg'
    svg.write_text('<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100"><rect width="200" height="100" fill="red"/></svg>')
    out = job([svg], 'PNG', width=400)[0]
    with Image.open(out) as loaded:
        check(loaded.size == (400,200) and loaded.getpixel((5,5))[0] == 255, 'SVG rasterizado')
    resized = job([image],'WEBP',width=120,quality=70)[0]
    with Image.open(resized) as loaded: check(loaded.size == (120,70), 'Redimensionamento proporcional')
    check(hashlib.sha256(image.read_bytes()).hexdigest() == original_hash, 'Original preservado byte a byte')

def text_pdf(path, texts):
    writer = PdfWriter()
    for n, text in enumerate(texts):
        page = writer.add_blank_page(width=300+n*100, height=200)
        font = DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')})
        page[NameObject('/Resources')] = DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):writer._add_object(font)})})
        stream = DecodedStreamObject(); stream.set_data(('BT /F1 24 Tf 20 100 Td ('+text+') Tj ET').encode('ascii'))
        page[NameObject('/Contents')] = writer._add_object(stream)
    writer.write(path); writer.close()

pdf = ROOT / 'texto duas páginas.pdf'
text_pdf(pdf, ['Primeira pagina', 'Segunda pagina'])

def pdf_tools():
    outputs = job([pdf],action='split-pdf')
    check(len(outputs) == 2 and all(len(PdfReader(p).pages) == 1 for p in outputs),'Separar PDF')
    out = job([pdf],action='select-pages',pages='2,1')[0]
    pages = PdfReader(out).pages
    check(len(pages) == 2 and 'Segunda' in pages[0].extract_text(),'Selecionar/reordenar páginas')
    out = job([pdf],action='text-pdf',pages='2')[0]
    check('Segunda' in out.read_text(encoding='utf-8-sig') and 'Primeira' not in out.read_text(encoding='utf-8-sig'),'Extrair texto e filtrar página')
    out = job([pdf, image],action='merge-pdf')[0]
    check(len(PdfReader(out).pages) == 3 and 'Primeira' in PdfReader(out).pages[0].extract_text(),'Juntar PDF e imagem preservando texto')
    check(len(job([out],action='extract-images')) >= 1,'Extrair imagens incorporadas')
    outs = job([pdf], 'WEBP', pages='2')
    check(len(outs) == 1 and '-0002' in outs[0].name,'PDF -> WebP com seleção de página')
    with Image.open(outs[0]) as loaded: check(loaded.width > 400,'PDF renderizado em alta resolução')

def ocr():
    text_image = ROOT / 'texto para OCR.png'
    with Image.new('RGB',(1400,350),'white') as img:
        d = ImageDraw.Draw(img); d.text((40,50),'TESTE CONVERSOR 12345',font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',72),fill='black'); img.save(text_image)
    out = job([text_image],action='ocr')[0]
    check('12345' in out.read_text(encoding='utf-8-sig'),'OCR de imagem com mecanismo incluído')
    out = job([pdf],action='ocr',pages='1')[0]
    check('Primeira' in out.read_text(encoding='utf-8-sig'),'OCR de PDF com mecanismo incluído')

def media():
    wav = ROOT / 'audio teste.wav'
    with wave.open(str(wav),'wb') as stream:
        stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(22050)
        stream.writeframes(b''.join(struct.pack('<h',int(10000*math.sin(2*math.pi*440*n/22050))) for n in range(11025)))
    ffmpeg = next((BASE/'runtime/imageio_ffmpeg/binaries').glob('ffmpeg-*.exe'))
    for fmt in sorted(app.AUDIO_TARGETS):
        output = job([wav],fmt)[0]
        subprocess.run([str(ffmpeg),'-hide_banner','-loglevel','error','-i',str(output),'-f','null','-'],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
        check(output.stat().st_size > 0,'Áudio -> ' + fmt + ' decodificado')
    source = ROOT / 'video teste.mp4'
    subprocess.run([str(ffmpeg),'-hide_banner','-loglevel','error','-y','-f','lavfi','-i','color=c=blue:s=160x120:r=10','-f','lavfi','-i','sine=frequency=440:sample_rate=22050','-t','0.5','-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac',str(source)],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
    for fmt in sorted(app.VIDEO_TARGETS | {'MP3'}):
        out = job([source],fmt)[0]
        subprocess.run([str(ffmpeg),'-hide_banner','-loglevel','error','-i',str(out),'-f','null','-'],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
        check(out.stat().st_size > 0, 'Vídeo -> '+fmt+' decodificado')

def office_fixtures():
    docx = ROOT/'documento teste.docx'
    with zipfile.ZipFile(docx,'w') as z:
        z.writestr('[Content_Types].xml','<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
        z.writestr('_rels/.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
        z.writestr('word/document.xml','<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Documento de teste Converter Aqui</w:t></w:r></w:p></w:body></w:document>')
    xlsx = ROOT/'planilha teste.xlsx'
    with zipfile.ZipFile(xlsx,'w') as z:
        z.writestr('[Content_Types].xml','<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>')
        z.writestr('_rels/.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>')
        z.writestr('xl/workbook.xml','<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Teste" sheetId="1" r:id="rId1"/></sheets></workbook>')
        z.writestr('xl/_rels/workbook.xml.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>')
        z.writestr('xl/worksheets/sheet1.xml','<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>Planilha Converter Aqui</t></is></c><c r="B1"><v>12345</v></c></row></sheetData></worksheet>')
    return docx,xlsx

def office():
    for source in office_fixtures():
        output = job([source],'PDF')[0]
        reader = PdfReader(output)
        text = ''.join(p.extract_text() for p in reader.pages)
        check(len(reader.pages) > 0 and ('12345' in text if source.suffix == '.xlsx' else 'Converter' in text),'Office '+source.suffix+' -> PDF com conteúdo')

def robustness():
    dest = ROOT/'lote'; dest.mkdir(exist_ok=True)
    sources = [image, ROOT/'imagem2.png']; shutil.copyfile(image,sources[1])
    result = app.execute(dict(inputs=[str(p) for p in sources], output=str(dest), target='PNG'))
    check(result['ok'] and result['completed'] == 2 and len(result['files']) == 2,'Lote com dois arquivos')
    before = {p.name:p.read_bytes() for p in dest.iterdir()}
    again = app.execute(dict(inputs=[str(image)],output=str(dest),target='PNG'))
    check(all((dest/n).read_bytes()==data for n,data in before.items()) and Path(again['files'][0]).name not in before,'Colisões não sobrescrevem arquivos')
    bad = ROOT/'corrompido.pdf'; bad.write_text('bad pdf')
    out = ROOT/'falhas'
    result = app.execute(dict(inputs=[str(bad),str(image)],output=str(out),target='PNG'))
    check(not result['ok'] and result['completed'] == 1 and len(result['errors']) == 1 and len(result['files']) == 1,'Lote continua após arquivo inválido')
    check(not list(out.glob('corrompido*')),'Sem arquivos parciais após falha')
    marker = ROOT/'cancelar'; marker.touch()
    result = app.execute(dict(inputs=[str(image)],output=str(out),target='PNG',_cancel=str(marker)))
    check(result['cancelled'] and not result['files'],'Cancelamento respeitado')
    marker.unlink()
    try: app.page_indexes('0-3',2)
    except ValueError: check(True,'Intervalo de páginas inválido rejeitado')
    else: raise AssertionError('range')
    jobfile = ROOT/'cli.json';jobfile.write_text(json.dumps(dict(inputs=[str(image)],output=str(ROOT/'cli'),target='WEBP')))
    result = subprocess.run([str(BASE/'ConverterAqui.exe'),'--job',str(jobfile)],creationflags=subprocess.CREATE_NO_WINDOW)
    check(result.returncode == 0 and json.loads(Path(str(jobfile)+'.result.json').read_text())['ok'],'Executável C# -> backend Python -> WebP')

for name, fn in [('Imagens',images),('PDF',pdf_tools),('OCR',ocr),('Mídia',media),('Office',office),('Robustez e lote',robustness)]:
    test(name,fn)
report = {'passed':len(passed),'checks':passed,'failures':failures}
(ROOT/'relatorio.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=True,indent=2),flush=True)
raise SystemExit(1 if failures else 0)
