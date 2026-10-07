// 키아트 지도 미리보기. Unity 셰이더(KUCAStylizedLighting.hlsl, StylizedBuilding.shader, StylizedGround.shader)와
// 같은 조명 모델: 색 = 알베도 × (하늘/땅 반구 환경광 + 해 × max(N·L, 0) × 그림자(세기 shadowStrength))
// 좌표: Unity (X 동, Y 위, Z 북) → three (X, Y, -Z)
import * as THREE from 'three';

const q = new URLSearchParams(location.search);
const num = (k, d) => (q.has(k) ? parseFloat(q.get(k)) : d);
const ART = '/My%20project/Assets/Art/KeyArt/';

const lin = (c) => new THREE.Color().setRGB(c[0], c[1], c[2], THREE.SRGBColorSpace);   // sRGB → 리니어 Color
const linRaw = (c) => new THREE.Color().setRGB(c[0], c[1], c[2], THREE.LinearSRGBColorSpace);

async function main() {
  const look = await (await fetch(ART + 'KeyArtLook.json')).json();
  const W = num('w', 1280), H = num('h', 800);
  const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  renderer.setSize(W, H);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.NoToneMapping;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  document.body.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  scene.background = lin(look.fog.color);
  scene.fog = new THREE.Fog(lin(look.fog.color), look.fog.start, look.fog.end);

  // 조명: 반구 환경광 + 해 (그림자 받는 몫 / 안 받는 몫으로 나눠 그림자 세기를 흉내)
  const PI = Math.PI;
  scene.add(new THREE.HemisphereLight(linRaw(look.ambient.sky), linRaw(look.ambient.ground), q.has('noamb') ? 0 : PI));
  const sunCol = lin(look.sun.color);
  const p = THREE.MathUtils.degToRad(look.sun.pitch), y = THREE.MathUtils.degToRad(look.sun.yaw);
  const toSun = new THREE.Vector3(-Math.sin(y) * Math.cos(p), Math.sin(p), Math.cos(y) * Math.cos(p));  // three 좌표
  const target = new THREE.Vector3(num('x', 60), 0, -num('z', -352));
  const s = look.shadowStrength;
  const sunShadow = new THREE.DirectionalLight(sunCol, look.sun.intensity * s * PI);
  const sunFill = new THREE.DirectionalLight(sunCol, look.sun.intensity * (1 - s) * PI);
  for (const L of [sunShadow, sunFill]) {
    L.position.copy(target).addScaledVector(toSun, 800);
    L.target.position.copy(target);
    scene.add(L, L.target);
  }
  const dist = num('dist', look.camera.distance);
  const ext = Math.max(160, dist * 0.75);
  sunShadow.castShadow = true;
  Object.assign(sunShadow.shadow.camera, { left: -ext, right: ext, top: ext, bottom: -ext, near: 100, far: 2000 });
  sunShadow.shadow.mapSize.set(4096, 4096);
  sunShadow.shadow.bias = -0.0004;
  sunShadow.shadow.normalBias = 0.6;
  sunShadow.shadow.radius = 3;

  // 지면
  const texLoader = new THREE.TextureLoader();
  const loadTex = (url, srgb) => new Promise((res) => texLoader.load(url, (t) => { if (srgb) t.colorSpace = THREE.SRGBColorSpace; res(t); }));
  const [groundTex, detailTex] = await Promise.all([loadTex(ART + 'CampusGround.jpg', true), loadTex(ART + 'GrassDetail.png', false)]);
  groundTex.anisotropy = 8;
  detailTex.wrapS = detailTex.wrapT = THREE.RepeatWrapping;
  const groundMat = new THREE.MeshLambertMaterial({ map: groundTex });
  groundMat.onBeforeCompile = (sh) => {
    sh.uniforms.uDetail = { value: detailTex };
    sh.uniforms.uDetailTile = { value: look.grassDetail.tile };
    sh.uniforms.uDetailStrength = { value: look.grassDetail.strength };
    sh.vertexShader = sh.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vWPos;')
      .replace('#include <fog_vertex>', '#include <fog_vertex>\nvWPos = (modelMatrix * vec4(transformed, 1.0)).xyz;');
    sh.fragmentShader = sh.fragmentShader.replace('#include <common>', '#include <common>\nvarying vec3 vWPos;\nuniform sampler2D uDetail; uniform float uDetailTile, uDetailStrength;')
      .replace('#include <map_fragment>', `#include <map_fragment>
        float greenness = clamp((diffuseColor.g - max(diffuseColor.r, diffuseColor.b)) * 8.0, 0.0, 1.0);
        float d = texture2D(uDetail, vec2(vWPos.x, -vWPos.z) / uDetailTile).r;
        diffuseColor.rgb *= 1.0 + (d - 0.5) * uDetailStrength * greenness;`);
  };
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(1418, 1548), groundMat);
  ground.rotation.x = -PI / 2;
  ground.receiveShadow = true;
  scene.add(ground);
  const outer = new THREE.Mesh(new THREE.PlaneGeometry(9000, 9000), new THREE.MeshLambertMaterial({ color: lin(look.outerGrass) }));
  outer.rotation.x = -PI / 2;
  outer.position.y = -0.3;
  outer.receiveShadow = true;
  scene.add(outer);

  // 건물 외곽 (Unity CampusBuildings 와 같은 돌출) — 스타일별 재질
  const blds = await (await fetch('./data/buildings.json')).json();
  const byStyle = {};
  for (const b of blds) (byStyle[b.style] ||= []).push(b);
  for (const [style, list] of Object.entries(byStyle)) {
    const pos = [];
    for (const b of list) {
      const P = b.poly.map(([x, z]) => new THREE.Vector2(x, -z));
      const tris = THREE.ShapeUtils.triangulateShape(P, []);
      for (const t of tris) {
        const [a, c, d] = t.map((i) => P[i]);
        // 위를 보게 감기
        const cr = (c.x - a.x) * (d.y - a.y) - (c.y - a.y) * (d.x - a.x);
        const tri = cr > 0 ? [a, d, c] : [a, c, d];
        for (const v of tri) pos.push(v.x, b.h, v.y);
      }
      const n = P.length;
      const area = THREE.ShapeUtils.area(P);
      for (let i = 0; i < n; i++) {
        let a = P[i], c = P[(i + 1) % n];
        // 바깥을 보게: 변의 오른쪽 법선 (x, z) = (dz, -dx) 가 다각형 바깥이 되도록
        if (area > 0) [a, c] = [c, a];
        pos.push(a.x, b.min, a.y, c.x, b.min, c.y, c.x, b.h, c.y, a.x, b.min, a.y, c.x, b.h, c.y, a.x, b.h, a.y);
      }
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.computeVertexNormals();
    const mesh = new THREE.Mesh(g, buildingMaterial(look.styles[style] || look.styles.Default));
    mesh.castShadow = mesh.receiveShadow = true;
    scene.add(mesh);
  }

  // 키아트 지오메트리 (나무, 산울타리, 건물 디테일, 랜드마크)
  const buf = await (await fetch(ART + 'KeyArtGeometry.bytes')).arrayBuffer();
  const vcMat = new THREE.MeshLambertMaterial({ vertexColors: true });
  for (const g of await parseGeometry(buf)) {
    const m = new THREE.Mesh(g, vcMat);
    m.castShadow = m.receiveShadow = true;
    scene.add(m);
  }

  // 카메라: CameraFollow 와 같은 피치·요·거리
  const cam = new THREE.PerspectiveCamera(num('fov', look.camera.fov), W / H, 1, 6000);
  const cp = THREE.MathUtils.degToRad(num('pitch', look.camera.pitch)), cy = THREE.MathUtils.degToRad(num('yaw', 0));
  const fwd = new THREE.Vector3(Math.sin(cy) * Math.cos(cp), -Math.sin(cp), -Math.cos(cy) * Math.cos(cp));
  cam.position.copy(target).addScaledVector(fwd, -dist);
  cam.lookAt(target);
  renderer.render(scene, cam);
  window.__done = true;
}

function buildingMaterial(st) {
  const mat = new THREE.MeshLambertMaterial({ color: 0xffffff });
  mat.onBeforeCompile = (sh) => {
    const u = {
      uWall: lin(st.wall), uTrim: lin(st.trim), uWindow: lin(st.window), uRoof: lin(st.roof),
      uFloorH: st.floorHeight, uSpacing: st.spacing, uWinW: st.windowWidth, uWinH: st.windowHeight,
      uPilEvery: st.pilasterEvery, uBrick: st.brick,
    };
    for (const [k, v] of Object.entries(u)) sh.uniforms[k] = { value: v };
    sh.vertexShader = sh.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vWPos; varying vec3 vWN;')
      .replace('#include <fog_vertex>', '#include <fog_vertex>\nvWPos = (modelMatrix * vec4(transformed, 1.0)).xyz; vWN = normalize(mat3(modelMatrix) * objectNormal);');
    sh.fragmentShader = sh.fragmentShader.replace('#include <common>', '#include <common>\n' + BUILDING_GLSL)
      .replace('#include <map_fragment>', 'diffuseColor.rgb = kucaBuilding(vec3(vWPos.x, vWPos.y, -vWPos.z), normalize(vec3(vWN.x, vWN.y, -vWN.z)));');
  };
  return mat;
}

// StylizedBuilding.shader 의 KucaBuildingAlbedo 와 같은 무늬 (Unity 좌표)
const BUILDING_GLSL = `
varying vec3 vWPos; varying vec3 vWN;
uniform vec3 uWall, uTrim, uWindow, uRoof;
uniform float uFloorH, uSpacing, uWinW, uWinH, uPilEvery, uBrick;
vec3 kucaBuilding(vec3 p, vec3 n) {
  if (n.y > 0.6) {
    vec2 g = abs(fract(p.xz / 4.0) - 0.5);
    return uRoof * (1.0 - 0.05 * step(0.47, max(g.x, g.y)));
  }
  vec2 side = normalize(vec2(-n.z, n.x) + 1e-5);
  float u = dot(p.xz, side) / uSpacing;
  float v = p.y / uFloorH;
  float fu = fract(u), fv = fract(v);
  vec3 c = uWall;
  if (uBrick > 0.5) c *= 1.0 - 0.08 * step(fract(p.y / 0.32), 0.14);
  if (uPilEvery > 0.5) {
    float colI = floor(u + 0.5);
    float gap = abs(fract(u + 0.5) - 0.5);
    if (mod(abs(colI), uPilEvery) < 0.5 && gap < 0.17 && p.y > 0.9) c = mix(c, uTrim, 0.85);
  }
  if (fv < 0.06 && p.y > 2.0) c = mix(c, uTrim, 0.7);
  float hw = uWinW * 0.5, hh = uWinH * 0.5;
  float du = abs(fu - 0.5), dv = abs(fv - 0.55);
  bool above = p.y > 1.4;
  if (above && du < hw + 0.05 && dv < hh + 0.07) {
    c = uTrim * 0.96;
    if (du < hw && dv < hh) {
      float t = (fv - (0.55 - hh)) / (2.0 * hh);
      vec3 gl = uWindow * (0.78 + 0.34 * t);
      float streak = step(0.8, fract((u * uSpacing + p.y) * 0.11));
      c = mix(gl, vec3(0.80, 0.88, 0.96), 0.25 * streak);
    }
  }
  if (above && du < hw + 0.1 && fv > 0.55 - hh - 0.12 && fv < 0.55 - hh - 0.06) c = uTrim;
  return c * mix(0.84, 1.0, clamp(p.y / 4.0, 0.0, 1.0));
}`;

async function parseGeometry(buf) {
  // build_art.py write_bytes v2: 'KUCA' int32 2, 나머지 gzip
  const head = new DataView(buf, 0, 8);
  if (String.fromCharCode(...new Uint8Array(buf, 0, 4)) !== 'KUCA' || head.getInt32(4, true) !== 2) throw new Error('bad geometry file');
  const body = await new Response(new Blob([buf.slice(8)]).stream().pipeThrough(new DecompressionStream('gzip'))).arrayBuffer();
  const dv = new DataView(body);
  let o = 0;
  const count = dv.getInt32(o, true); o += 4;
  const out = [];
  const toLin = (c) => (c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
  const lut = new Float32Array(256).map((_, i) => toLin(i / 255));
  const UNIT = 0.02;
  for (let k = 0; k < count; k++) {
    const nl = dv.getInt32(o, true); o += 4 + nl;
    const ox = dv.getFloat32(o, true), oy = dv.getFloat32(o + 4, true), oz = dv.getFloat32(o + 8, true); o += 12;
    const vc = dv.getInt32(o, true); o += 4;
    const src = new Int16Array(body.slice(o, o + vc * 6)); o += vc * 6;
    const col = new Uint8Array(body, o, vc * 4); o += vc * 4;
    const pos = new Float32Array(vc * 3), c = new Float32Array(vc * 3);
    for (let t = 0; t < vc; t += 3) {
      const order = [t, t + 2, t + 1];   // z 를 뒤집으니 감는 방향도 뒤집는다
      for (let j = 0; j < 3; j++) {
        const s = order[j], d = t + j;
        pos[d * 3] = ox + src[s * 3] * UNIT; pos[d * 3 + 1] = oy + src[s * 3 + 1] * UNIT; pos[d * 3 + 2] = -(oz + src[s * 3 + 2] * UNIT);
        c[d * 3] = lut[col[s * 4]]; c[d * 3 + 1] = lut[col[s * 4 + 1]]; c[d * 3 + 2] = lut[col[s * 4 + 2]];
      }
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('color', new THREE.BufferAttribute(c, 3));
    g.computeVertexNormals();
    out.push(g);
  }
  return out;
}

main().catch((e) => { document.title = 'ERROR ' + e.message; console.error(e); window.__error = String(e.stack || e); });
