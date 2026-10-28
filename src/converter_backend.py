"""Conversões locais do Converter Aqui. Sem serviços de rede."""
from __future__ import annotations
import contextlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import warnings

from PIL import Image, ImageOps
from pypdf import PdfReader, PdfWriter
import pillow_heif
import resvg_py

BASE = Path(__file__).resolve().parent
IMAGE_EXT = {'.png', '.jpg', '.jpeg', '.bmp', '.gif', '.tif', '.tiff', '.webp', '.heic', '.heif', '.svg'}
AUDIO_EXT = {'.mp3', '.wav', '.flac', '.ogg', '.m4a', '.aac', '.opus', '.wma', '.aiff'}
VIDEO_EXT = {'.mp4', '.mkv', '.avi', '.mov', '.webm', '.wmv', '.m4v', '.mpeg', '.mpg'}
DOC_EXT = {'.doc', '.docx', '.rtf', '.odt', '.txt', '.xls', '.xlsx', '.xlsm', '.ods', '.csv'}
IMAGE_TARGETS = {'PNG', 'JPG', 'BMP', 'GIF', 'TIFF', 'WEBP', 'HEIC'}
AUDIO_TARGETS = {'MP3', 'WAV', 'FLAC', 'OGG', 'M4A'}
VIDEO_TARGETS = {'MP4', 'MKV', 'WEBM'}
pillow_heif.register_heif_opener()
warnings.simplefilter('error', Image.DecompressionBombWarning)
Image.MAX_IMAGE_PIXELS = 100_000_000

class Cancelled(Exception):
    pass

def event(message, step=None, total=None):
    data = {'message': message}
    if total:
        # step = itens concluídos; a interface mostra step/total na barra de progresso.
        data.update(step=step, total=total)
    print(json.dumps(data, ensure_ascii=True), flush=True)

def check_cancel(job):
    if job.get('_cancel') and Path(job['_cancel']).exists():
        raise Cancelled('Cancelado pelo usuário.')

def run_process(args, job, timeout=1800, cancelable=True):
    """Sem shell; stderr vai para disco para não bloquear nem crescer na RAM."""
    check_cancel(job)
    with tempfile.TemporaryFile() as error, tempfile.TemporaryFile() as output:
        with subprocess.Popen([str(a) for a in args], stdin=subprocess.DEVNULL, stdout=output,
                              stderr=error, creationflags=subprocess.CREATE_NO_WINDOW) as process:
            started = time.monotonic()
            try:
                while process.poll() is None:
                    if cancelable:
                        check_cancel(job)
                    if time.monotonic() - started > timeout:
                        raise RuntimeError('O componente excedeu o tempo de espera.')
                    time.sleep(.1)
            except BaseException:
                subprocess.run(['taskkill.exe', '/PID', str(process.pid), '/T', '/F'],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                               creationflags=subprocess.CREATE_NO_WINDOW)
                if process.poll() is None:
                    process.kill()
                process.wait()
                raise
            if process.returncode:
                error.seek(0, 2)
                size = error.tell()
                error.seek(max(0, size - 5000))
                detail = error.read().decode('utf-8', 'replace').strip()
                raise RuntimeError(detail or 'O componente de conversão retornou um erro.')
    check_cancel(job)

def windows_service(mode, source, destination, job):
    if mode == 'OCR':
        engine = BASE / 'ocr/tesseract.exe'
        data = BASE / 'ocr/tessdata'
        if not engine.is_file() or not all((data / (name + '.traineddata')).is_file() for name in ('por', 'eng')):
            raise ValueError('Componentes de OCR ausentes. Reinstale o pacote completo do aplicativo.')
        destination = Path(destination)
        output_base = destination.with_suffix('')
        run_process([engine, source, output_base, '--tessdata-dir', data, '-l', 'por+eng', '--psm', '3', 'txt'], job, timeout=600)
        if not destination.is_file():
            raise ValueError('O mecanismo de OCR não gerou o resultado.')
        return
    powershell = Path(os.environ.get('SystemRoot', r'C:\Windows')) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
    run_process([powershell, '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File',
                 BASE / 'WindowsServices.ps1', '-Mode', mode, '-InputPath', source,
                 '-OutputPath', destination, '-CancelFile', job.get('_cancel', '')],
                job, timeout=600)

def page_indexes(spec, count):
    if not spec.strip():
        return list(range(count))
    indexes = []
    for part in spec.split(','):
        match = re.fullmatch(r'\s*(\d+)\s*(?:-\s*(\d+)\s*)?', part)
        if not match:
            raise ValueError('Páginas inválidas. Use, por exemplo: 1-3,5.')
        start, end = int(match[1]), int(match[2] or match[1])
        if not 1 <= start <= end <= count:
            raise ValueError('O intervalo deve estar entre 1 e %d.' % count)
        indexes.extend(range(start - 1, end))
    return indexes

def read_pdf(source):
    reader = PdfReader(source)
    if reader.is_encrypted and not reader.decrypt(''):
        raise ValueError('PDF protegido: remova a senha antes de converter.')
    if not reader.pages:
        raise ValueError('O PDF não contém páginas.')
    return reader

@contextlib.contextmanager
def load_image(source, job):
    if source.suffix.lower() == '.svg':
        # resvg não executa scripts nem faz requisições HTTP. Renderiza SVG em bitmap.
        data = resvg_py.svg_to_bytes(svg_string=source.read_text(encoding='utf-8-sig'),
                                    width=int(job.get('width') or 1600), resources_dir=str(source.parent))
        handle = Image.open(io.BytesIO(data))
    else:
        handle = Image.open(source)
    try:
        if getattr(handle, 'n_frames', 1) > 1:
            event(source.name + ': apenas o primeiro quadro/página da imagem será usado.')
        image = ImageOps.exif_transpose(handle)
        try:
            image.load()
            width = int(job.get('width') or 0)
            if width > 0 and image.width > width:
                image.thumbnail((width, max(1, round(image.height * width / image.width))), Image.Resampling.LANCZOS)
            yield image
        finally:
            if image is not handle:
                image.close()
    finally:
        handle.close()

def opaque(image):
    rgba = image.convert('RGBA')
    white = Image.new('RGB', image.size, 'white')
    white.paste(rgba, mask=rgba.getchannel('A'))
    rgba.close()
    return white

def image_convert(source, target, stage, job, name=None):
    output = stage / ((name or source.stem) + '.' + target.lower())
    quality = int(job.get('quality', 90))
    with load_image(source, job) as image:
        check_cancel(job)
        if target in {'JPG', 'PDF', 'BMP'}:
            with opaque(image) as converted:
                fmt = {'JPG': 'JPEG', 'PDF': 'PDF', 'BMP': 'BMP'}[target]
                converted.save(output, format=fmt, quality=quality, resolution=96.0)
        elif target == 'HEIC':
            image.convert('RGBA' if 'A' in image.getbands() else 'RGB').save(output, format='HEIF', quality=quality)
        elif image.mode not in {'RGB', 'RGBA', 'L', 'LA', 'P', '1'}:
            with image.convert('RGBA') as converted:
                converted.save(output, format=target, quality=quality)
        else:
            image.save(output, format=target, quality=quality)
    return [output]

def pdf_images(source, target, stage, job):
    rendered = stage / '_paginas'
    rendered.mkdir()
    windows_service('Render', source, rendered, job)
    pages = sorted(rendered.glob('*.png'))
    if not pages:
        raise ValueError('Nenhuma página foi renderizada.')
    selected = page_indexes(job.get('pages', ''), len(pages))
    results = []
    for i in selected:
        check_cancel(job)
        results += image_convert(pages[i], target, stage, job, source.stem + '-pagina-%04d' % (i + 1))
    return results

def media_convert(source, target, stage, job):
    executables = list((BASE / 'runtime/imageio_ffmpeg/binaries').glob('ffmpeg-*.exe'))
    if not executables:
        raise ValueError('FFmpeg ausente. Extraia a pasta completa do aplicativo.')
    output = stage / (source.stem + '.' + target.lower())
    args = [executables[0], '-hide_banner', '-loglevel', 'error', '-nostdin', '-n',
            '-protocol_whitelist', 'file,pipe', '-i', source, '-map_metadata', '-1']
    if target in AUDIO_TARGETS:
        args += ['-map', '0:a:0', '-vn']
        codec = {'MP3': ['-c:a', 'libmp3lame', '-q:a', '2'], 'WAV': ['-c:a', 'pcm_s16le'],
                 'FLAC': ['-c:a', 'flac'], 'OGG': ['-c:a', 'libvorbis', '-q:a', '5'],
                 'M4A': ['-c:a', 'aac', '-b:a', '192k']}[target]
        args += codec
    else:
        args += ['-map', '0:v:0', '-map', '0:a:0?', '-sn', '-dn', '-vf', 'scale=trunc(iw/2)*2:trunc(ih/2)*2', '-pix_fmt', 'yuv420p']
        if target == 'WEBM':
            args += ['-c:v', 'libvpx-vp9', '-crf', '32', '-b:v', '0', '-c:a', 'libopus']
        else:
            args += ['-c:v', 'libx264', '-preset', 'veryfast', '-crf', '23', '-c:a', 'aac', '-b:a', '192k']
            if target == 'MP4':
                args += ['-movflags', '+faststart']
    run_process(args + [output], job)
    return [output]

def document_convert(source, stage, job):
    output = stage / (source.stem + '.pdf')
    candidates = [BASE / 'libreoffice/program/soffice.com', BASE / 'libreoffice/program/soffice.exe']
    for variable in ('ProgramFiles', 'ProgramFiles(x86)'):
        if os.environ.get(variable):
            candidates.append(Path(os.environ[variable]) / 'LibreOffice/program/soffice.com')
    office = next((p for p in candidates if p.is_file()), None)
    if office:
        profile = stage / '_perfil-office'
        user = profile / 'user'
        user.mkdir(parents=True)
        # Perfil separado: não altera as preferências do LibreOffice do usuário.
        (user / 'registrymodifications.xcu').write_text(
            '<?xml version="1.0" encoding="UTF-8"?>'
            '<oor:items xmlns:oor="http://openoffice.org/2001/registry">'
            '<item oor:path="/org.openoffice.Office.Common/Security/Scripting">'
            '<prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop>'
            '</item></oor:items>', encoding='utf-8')
        run_process([office, '-env:UserInstallation=' + profile.as_uri(), '--headless', '--nologo',
                     '--nodefault', '--norestore', '--convert-to', 'pdf', '--outdir', stage, source],
                    job, timeout=300)
        if not output.is_file():
            raise ValueError('O documento não pôde ser exportado. Verifique se está íntegro e sem senha.')
    else:
        raise ValueError('LibreOffice ausente. Extraia também a pasta libreoffice do pacote completo.')
    read_pdf(output)
    return [output]

def convert_one(source, stage, job):
    action, target, extension = job.get('action', 'convert'), job.get('target', '').upper(), source.suffix.lower()
    if not source.is_file():
        raise ValueError('Arquivo não encontrado: ' + str(source))
    if action == 'convert':
        if extension in IMAGE_EXT and target in IMAGE_TARGETS | {'PDF'}:
            return image_convert(source, target, stage, job)
        if extension == '.pdf' and target in IMAGE_TARGETS:
            return pdf_images(source, target, stage, job)
        if extension in AUDIO_EXT | VIDEO_EXT and target in AUDIO_TARGETS:
            return media_convert(source, target, stage, job)
        if extension in VIDEO_EXT and target in VIDEO_TARGETS:
            return media_convert(source, target, stage, job)
        if extension in DOC_EXT and target == 'PDF':
            return document_convert(source, stage, job)
        raise ValueError('Esta combinação de arquivo e formato não é suportada.')
    if action == 'ocr':
        output = stage / (source.stem + '-ocr.txt')
        if extension in IMAGE_EXT:
            png = image_convert(source, 'PNG', stage, job, '_ocr-origem')[0]
            windows_service('OCR', png, output, job)
            if not output.read_text(encoding='utf-8-sig').strip():
                raise ValueError('Nenhum texto reconhecido nesta imagem.')
        elif extension == '.pdf':
            rendered = stage / '_ocr-paginas'
            rendered.mkdir()
            windows_service('Render', source, rendered, job)
            texts = []
            found_text = False
            files = sorted(rendered.glob('*.png'))
            if not files:
                raise ValueError('O PDF não contém páginas.')
            for i in page_indexes(job.get('pages', ''), len(files)):
                check_cancel(job)
                text_file = stage / ('ocr-%d.txt' % i)
                windows_service('OCR', files[i], text_file, job)
                text = text_file.read_text(encoding='utf-8-sig')
                found_text = found_text or bool(text.strip())
                texts.append('--- Página %d ---\n%s' % (i + 1, text))
            if not found_text:
                raise ValueError('Nenhum texto reconhecido nas páginas selecionadas.')
            output.write_text('\n\n'.join(texts), encoding='utf-8-sig')
        else:
            raise ValueError('OCR aceita imagens e PDF.')
        return [output]
    if extension != '.pdf':
        raise ValueError('Esta ferramenta aceita somente PDF.')
    reader = read_pdf(source)
    selected = page_indexes(job.get('pages', ''), len(reader.pages))
    if action in {'split-pdf', 'select-pages'}:
        if action == 'select-pages':
            writer = PdfWriter()
            for i in selected:
                writer.add_page(reader.pages[i])
            output = stage / (source.stem + '-paginas.pdf')
            with output.open('wb') as stream:
                writer.write(stream)
            writer.close()
            return [output]
        results = []
        for i in selected:
            check_cancel(job)
            writer = PdfWriter()
            writer.add_page(reader.pages[i])
            output = stage / (source.stem + '-pagina-%04d.pdf' % (i + 1))
            with output.open('wb') as stream:
                writer.write(stream)
            writer.close()
            results.append(output)
        return results
    if action == 'text-pdf':
        texts = ['--- Página %d ---\n%s' % (i + 1, reader.pages[i].extract_text() or '') for i in selected]
        if not any((reader.pages[i].extract_text() or '').strip() for i in selected):
            raise ValueError('O PDF não tem texto selecionável. Use Reconhecer texto (OCR).')
        output = stage / (source.stem + '-texto.txt')
        output.write_text('\n\n'.join(texts), encoding='utf-8-sig')
        return [output]
    if action == 'extract-images':
        results = []
        for i in selected:
            for n, image in enumerate(reader.pages[i].images, 1):
                check_cancel(job)
                name = source.stem + '-pagina-%04d-imagem-%03d' % (i + 1, n)
                suffix = Path(image.name).suffix.lower()
                if suffix in {'.png', '.jpg', '.jpeg', '.jp2', '.tif', '.tiff', '.gif', '.bmp'}:
                    output = stage / (name + suffix)
                    output.write_bytes(image.data)
                else:
                    # Sem extensão conhecida (ex.: imagens com filtros raros): regrava como PNG.
                    output = stage / (name + '.png')
                    image.image.save(output, format='PNG')
                results.append(output)
        if not results:
            raise ValueError('Nenhuma imagem incorporada foi encontrada. Para capturar a página inteira, converta o PDF para PNG.')
        return results
    raise ValueError('Operação desconhecida.')

def merge_sources(sources, stage, job):
    if len(sources) < 2:
        raise ValueError('Adicione pelo menos dois PDFs ou imagens para juntar.')
    writer = PdfWriter()
    try:
        for n, source in enumerate(sources):
            check_cancel(job)
            event('Juntando %d/%d: %s' % (n + 1, len(sources), source.name), n, len(sources))
            if source.suffix.lower() in IMAGE_EXT:
                image_dir = stage / ('_imagem-%04d' % n)
                image_dir.mkdir()
                source = image_convert(source, 'PDF', image_dir, job)[0]
            elif source.suffix.lower() != '.pdf':
                raise ValueError('Juntar PDF aceita apenas PDFs e imagens.')
            reader = read_pdf(source)
            writer.append(reader, import_outline=False)
        output = stage / 'documentos-unidos.pdf'
        with output.open('wb') as stream:
            writer.write(stream)
        return [output]
    finally:
        writer.close()

def commit(files, destination, job):
    saved = []
    try:
        for source in files:
            check_cancel(job)
            if not source.is_file() or source.stat().st_size == 0:
                raise ValueError('O componente não gerou um arquivo válido.')
            counter = 0
            while True:
                name = source.stem + ('' if counter == 0 else ' (%d)' % counter) + source.suffix
                path = destination / name
                try:
                    handle = path.open('xb')
                    break
                except FileExistsError:
                    counter += 1
            saved.append(path)
            with handle, source.open('rb') as original:
                shutil.copyfileobj(original, handle)
        return [str(p) for p in saved]
    except BaseException:
        for path in saved:
            path.unlink(missing_ok=True)
        raise

def execute(job):
    sources = [Path(p).resolve() for p in job.get('inputs', [])]
    if not sources:
        raise ValueError('Adicione pelo menos um arquivo.')
    if any(not p.is_file() for p in sources):
        raise ValueError('Um dos arquivos de origem não foi encontrado.')
    if not 1 <= int(job.get('quality', 90)) <= 100:
        raise ValueError('A qualidade deve estar entre 1 e 100.')
    if not 0 <= int(job.get('width', 0)) <= 16384:
        raise ValueError('A largura deve estar entre 0 e 16384 pixels.')
    output = Path(job['output']).resolve() if job.get('output') else None
    if output:
        output.mkdir(parents=True, exist_ok=True)
    result = {'ok': True, 'cancelled': False, 'files': [], 'errors': [], 'completed': 0, 'total': len(sources)}
    if job.get('action') == 'merge-pdf':
        check_cancel(job)
        destination = output or sources[0].parent
        with tempfile.TemporaryDirectory(prefix='ConverterAqui-') as temporary:
            files = merge_sources(sources, Path(temporary), job)
            result['files'] = commit(files, destination, job)
            result['completed'] = len(sources)
        return result
    for i, source in enumerate(sources):
        try:
            check_cancel(job)
            event('Convertendo %d/%d: %s' % (i + 1, len(sources), source.name), i, len(sources))
            with tempfile.TemporaryDirectory(prefix='ConverterAqui-') as temporary:
                files = convert_one(source, Path(temporary), job)
                result['files'] += commit(files, output or source.parent, job)
            result['completed'] += 1
        except Cancelled:
            result.update(ok=False, cancelled=True)
            break
        except Exception as error:
            result['ok'] = False
            result['errors'].append({'file': str(source), 'message': str(error)})
            event('Erro em %s: %s' % (source.name, error))
    return result

def main():
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    job_file = Path(sys.argv[1]).resolve()
    result_file = Path(str(job_file) + '.result.json')
    try:
        job = json.loads(job_file.read_text(encoding='utf-8-sig'))
        job['_cancel'] = str(job_file) + '.cancel'
        result = execute(job)
    except Cancelled:
        result = {'ok': False, 'cancelled': True, 'files': [], 'errors': [], 'completed': 0, 'total': 0}
    except Exception as error:
        result = {'ok': False, 'cancelled': False, 'files': [], 'errors': [{'file': '', 'message': str(error)}], 'completed': 0, 'total': 0}
    result_file.write_text(json.dumps(result, ensure_ascii=False), encoding='utf-8')
    return 0 if result['ok'] else 2

if __name__ == '__main__':
    raise SystemExit(main())
