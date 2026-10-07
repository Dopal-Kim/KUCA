// 수집 동물 미리보기: CreatureMeshes.bytes 를 읽어 줄 세워 그린다 (?yaw=0&ids=a,b&cols=4)
import * as THREE from 'three';
import { mergeVertices } from 'three/addons/utils/BufferGeometryUtils.js';

const q = new URLSearchParams(location.search);
const W = +(q.get('w') || 1600), H = +(q.get('h') || 900);
const yaw = +(q.get('yaw') || 0) * Math.PI / 180;
const cols = +(q.get('cols') || 4);
const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
renderer.setSize(W, H);
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.shadowMap.enabled = true;
document.body.appendChild(renderer.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(0xf4f1ea);
scene.add(new THREE.HemisphereLight(0xeaf2ff, 0xd8cdb8, 1.4));
const sun = new THREE.DirectionalLight(0xfff1dc, 2.2);
sun.position.set(-30, 60, 40);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
Object.assign(sun.shadow.camera, { left: -60, right: 60, top: 60, bottom: -60 });
scene.add(sun);

const toLin = (c) => (c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4));
async function load() {
  const buf = await (await fetch('../../../My project/Assets/Resources/KUCA/CreatureMeshes.bytes')).arrayBuffer();
  const body = await new Response(new Blob([buf.slice(8)]).stream().pipeThrough(new DecompressionStream('gzip'))).arrayBuffer();
  const dv = new DataView(body);
  let o = 0;
  const n = dv.getInt32(o, true); o += 4;
  const out = {};
  for (let k = 0; k < n; k++) {
    const nl = dv.getInt32(o, true); o += 4;
    const name = new TextDecoder().decode(new Uint8Array(body, o, nl)); o += nl;
    const ox = dv.getFloat32(o, true), oy = dv.getFloat32(o + 4, true), oz = dv.getFloat32(o + 8, true); o += 12;
    const vc = dv.getInt32(o, true); o += 4;
    const flags = dv.getInt32(o, true); o += 4;
    const src = new Int16Array(body.slice(o, o + vc * 6)); o += vc * 6;
    const col = new Uint8Array(body, o, vc * 4); o += vc * 4;
    if (flags & 1) o += vc * 4;
    const pos = new Float32Array(vc * 3), c = new Float32Array(vc * 3);
    for (let i = 0; i < vc; i++) {
      pos[i * 3] = ox + src[i * 3] * 0.02; pos[i * 3 + 1] = oy + src[i * 3 + 1] * 0.02; pos[i * 3 + 2] = oz + src[i * 3 + 2] * 0.02;
      for (let j = 0; j < 3; j++) c[i * 3 + j] = toLin(col[i * 4 + j] / 255);
    }
    let g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('color', new THREE.BufferAttribute(c, 3));
    g = mergeVertices(g, 1e-3);          // 같은 위치·색 정점 합치기 → 부드러운 법선 (Unity CreatureLibrary 와 같음)
    g.computeVertexNormals();
    out[name.replace('Creature_', '')] = g;
  }
  return out;
}

const geos = await load();
const ids = (q.get('ids') || Object.keys(geos).join(',')).split(',');
const mat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.62, metalness: 0.0 });
const sp = 12;
ids.forEach((id, i) => {
  const m = new THREE.Mesh(geos[id], mat);
  m.castShadow = m.receiveShadow = true;
  const cx = (i % cols - (Math.min(cols, ids.length) - 1) / 2) * sp;
  const cz = Math.floor(i / cols) * sp * 1.2;
  m.position.set(cx, -Math.floor(i / cols) * 11, 0);
  m.rotation.y = yaw;
  scene.add(m);
});
const rows = Math.ceil(ids.length / cols);
const ground = new THREE.Mesh(new THREE.PlaneGeometry(400, 400), new THREE.ShadowMaterial({ opacity: 0.18 }));
ground.rotation.x = -Math.PI / 2; ground.receiveShadow = true; scene.add(ground);
const cam = new THREE.PerspectiveCamera(22, W / H, 1, 2000);
const span = Math.max(Math.min(cols, ids.length) * sp, rows * 12 * W / H);
const dist = span / (2 * Math.tan(11 * Math.PI / 180)) / (W / H) * 1.05;
const pitch = +(q.get('pitch') || 12) * Math.PI / 180;
const cy = 5 - (rows - 1) * 5.5;
cam.position.set(0, cy + Math.sin(pitch) * dist, Math.cos(pitch) * dist);
cam.lookAt(0, cy, 0);
renderer.render(scene, cam);
window.__done = true;
