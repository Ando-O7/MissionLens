// Drives the spatial mock in headless Chrome over the DevTools protocol: real mouse and key events,
// state assertions, screenshots. No dependencies (Node 22+ for the global WebSocket).
//   node experiments/spatial/drive.mjs <page-url> <out-dir> <tmp-profile-dir>
import { spawn } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
const [url, outDir, profile] = process.argv.slice(2), port = 9333, sleep = ms => new Promise(r => setTimeout(r, ms));
mkdirSync(outDir, { recursive: true });
const chrome = spawn('/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', ['--headless=new', `--remote-debugging-port=${port}`, '--enable-unsafe-swiftshader',
  '--hide-scrollbars', '--window-size=1600,1000', `--user-data-dir=${profile}`, 'about:blank'], { stdio: 'ignore' });
let page; for (let i = 0; i < 60 && !page; i++) { await sleep(250); try { page = (await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t => t.type === 'page'); } catch {} }
const ws = new WebSocket(page.webSocketDebuggerUrl); await new Promise(r => ws.onopen = r);
let seq = 0; const pending = new Map();
ws.onmessage = e => { const m = JSON.parse(e.data); if (m.id && pending.has(m.id)) { pending.get(m.id)(m.result ?? m.error); pending.delete(m.id); } };
const send = (method, params = {}) => new Promise(res => { const id = ++seq; pending.set(id, res); ws.send(JSON.stringify({ id, method, params })); });
const ev = async expr => (await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true })).result?.value;
const state = () => ev('window.__ml && window.__ml.state()');
const waitFor = async (expr, timeout = 180000) => { const t0 = Date.now(); while (Date.now() - t0 < timeout) { if (await ev(expr)) return true; await sleep(300); } return false; };
const idle = () => waitFor('window.__ml && !window.__ml.state().busy');
const mouse = (type, x, y) => send('Input.dispatchMouseEvent', { type, x, y, button: 'left', buttons: type === 'mouseReleased' ? 0 : 1, clickCount: 1 });
const click = async ([x, y]) => { await mouse('mouseMoved', x, y); await mouse('mousePressed', x, y); await sleep(60); await mouse('mouseReleased', x, y); await sleep(250); };
const clickBtn = async label => { const p = await ev(`window.__ml.btn(${JSON.stringify(label)})`); if (!p) { results.push(['FAIL', `button "${label}" not found; have ${JSON.stringify(await ev('window.__ml.labels()'))}`]); return; } await click(p); };
const shot = async name => { const r = await send('Page.captureScreenshot', { format: 'jpeg', quality: 84 }); writeFileSync(`${outDir}/${name}.jpg`, Buffer.from(r.data, 'base64')); };
const results = []; const check = (name, ok, detail = '') => results.push([ok ? 'ok  ' : 'FAIL', name + (detail ? ' — ' + detail : '')]);

await send('Page.enable'); await send('Runtime.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 1000, deviceScaleFactor: 1, mobile: false });
await send('Page.navigate', { url });
check('page loads and exposes state', await waitFor('window.__ml && window.__ml.state().frames > 3'));
const errText = () => ev("document.getElementById('err').textContent");

let s = await state(); check('starts on the table at BASE', s.stage === 'table' && s.here === 'BASE', JSON.stringify(s));
await clickBtn('row:B'); s = await state(); check('clicking a station row selects it', s.sel === 'B', s.sel);
await clickBtn('Slope layer'); await sleep(1500); s = await state(); check('slope layer turns on', s.slope > .5, 'opacity ' + s.slope);
await shot('click_1_table_slope');
const f0 = (await state()).frames; await sleep(3000); const fps = ((await state()).frames - f0) / 3; check('renders continuously', fps > 1, fps.toFixed(1) + ' frames/s in this software renderer');
const h0 = (await state()).hdg; await mouse('mouseMoved', 900, 300); await mouse('mousePressed', 900, 300); for (let i = 1; i <= 8; i++) { await mouse('mouseMoved', 900 - i * 25, 300); await sleep(30); } await mouse('mouseReleased', 700, 300);
s = await state(); check('dragging turns the head', Math.abs(s.hdg - h0) > 3, `${h0} -> ${s.hdg}`);
await clickBtn('Slope layer'); await clickBtn('Walk the terrain'); await idle(); s = await state(); check('"Walk the terrain" enters the model at 1:25 at the selected station', s.stage === 'giant' && s.sel === 'B' && Math.abs(s.S - 1 / 25) < 1e-6 && s.room < .2, JSON.stringify(s));
await shot('click_2_giant');
const a0 = [s.ax, s.az]; await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'w', code: 'KeyW', text: 'w' }); await sleep(2500); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'w', code: 'KeyW' });
s = await state(); check('W walks across the terrain', Math.hypot(s.ax - a0[0], s.az - a0[1]) > 2, `moved ${Math.hypot(s.ax - a0[0], s.az - a0[1]).toFixed(0)} m`);
await clickBtn('True scale'); await idle(); s = await state(); check('"True scale" goes to 1:1', s.stage === 'walk' && s.S === 1, JSON.stringify(s));
await shot('click_3_walk');
await clickBtn('Surface image at'); await idle(); await sleep(2500); s = await state(); check('"Surface image" shows the panorama at the nearest station', s.stage === 'field' && s.pano > .8, JSON.stringify(s));
await shot('click_4_field');
await clickBtn('Table'); await idle(); await sleep(2000); s = await state(); check('"Table" returns to the room', s.stage === 'table' && s.room > .8 && s.pano < .2, JSON.stringify(s));
await clickBtn('row:BASE'); await clickBtn('Stand at'); await idle(); await sleep(2000); s = await state(); check('"Stand at BASE" goes straight to the surface image', s.stage === 'field' && s.here === 'BASE' && s.pano > .8, JSON.stringify(s));
await shot('click_5_field_base');
await clickBtn('Walk to'); await idle(); await sleep(2500); s = await state(); check('"Walk to A" travels the route and ends the Approach task', s.stage === 'field' && s.here === 'A' && s.taskIndex === 1 && s.pano > .8, JSON.stringify(s));
await shot('click_6_arrived_A');
await clickBtn('check:0'); await clickBtn('check:1'); s = await state(); check('checklist items tick', s.done.length === 2, JSON.stringify(s.done));
await clickBtn('Complete task'); s = await state(); check('"Complete task" advances the timeline', s.taskIndex === 2, 'task ' + s.taskIndex);
const pin = await ev("window.__ml.pin('B')"); if (pin) { await click(pin); await idle(); await sleep(2500); s = await state(); check('pinching a station label walks there', s.here === 'B' && s.stage === 'field', JSON.stringify(s)); await shot('click_7_arrived_B'); } else check('station label B is on screen to click', false);
const err = await errText(); check('no script errors', !err, err);
for (const [k, v] of results) console.log(k, v);
console.log(results.every(r => r[0].trim() === 'ok') ? 'ALL PASS' : 'SOME FAILED');
ws.close(); chrome.kill();
