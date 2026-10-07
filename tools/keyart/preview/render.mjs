// 미리보기 스크린샷: node render.mjs [이름=x,z,거리,요 ...]
// 결과: shots/<이름>.png  (Chromium 은 PLAYWRIGHT 에 맞춰 설치된 것을 쓴다. CHROME 환경변수로 바꿀 수 있음)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..', '..');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.jpg': 'image/jpeg', '.png': 'image/png' };

const VIEWS = {
  plaza: [60, -352, 420, 0],        // 사색의 광장, 중앙도서관
  library_close: [-20, -352, 190, 300],
  theater: [300, -560, 380, 0],     // 평화노천극장, 연못
  stadium: [-198, -20, 420, 20],    // 대운동장
  campus_wide: [60, -250, 1100, 0],
};

const args = process.argv.slice(2);
const views = args.length
  ? Object.fromEntries(args.map((a) => { const [n, v] = a.split('='); return [n, v ? v.split(',').map(Number) : VIEWS[n]]; }))
  : VIEWS;

const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!p.startsWith(root) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { res.writeHead(404); return res.end(); }
  res.writeHead(200, { 'Content-Type': types[path.extname(p)] || 'application/octet-stream' });
  fs.createReadStream(p).pipe(res);
});
await new Promise((r) => server.listen(0, r));
const port = server.address().port;

const exe = process.env.CHROME || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
fs.mkdirSync(path.join(here, 'shots'), { recursive: true });
for (let [name, [x, z, dist, yaw, pitch]] of Object.entries(views)) {
  const page = await browser.newPage({ viewport: { width: Number(process.env.W || 1280), height: Number(process.env.H || 800) } });
  page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') console.log('[page]', m.text().slice(0, 600)); });
  await page.goto(`http://localhost:${port}/tools/keyart/preview/index.html?x=${x}&z=${z}&dist=${dist}&w=${process.env.W || 1280}&h=${process.env.H || 800}&yaw=${yaw}${pitch !== undefined ? '&pitch=' + pitch : ''}${process.env.MODE ? "&mode=" + process.env.MODE : ""}${process.env.SEASON ? "&season=" + process.env.SEASON : ""}${process.env.EXTRA || ""}`);
  name = [name, process.env.MODE, process.env.SEASON].filter(Boolean).join("_");
  await page.waitForFunction(() => window.__done || window.__error, null, { timeout: 600000 });
  const err = await page.evaluate(() => window.__error);
  if (err) console.log(name, 'ERROR', err);
  await page.screenshot({ path: path.join(here, 'shots', `${name}.png`) });
  console.log('shot', name);
  await page.close();
}
await browser.close();
server.close();
