// Isolated component checks only: no database, credentials, or application APIs.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { dirname, extname, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const assets = resolve(here, '../../src/ZuLms.Web/wwwroot');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.png': 'image/png' };
createServer(async (req, res) => {
  const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
  const root = pathname.startsWith('/assets/') ? assets : here;
  const relative = root === assets ? pathname.slice('/assets/'.length) : pathname === '/' ? 'regression.html' : pathname.slice(1);
  const path = resolve(root, relative);
  if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
  try {
    let body = await readFile(path);
    if (path === resolve(here, 'lectures.html')) {
      // Test-only rendering of the actual page markup, not Razor/authentication.
      const razor = await readFile(resolve(assets, '../Pages/Instructor/Lectures.cshtml'), 'utf8');
      const markup = razor.slice(razor.indexOf('<div class="container'), razor.indexOf('@section Scripts'))
        .replaceAll('@RouteData.Values["courseId"]', '7')
        .replaceAll('@T["Lectures.ValidationError"]', 'تعذّر حفظ المحاضرة. راجع الحقول الموضّحة أدناه.')
        .replaceAll('@T["Lectures.Error"]', 'تعذّر إكمال الطلب. أعد المحاولة.');
      body = Buffer.from(body.toString().replace('<!-- LECTURE_PAGE -->', markup));
    }
    res.writeHead(200, { 'Content-Type': types[extname(path)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    res.end(body);
  } catch { res.writeHead(404).end(); }
}).listen(5286, '127.0.0.1', () => console.log('Component regression fixture: http://127.0.0.1:5286 (SIMULATED-UI, not application acceptance)'));
