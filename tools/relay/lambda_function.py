"""
챗쿠(FactChat · BAZE) 게이트웨이 중계 — AWS Lambda (서울 ap-northeast-2), Python 3.12, 함수 URL.

왜: 게이트웨이가 Azure(일본) 서버의 요청을 403 으로 막는다. 한국 리전 Lambda 를 거치면 정상으로 받는다.
- 비밀 경로로 온 POST 만 게이트웨이에 그대로 넘긴다 (본문 · Authorization 은 Azure 서버가 보낸 그대로).
- 비밀 경로는 환경 변수 RELAY_SECRET (저장소에 넣지 않는다). 키는 여기 저장하지 않는다.
- Azure 앱 설정: Recommend__Endpoint = https://<함수 URL>/<RELAY_SECRET>/v1/gateway/chat/completions
"""
import base64
import hmac
import os
import urllib.error
import urllib.request

UPSTREAM = "https://factchat-cloud.mindlogic.ai/v1/gateway/chat/completions"
SECRET = os.environ.get("RELAY_SECRET", "")
PASS_HEADERS = ("authorization", "content-type", "accept")


def lambda_handler(event, context):
    method = event.get("requestContext", {}).get("http", {}).get("method", "")
    expected = f"/{SECRET}/v1/gateway/chat/completions"
    if not SECRET or method != "POST" or not hmac.compare_digest(event.get("rawPath", ""), expected):
        return {"statusCode": 403, "body": "forbidden"}

    body = event.get("body") or ""
    data = base64.b64decode(body) if event.get("isBase64Encoded") else body.encode("utf-8")
    headers = {k: v for k, v in (event.get("headers") or {}).items() if k.lower() in PASS_HEADERS}
    headers["User-Agent"] = "kuca-relay/1.0"
    req = urllib.request.Request(UPSTREAM, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=28) as r:
            return {"statusCode": r.status,
                    "headers": {"content-type": r.headers.get("content-type", "application/json")},
                    "body": r.read().decode("utf-8")}
    except urllib.error.HTTPError as e:
        return {"statusCode": e.code,
                "headers": {"content-type": e.headers.get("content-type", "text/plain")},
                "body": e.read().decode("utf-8", "replace")}
    except Exception:
        return {"statusCode": 502, "body": "relay error"}
