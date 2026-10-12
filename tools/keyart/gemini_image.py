"""
제미나이 이미지 생성 (BAZE · 챗쿠 API 게이트웨이, 모델 gemini-3.1-flash-image-preview).
  python tools/keyart/gemini_image.py <out.png> <prompt.txt> [레퍼런스 이미지 ...] [--ratio 16:9] [--size 2K]
키: 환경 변수 BAZE_API_KEY (BAZE › API Gateway 에서 발급). 저장소에 넣지 않는다.
레퍼런스 이미지는 그림체를 맞출 기준 (게임 렌더, 키아트, 피규어 렌더 등)으로 함께 보낸다.
"""
import base64
import json
import mimetypes
import os
import pathlib
import sys
import urllib.request

URL = 'https://factchat-cloud.mindlogic.ai/v1/gateway/images/generate/'
MODEL = os.environ.get('KUCA_IMAGE_MODEL', 'gemini-3.1-flash-image-preview')


def main(argv):
    opts = {'--ratio': '1:1', '--size': '1K'}
    args = []
    it = iter(argv)
    for a in it:
        if a in opts:
            opts[a] = next(it)
        else:
            args.append(a)
    out, prompt_file, refs = args[0], args[1], args[2:]
    key = os.environ.get('BAZE_API_KEY')
    if not key:
        sys.exit('BAZE_API_KEY 환경 변수가 없습니다')
    images = []
    for r in refs:
        mt = mimetypes.guess_type(r)[0] or 'image/png'
        images.append(f'data:{mt};base64,' + base64.b64encode(open(r, 'rb').read()).decode())
    body = {'model': MODEL, 'prompt': pathlib.Path(prompt_file).read_text(), 'number_of_images': 1,
            'image_size': opts['--size'], 'aspect_ratio': opts['--ratio']}
    if images:
        body['input_images'] = images
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), headers={
        'Content-Type': 'application/json', 'x-api-key': key, 'User-Agent': 'curl/8.5.0'})
    d = json.load(urllib.request.urlopen(req, timeout=600))
    url = d['data'][0]['url']
    raw = base64.b64decode(url.split(',', 1)[1]) if url.startswith('data:') else urllib.request.urlopen(url).read()
    try:
        import io
        from PIL import Image
        Image.open(io.BytesIO(raw)).save(out)   # 확장자대로 저장 (png 등)
    except ImportError:
        open(out, 'wb').write(raw)
    print('saved', out, 'credits', d.get('credits_charged'))


if __name__ == '__main__':
    main(sys.argv[1:])
