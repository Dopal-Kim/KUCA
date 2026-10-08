#!/usr/bin/env bash
# KUCA 메모 서버를 Azure App Service (Linux, .NET 10) 에 올린다.
#
# 처음 한 번:   az login                     (브라우저로 Azure for Students 계정 로그인)
# 배포:         ./deploy-azure.sh             (처음이면 리소스를 만들고, 다음부터는 코드만 다시 올린다)
#
# 메모 DB 와 사진은 앱 폴더가 아닌 /home/data 에 둔다. App Service 의 /home 은 재시작·재배포 뒤에도 남는다.
set -euo pipefail

APP_NAME="${APP_NAME:-kuca-memo}"            # 주소: https://$APP_NAME.azurewebsites.net (전 세계에서 하나뿐이어야 함)
RESOURCE_GROUP="${RESOURCE_GROUP:-kuca-rg}"
LOCATION="${LOCATION:-japaneast}"            # Azure for Students 는 허용 리전이 정해져 있어 한국에서 가장 가까운 도쿄
PLAN="${PLAN:-kuca-plan}"
SKU="${SKU:-F1}"                             # F1 = 무료 (하루 CPU 60분, 잠시 쓰지 않으면 잠듦). 항상 켜 두려면 B1
RUNTIME="${RUNTIME:-DOTNETCORE:10.0}"

cd "$(dirname "$0")/KucaMemoServer"

if ! az group show --name "$RESOURCE_GROUP" >/dev/null 2>&1; then
  echo "▶ 리소스 그룹 만들기: $RESOURCE_GROUP ($LOCATION)"
  az group create --name "$RESOURCE_GROUP" --location "$LOCATION" >/dev/null
fi

if ! az appservice plan show --name "$PLAN" --resource-group "$RESOURCE_GROUP" >/dev/null 2>&1; then
  echo "▶ App Service 요금제 만들기: $PLAN ($SKU, Linux)"
  az appservice plan create --name "$PLAN" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" --sku "$SKU" --is-linux >/dev/null
fi

if ! az webapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" >/dev/null 2>&1; then
  echo "▶ 웹 앱 만들기: $APP_NAME ($RUNTIME)"
  az webapp create --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --plan "$PLAN" --runtime "$RUNTIME" >/dev/null
  az webapp update --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --https-only true >/dev/null
  az webapp config appsettings set --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --settings \
    Memos__DatabasePath=/home/data/memos.db \
    Photos__Directory=/home/data/photos \
    WEBSITES_ENABLE_APP_SERVICE_STORAGE=true >/dev/null
fi

echo "▶ 빌드 (dotnet publish)"
rm -rf ../publish ../publish.zip
dotnet publish -c Release -o ../publish >/dev/null
(cd ../publish && zip -qr ../publish.zip .)

echo "▶ 올리기 (zip 배포)"
az webapp deploy --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --src-path ../publish.zip --type zip >/dev/null

URL="https://$APP_NAME.azurewebsites.net"
echo "▶ 확인: $URL/api/health"
for i in $(seq 1 30); do
  if curl -fsS -m 10 "$URL/api/health" >/dev/null 2>&1; then
    echo "✓ 배포 완료: $URL   (앱 설정 › 메모 서버에 이 주소를 넣으면 됩니다)"
    exit 0
  fi
  sleep 5
done
echo "✗ 아직 응답이 없어요. 첫 시작은 1~2분 걸릴 수 있어요: az webapp log tail -n $APP_NAME -g $RESOURCE_GROUP"
exit 1
