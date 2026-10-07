// 키아트 지도 미리보기. Unity 셰이더(KUCAStylizedLighting.hlsl, StylizedBuilding/StylizedGround/VertexColorLit)와
// 같은 '햇살' 조명: 감싸는 빛 + 푸른 그늘 + 따뜻한 윗면 + 가장자리 빛 + 채도 + 화면 햇살 번짐.
// 좌표: Unity (X 동, Y 위, Z 북) → three (X, Y, -Z)
import * as THREE from 'three';

const q = new URLSearchParams(location.search);
const num = (k, d) => (q.has(k) ? parseFloat(q.get(k)) : d);
const ART = '/My%20project/Assets/Art/KeyArt/';

const lin = (c) => new THREE.Color().setRGB(c[0], c[1], c[2], THREE.SRGBColorSpace);   // sRGB → 리니어
const raw = (c) => new THREE.Vector3(c[0], c[1], c[2]);                                // 리니어 배율 그대로

async function main() {
  const look = await (await fetch(ART + 'KeyArtLook.json')).json();
  // ?mode=day|sunset|night : KeyArtLook.json modes 로 덮어쓰기 (Unity KeyArtLook 과 같은 규칙)
  const mode = q.get('mode') || 'day';
  const md = (look.modes && look.modes[mode]) || {};
  for (const k of ['sun', 'ambient', 'sunny', 'fog']) if (md[k]) look[k] = { ...look[k], ...md[k] };
  if (md.shadowStrength !== undefined) look.shadowStrength = md.shadowStrength;
  const night = md.night || 0;
  const skyTint = (md.sky && md.sky.tint) || look.fog.color;
  // ?season=spring|summer|autumn|winter (KeyArtLook.json seasons)
  const season = (look.seasons || {})[q.get('season') || 'spring'] || { leaf: [1, 1, 1], autumn: 0, blossom: [1, 1, 1, 0], grass: [1, 1, 1], snow: 0 };
  const W = num('w', 1280), H = num('h', 800);
  const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  renderer.setSize(W, H);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.NoToneMapping;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  document.body.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  scene.background = lin(look.fog.color).lerp(lin(skyTint), 0.5);
  scene.fog = new THREE.Fog(lin(look.fog.color), look.fog.start, look.fog.end);

  // 해 하나 (그림자). 환경광·그늘색 등은 셰이더 uniform
  const p = THREE.MathUtils.degToRad(look.sun.pitch), y = THREE.MathUtils.degToRad(look.sun.yaw);
  const toSun = new THREE.Vector3(-Math.sin(y) * Math.cos(p), Math.sin(p), Math.cos(y) * Math.cos(p));
  const target = new THREE.Vector3(num('x', 60), 0, -num('z', -352));
  const sun = new THREE.DirectionalLight(lin(look.sun.color), look.sun.intensity);
  sun.position.copy(target).addScaledVector(toSun, 800);
  sun.target.position.copy(target);
  scene.add(sun, sun.target);
  const dist = num('dist', look.camera.distance);
  const ext = Math.max(160, dist * 0.75);
  sun.castShadow = true;
  Object.assign(sun.shadow.camera, { left: -ext, right: ext, top: ext, bottom: -ext, near: 100, far: 2000 });
  sun.shadow.mapSize.set(4096, 4096);
  sun.shadow.bias = -0.0004;
  sun.shadow.normalBias = 0.6;
  sun.shadow.radius = 4;

  const texLoader = new THREE.TextureLoader();
  const loadTex = (url, srgb) => new Promise((res) => texLoader.load(url, (t) => { if (srgb) t.colorSpace = THREE.SRGBColorSpace; res(t); }));
  const [groundTex, detailTex, lightsTex] = await Promise.all([loadTex(ART + 'CampusGround.jpg', true), loadTex(ART + 'GrassDetail.png', false), loadTex(ART + 'KeyArtLights.png', false)]);
  const S = look.sunny;
  const common = {
    uSkyAmb: { value: raw(look.ambient.sky) }, uGroundAmb: { value: raw(look.ambient.ground) },
    uShadowStrength: { value: look.shadowStrength }, uWrap: { value: S.wrap }, uShadowTint: { value: raw(S.shadowTint) },
    uWarmTop: { value: S.warmTop }, uRim: { value: S.rim }, uSat: { value: S.saturation },
    uWash: { value: S.wash }, uWashColor: { value: lin(S.washColor) }, uResolution: { value: new THREE.Vector2(W, H) },
    uTime: { value: 0 },
    uNight: { value: night }, uGlowColor: { value: lin(look.glow.color) }, uGlowStrength: { value: look.glow.strength },
    uLitWindows: { value: look.glow.litWindows },
    uLights: { value: lightsTex }, uGroundLight: { value: look.glow.groundLight || 0 },
    uSeasonLeaf: { value: raw(season.leaf) }, uSeasonBlossom: { value: new THREE.Vector4(...lin(season.blossom).toArray(), season.blossom[3]) },
    uSeasonGrass: { value: raw(season.grass) }, uAutumn: { value: season.autumn }, uSnow: { value: season.snow },
  };

  // 지면
  groundTex.anisotropy = 8;
  detailTex.wrapS = detailTex.wrapT = THREE.RepeatWrapping;
  const groundAlbedo = `
    float greenness = clamp((diffuseColor.g - max(diffuseColor.r, diffuseColor.b)) * 8.0, 0.0, 1.0);
    float dtl = texture2D(uDetail, vec2(vWPos.x, -vWPos.z) / uDetailTile).r;
    diffuseColor.rgb *= 1.0 + (dtl - 0.5) * uDetailStrength * greenness;
    diffuseColor.rgb = kucaSeasonGround(diffuseColor.rgb, vec3(vWPos.x, vWPos.y, -vWPos.z));
    diffuseColor.rgb = kucaWater(diffuseColor.rgb, vec3(vWPos.x, vWPos.y, -vWPos.z));
    kNightAmt = 1.0;`;
  const groundMat = sunny(new THREE.MeshLambertMaterial({ map: groundTex }), common,
    { uDetail: { value: detailTex }, uDetailTile: { value: look.grassDetail.tile }, uDetailStrength: { value: look.grassDetail.strength },
    },
    'uniform sampler2D uDetail; uniform float uDetailTile, uDetailStrength;', groundAlbedo, '#include <map_fragment>');
  const outer = new THREE.Mesh(new THREE.PlaneGeometry(9000, 9000), sunny(new THREE.MeshLambertMaterial({ color: lin(look.outerGrass) }), common));
  outer.rotation.x = -Math.PI / 2;
  outer.position.y = -0.3;
  outer.receiveShadow = true;
  scene.add(outer);

  // 지오메트리: Shell<스타일>_n 은 건물 외벽 재질, 나머지는 버텍스 색
  const buf = await (await fetch(ART + 'KeyArtGeometry.bytes')).arrayBuffer();
  const vcMat = sunny(new THREE.MeshLambertMaterial({ vertexColors: true }), common, {}, 'varying float vGlow;',
    `{ vec3 P = vec3(vWPos.x, vWPos.y, -vWPos.z);
       vec3 dn = normalize(cross(dFdx(vWPos), dFdy(vWPos)));
       diffuseColor.rgb = kucaSeasonFoliage(diffuseColor.rgb, P, vec3(dn.x, dn.y, -dn.z)); }
     kEmit = diffuseColor.rgb * uGlowColor * uGlowStrength * uNight * (1.0 - vGlow) * 2.0;
     kNightAmt = 0.6;`, '#include <color_fragment>',
    'attribute float glowMask; varying float vGlow;@@vGlow = glowMask;');
  const shellMats = {};
  for (const { name, g } of await parseGeometry(buf)) {
    const shell = name.match(/^Shell(\w+?)_\d+$/);
    let mat = vcMat;
    if (name.startsWith('Terrain')) {
      // 지형 메시: 지면 텍스처를 월드 좌표로 붙인다 (Unity StylizedGround 의 _WorldUV 와 같음)
      const P = g.getAttribute('position');
      const uv = new Float32Array(P.count * 2);
      for (let i = 0; i < P.count; i++) { uv[i * 2] = P.getX(i) / 1418 + 0.5; uv[i * 2 + 1] = -P.getZ(i) / 1548 + 0.5; }
      g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
      const t = new THREE.Mesh(g, groundMat);
      t.receiveShadow = true;
      scene.add(t);
      continue;
    }
    if (shell) {
      const st = shell[1];
      mat = shellMats[st] ||= buildingMaterial(look.styles[st] || look.styles.Default, look.buildingHeightScale || 1, common);
    }
    const m = new THREE.Mesh(g, mat);
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

// KUCAStylizedLighting.hlsl 와 같은 계산 (뷰 공간)
const SUNNY_GLSL = `
uniform vec3 uSkyAmb, uGroundAmb, uShadowTint, uWashColor;
uniform float uShadowStrength, uWrap, uWarmTop, uRim, uSat, uWash, uTime, uNight, uGlowStrength, uLitWindows;
uniform vec3 uGlowColor;
vec3 kEmit = vec3(0.0);   // 밤 불빛 (조명 뒤에 더함)
float kNightAmt = 0.0;    // 넓게 퍼지는 밤 빛을 받는 정도 (지면 1, 나무 0.6, 벽 0.35)
uniform sampler2D uLights;
uniform float uGroundLight, uAutumn, uSnow;
uniform vec3 uSeasonLeaf, uSeasonGrass;
uniform vec4 uSeasonBlossom;
vec3 kucaNightLight(vec3 albedo, vec3 p, float amount) {
  if (uNight <= 0.001 || amount <= 0.0) return vec3(0.0);
  float pool = texture2D(uLights, p.xz / vec2(1418.0, 1548.0) + 0.5).r;
  return uGlowColor * pool * uNight * uGroundLight * amount * (albedo * 1.3 + 0.06);
}
vec3 kucaSeasonFoliage(vec3 c, vec3 p, vec3 n) {
  float lum = dot(c, vec3(0.3, 0.6, 0.1));
  float green = clamp((c.g - max(c.r, c.b) * 1.05) * 8.0, 0.0, 1.0);
  float pink = clamp((c.r - c.g * 1.25) * 6.0, 0.0, 1.0) * clamp((c.b - c.g * 0.8) * 6.0, 0.0, 1.0);
  float leafy = smoothstep(0.26, 0.34, c.r / max(c.g, 0.01));
  float h = fract(sin(dot(floor(p.xz / 4.0), vec2(12.9898, 78.233))) * 43758.5453);
  vec3 maple = mix(vec3(0.80, 0.22, 0.05), vec3(0.95, 0.58, 0.06), h);
  maple = mix(maple, vec3(0.55, 0.10, 0.04), step(0.82, h));
  vec3 g2 = c * uSeasonLeaf;
  g2 = mix(g2, maple * lum * 2.4, uAutumn * leafy);
  c = mix(c, g2, green);
  c = mix(c, uSeasonBlossom.rgb * (0.7 + lum), pink * uSeasonBlossom.a);
  return mix(c, vec3(0.90, 0.94, 1.0), uSnow * smoothstep(0.35, 0.75, n.y));
}
vec3 kucaSeasonGround(vec3 c, vec3 p) {
  float green = clamp((c.g - max(c.r, c.b)) * 8.0, 0.0, 1.0);
  c = mix(c, c * uSeasonGrass, green);
  float water = clamp((c.b - max(c.r, c.g) * 0.85) * 5.0, 0.0, 1.0);
  float nse = sin(p.x * 0.05) * sin(p.z * 0.043) * 0.5 + 0.5;
  float snow = uSnow * clamp(green * 1.2 + 0.3, 0.0, 1.0) * smoothstep(0.15, 0.6, nse * 0.6 + uSnow * 0.7);
  c = mix(c, vec3(0.84, 0.88, 0.95), snow * (1.0 - water));
  return mix(c, vec3(0.70, 0.82, 0.92), uSnow * water * 0.6);
}
uniform vec2 uResolution;
varying vec3 vWPos;
vec3 kucaShade(vec3 albedo, vec3 N, vec3 L, vec3 sunCol, float atten, vec3 up, vec3 V) {
  float lit = clamp((dot(N, L) + uWrap) / (1.0 + uWrap), 0.0, 1.0);
  lit = lit * lit * (3.0 - 2.0 * lit);
  float sun = lit * mix(1.0 - uShadowStrength, 1.0, atten);
  float ny = dot(N, up);
  vec3 amb = mix(uGroundAmb, uSkyAmb, ny * 0.5 + 0.5) * mix(uShadowTint, vec3(1.0), sun);
  vec3 col = albedo * (amb + sunCol * sun);
  col += albedo * sunCol * uWarmTop * max(ny, 0.0) * max(ny, 0.0) * sun;
  float rim = pow(1.0 - clamp(dot(N, V), 0.0, 1.0), 3.0);
  col += vec3(1.0, 0.96, 0.88) * uRim * rim * (0.35 + 0.65 * sun) * 0.5;
  float l = dot(col, vec3(0.299, 0.587, 0.114));
  return max(mix(vec3(l), col, uSat), 0.0);
}
vec3 kucaSunWash(vec3 col, vec2 uv) {
  float d = clamp(1.0 - length((uv - vec2(0.0, 1.0)) * vec2(0.75, 1.0)), 0.0, 1.0);
  return col + uWashColor * uWash * d * d;
}
vec3 kucaWater(vec3 albedo, vec3 p) {
  float w = clamp((albedo.b - max(albedo.r, albedo.g) * 0.85) * 5.0, 0.0, 1.0);
  float s = sin(dot(p.xz, vec2(0.9, 1.3)) * 1.7 + uTime * 1.5) * sin(dot(p.xz, vec2(-1.1, 0.7)) * 2.1 - uTime);
  return albedo + vec3(0.9, 0.97, 1.0) * pow(clamp(s, 0.0, 1.0), 8.0) * 0.45 * w;
}`;

const SUNNY_LIGHT = `
{
  vec3 Nv = normalize(normal);
  float atten = 1.0;
  #if defined(USE_SHADOWMAP) && NUM_DIR_LIGHT_SHADOWS > 0
    DirectionalLightShadow dls = directionalLightShadows[0];
    atten = getShadow(directionalShadowMap[0], dls.shadowMapSize, dls.shadowBias, dls.shadowRadius, vDirectionalShadowCoord[0]);
  #endif
  vec3 upV = normalize((viewMatrix * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
  outgoingLight = kucaShade(diffuseColor.rgb, Nv, directionalLights[0].direction, directionalLights[0].color, atten, upV, normalize(vViewPosition));
  outgoingLight = kucaSunWash(outgoingLight, gl_FragCoord.xy / uResolution) + kEmit
    + kucaNightLight(diffuseColor.rgb, vec3(vWPos.x, vWPos.y, -vWPos.z), kNightAmt);
}
#include <opaque_fragment>`;

/** MeshLambertMaterial 의 조명을 햇살 조명으로 바꾼다. albedoCode 는 replaceChunk 뒤에 붙는다. */
let sunnyId = 0;
function sunny(mat, common, extraUniforms = {}, extraDecl = '', albedoCode = '', replaceChunk = null, vertexExtra = '') {
  // three 는 onBeforeCompile 소스 문자열로 셰이더를 캐시하므로 재질마다 키를 달리 준다
  const key = 'sunny' + (sunnyId++) + albedoCode.length;
  mat.customProgramCacheKey = () => key;
  mat.onBeforeCompile = (sh) => {
    Object.assign(sh.uniforms, common, extraUniforms);
    sh.vertexShader = sh.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 vWPos;\n' + vertexExtra.split('@@')[0])
      .replace('#include <fog_vertex>', '#include <fog_vertex>\nvWPos = (modelMatrix * vec4(transformed, 1.0)).xyz;\n' + (vertexExtra.split('@@')[1] || ''));
    let f = sh.fragmentShader.replace('#include <common>', '#include <common>\n' + SUNNY_GLSL + '\n' + extraDecl);
    if (replaceChunk) f = f.replace(replaceChunk, replaceChunk.startsWith('#include') ? replaceChunk + '\n' + albedoCode : albedoCode);
    sh.fragmentShader = f.replace('#include <opaque_fragment>', SUNNY_LIGHT);
  };
  return mat;
}

function buildingMaterial(st, heightScale, common) {
  const u = {
    uWall: { value: lin(st.wall) }, uTrim: { value: lin(st.trim) }, uWindow: { value: lin(st.window) }, uRoof: { value: lin(st.roof) },
    uFloorH: { value: st.floorHeight * heightScale }, uSpacing: { value: st.spacing }, uWinW: { value: st.windowWidth },
    uWinH: { value: st.windowHeight }, uPilEvery: { value: st.pilasterEvery }, uBrick: { value: st.brick }, uArch: { value: st.arch || 0 },
  };
  return sunny(new THREE.MeshLambertMaterial({ color: 0xffffff }), common, u, BUILDING_GLSL,
    'diffuseColor.rgb = kucaBuilding(vec3(vWPos.x, vWPos.y, -vWPos.z), normalize(vec3(vWN.x, vWN.y, -vWN.z)), vWall); kNightAmt = 0.35;',
    '#include <map_fragment>',
    'attribute vec2 wallUV; attribute vec3 roofTint; varying vec2 vWall; varying vec3 vWN; varying vec3 vRoofTint;@@vWall = wallUV; vRoofTint = roofTint; vWN = normalize(mat3(modelMatrix) * objectNormal);');
}

// StylizedBuilding.shader 의 BuildingAlbedo 와 같은 무늬 (Unity 좌표, wall = (벽 위 위치, 벽 길이) m)
const BUILDING_GLSL = `
varying vec2 vWall; varying vec3 vWN; varying vec3 vRoofTint;
uniform vec3 uWall, uTrim, uWindow, uRoof;
uniform float uFloorH, uSpacing, uWinW, uWinH, uPilEvery, uBrick, uArch;
float sdRoundBox(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
vec3 kucaBuilding(vec3 p, vec3 n, vec2 wall) {
  float ao = mix(0.86, 1.0, clamp(p.y / 4.0, 0.0, 1.0));
  if (n.y > 0.6) {
    vec2 g = abs(fract(p.xz / 5.0) - 0.5);
    vec3 roof = uRoof * vRoofTint * 1.1 * (1.0 - 0.035 * smoothstep(0.46, 0.49, max(g.x, g.y)));
    return mix(roof, vec3(0.90, 0.94, 1.0), uSnow * 0.9);
  }
  vec3 c = uWall * mix(0.93, 1.04, clamp(p.y / 24.0, 0.0, 1.0));
  if (uBrick > 0.5) c *= 1.0 - 0.06 * step(fract(p.y / 0.34), 0.12);
  float fv = fract(p.y / uFloorH), fl = floor(p.y / uFloorH);
  if (fv < 0.05 && p.y > 2.0) c = mix(c, uTrim, 0.55);
  float wl = wall.y, wu = wall.x;
  if (wl < 0.5) { vec2 side = normalize(vec2(-n.z, n.x) + 1e-5); wu = dot(p.xz, side); wl = 100000.0; }
  bool infinite = wl > 99999.0;
  // 벽 버텍스 색 R = 벽 종류: 1 보통 창, 0.5 커튼월(전면 유리), 0 석재 + 세로 슬릿 창 (멀티미디어·글로벌관)
  float wmode = vRoofTint.r;
  if (wmode < 0.75 && !infinite) {
    if (p.y < 1.2) return c * ao;
    if (wmode < 0.25) {
      vec3 s = mix(uWall, vec3(0.95, 0.91, 0.84), 0.75) * mix(0.95, 1.03, clamp(p.y / 24.0, 0.0, 1.0));
      float sp = 3.2, nS = max(1.0, floor((wl - 3.0) / sp));
      float lu2 = (wu - (wl - nS * sp) * 0.5) / sp;
      if (lu2 < 0.0 || lu2 >= nS) return s * ao;
      float ci = floor(lu2), qx = (fract(lu2) - 0.5) * sp;
      vec3 res = s * ao;
      if (abs(qx) < 0.42 && fv > 0.1) {
        float h = fract(sin(dot(vec2(ci, fl), vec2(12.9898, 78.233))) * 43758.5453);
        vec3 gl = mix(uWindow * 0.7, mix(uWindow, vec3(0.88, 0.95, 1.0), 0.5), fv) * mix(1.0, 0.45, uNight);
        if (h < uLitWindows) kEmit = uGlowColor * uGlowStrength * uNight * 0.8;
        res = gl;
      } else if (abs(qx) < 0.58) res = uTrim;
      float fadeS = clamp(fwidth(qx) * 3.0 - 0.6, 0.0, 1.0);
      kEmit = mix(kEmit, uGlowColor * uGlowStrength * uNight * uLitWindows * 0.25, fadeS);
      return mix(res, mix(s * ao, uWindow, 0.25), fadeS);
    }
    if (min(wu, wl - wu) < 0.7) return uTrim * ao;
    float mu = fract(wu / 1.6), cg = floor(wu / 1.6);
    float hg = fract(sin(dot(vec2(cg, fl), vec2(12.9898, 78.233))) * 43758.5453);
    vec3 gl = mix(uWindow * 0.62, mix(uWindow, vec3(0.86, 0.94, 1.0), 0.6), fv);
    gl = mix(gl, vec3(0.96, 0.99, 1.0), 0.22 * step(0.88, fract((wu * 0.5 + p.y) * 0.035)));
    gl *= mix(1.0, 0.45, uNight);
    if (hg < uLitWindows) kEmit = uGlowColor * uGlowStrength * uNight * (0.6 + 0.4 * fv);
    vec3 resG = gl;
    if (abs(mu - 0.5) > 0.46 || fv < 0.06) resG = uTrim * 0.94;
    float fadeG = clamp(fwidth(wu) * 2.0 - 0.6, 0.0, 1.0);
    kEmit = mix(kEmit, uGlowColor * uGlowStrength * uNight * uLitWindows * 0.7, fadeG);
    return mix(resG, mix(gl, uTrim, 0.15), fadeG);
  }
  float margin = infinite ? 0.0 : clamp(wl * 0.12, 1.8, 3.2);
  float nWin = infinite ? 100000.0 : floor((wl - 2.0 * margin) / uSpacing);
  if (nWin < 1.0 || p.y < 1.2) return c * ao;
  float lu = (wu - (infinite ? 0.0 : (wl - nWin * uSpacing) * 0.5)) / uSpacing;
  if (lu < 0.0 || lu >= nWin) return mix(c, uTrim, 0.22) * ao;                       // 모서리 기둥 (창 없음)
  float colI = floor(lu), fu = fract(lu);
  if (uPilEvery > 0.5 && mod(colI, uPilEvery) > uPilEvery - 1.5) return mix(c, uTrim, 0.3) * ao;   // 창 묶음 사이 벽
  vec2 qq = vec2((fu - 0.5) * uSpacing, (fv - 0.55) * uFloorH);
  vec2 hb = vec2(uWinW * uSpacing * 0.5, uWinH * uFloorH * 0.5);
  float d;
  if (uArch > 0.5 && qq.y > hb.y - hb.x) d = length(qq - vec2(0.0, hb.y - hb.x)) - hb.x;
  else d = sdRoundBox(qq, hb, min(hb.x, hb.y) * (uArch > 0.5 ? 0.2 : 0.42));
  vec3 res = c * ao;
  if (qq.y < -hb.y && qq.y > -hb.y - 0.24 && abs(qq.x) < hb.x + 0.22) res = uTrim * ao;    // 창턱
  else if (d < 0.0) {
    float t = clamp((qq.y + hb.y) / (2.0 * hb.y), 0.0, 1.0);
    float h = fract(sin(dot(vec2(colI, fl), vec2(12.9898, 78.233))) * 43758.5453);
    vec3 gl = mix(uWindow * 0.72, mix(uWindow, vec3(0.88, 0.95, 1.0), 0.55), t);
    gl = mix(gl, vec3(0.97, 0.99, 1.0), 0.4 * step(0.86, fract((qq.x * 0.7 + qq.y) * 0.45 + h)));
    if (h > 0.8 && t > 0.5) gl = mix(gl, vec3(1.0, 0.93, 0.84), 0.55);
    if (uWinW > 0.8 && abs(qq.x) < 0.06) gl = uTrim * 0.95;
    gl *= mix(1.0, 0.45, uNight);
    if (h < uLitWindows) kEmit = uGlowColor * uGlowStrength * uNight * (0.65 + 0.35 * t);
    res = gl;
  }
  else if (d < 0.16) res = uTrim;
  // 멀리서 창이 몇 픽셀보다 작아지면 평균색으로 (지글거림 방지)
  float px = max(fwidth(qq.x), fwidth(qq.y));
  vec3 avg = mix(c * ao, mix(uWindow, uTrim, 0.35), clamp(uWinW * uWinH * 1.4, 0.0, 0.7));
  float fade = clamp(px * 3.0 - 0.6, 0.0, 1.0);
  kEmit = mix(kEmit, uGlowColor * uGlowStrength * uNight * uLitWindows * clamp(uWinW * uWinH * 1.4, 0.0, 0.7), fade);
  return mix(res, avg, fade);
}`;

async function parseGeometry(buf) {
  // build_art.py write_bytes v3: 'KUCA' int32 3, 나머지 gzip
  const head = new DataView(buf, 0, 8);
  if (String.fromCharCode(...new Uint8Array(buf, 0, 4)) !== 'KUCA' || head.getInt32(4, true) !== 3) throw new Error('bad geometry file');
  const body = await new Response(new Blob([buf.slice(8)]).stream().pipeThrough(new DecompressionStream('gzip'))).arrayBuffer();
  const dv = new DataView(body);
  let o = 0;
  const count = dv.getInt32(o, true); o += 4;
  const out = [];
  const toLin = (c) => (c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
  const lut = new Float32Array(256).map((_, i) => toLin(i / 255));
  const UNIT = 0.02;
  for (let k = 0; k < count; k++) {
    const nl = dv.getInt32(o, true); o += 4;
    const name = new TextDecoder().decode(new Uint8Array(body, o, nl)); o += nl;
    const ox = dv.getFloat32(o, true), oy = dv.getFloat32(o + 4, true), oz = dv.getFloat32(o + 8, true); o += 12;
    const vc = dv.getInt32(o, true); o += 4;
    const flags = dv.getInt32(o, true); o += 4;
    const src = new Int16Array(body.slice(o, o + vc * 6)); o += vc * 6;
    const col = new Uint8Array(body, o, vc * 4); o += vc * 4;
    let aux = null;
    if (flags & 1) { aux = new Uint16Array(body.slice(o, o + vc * 4)); o += vc * 4; }
    const pos = new Float32Array(vc * 3), c = new Float32Array(vc * 3), wuv = new Float32Array(vc * 2), tint = new Float32Array(vc * 3), glow = new Float32Array(vc);
    for (let t = 0; t < vc; t += 3) {
      const order = [t, t + 2, t + 1];   // z 를 뒤집으니 감는 방향도 뒤집는다
      for (let j = 0; j < 3; j++) {
        const s = order[j], d = t + j;
        pos[d * 3] = ox + src[s * 3] * UNIT; pos[d * 3 + 1] = oy + src[s * 3 + 1] * UNIT; pos[d * 3 + 2] = -(oz + src[s * 3 + 2] * UNIT);
        c[d * 3] = lut[col[s * 4]]; c[d * 3 + 1] = lut[col[s * 4 + 1]]; c[d * 3 + 2] = lut[col[s * 4 + 2]];
        glow[d] = col[s * 4 + 3] / 255;
        tint[d * 3] = col[s * 4] / 255; tint[d * 3 + 1] = col[s * 4 + 1] / 255; tint[d * 3 + 2] = col[s * 4 + 2] / 255;   // 지붕 파스텔 (그대로의 값)
        if (aux) { wuv[d * 2] = aux[s * 2] * 0.1; wuv[d * 2 + 1] = aux[s * 2 + 1] * 0.1; }
      }
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('color', new THREE.BufferAttribute(c, 3));
    g.setAttribute('wallUV', new THREE.BufferAttribute(wuv, 2));
    g.setAttribute('roofTint', new THREE.BufferAttribute(tint, 3));
    g.setAttribute('glowMask', new THREE.BufferAttribute(glow, 1));
    g.computeVertexNormals();
    out.push({ name, g });
  }
  return out;
}

main().catch((e) => { document.title = 'ERROR ' + e.message; console.error(e); window.__error = String(e.stack || e); });
