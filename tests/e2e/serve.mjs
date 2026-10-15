// Minimal static file server for the e2e tests and the renderer harness.
// Serves the repository root (or --root) with correct MIME types, including the
// Blazor WebAssembly payload (.wasm/.dll/.dat/.blat/.br/.gz), JSON atlases and TTF fonts.
//
//   node tests/e2e/serve.mjs [--port 8321] [--root /path]
//   import { startServer } from './serve.mjs'; const srv = await startServer({ port: 0 });

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
export const REPO_ROOT = path.resolve(here, '..', '..');

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.htm': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.wasm': 'application/wasm',
  '.ttf': 'font/ttf',
  '.otf': 'font/otf',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.webp': 'image/webp',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.txt': 'text/plain; charset=utf-8',
  '.md': 'text/markdown; charset=utf-8',
  '.yaml': 'application/yaml',
  '.yml': 'application/yaml',
  '.xml': 'application/xml',
  '.dll': 'application/octet-stream',
  '.pdb': 'application/octet-stream',
  '.dat': 'application/octet-stream',
  '.blat': 'application/octet-stream',
  '.webmanifest': 'application/manifest+json',
  '.mp3': 'audio/mpeg',
  '.ogg': 'audio/ogg',
  '.wav': 'audio/wav'
};

function contentType(filePath) {
  let ext = path.extname(filePath).toLowerCase();
  const encodings = { '.br': 'br', '.gz': 'gzip' };
  let encoding = null;
  if (encodings[ext]) {
    encoding = encodings[ext];
    ext = path.extname(filePath.slice(0, -ext.length)).toLowerCase();
  }
  return { type: MIME[ext] || 'application/octet-stream', encoding };
}

export function startServer({ port = 0, root = REPO_ROOT, host = '127.0.0.1', quiet = true } = {}) {
  const rootDir = path.resolve(root);
  const server = http.createServer((req, res) => {
    const method = req.method || 'GET';
    if (method !== 'GET' && method !== 'HEAD') {
      res.writeHead(405, { Allow: 'GET, HEAD' });
      res.end();
      return;
    }
    let pathname;
    try {
      pathname = decodeURIComponent(new URL(req.url, `http://${req.headers.host || 'localhost'}`).pathname);
    } catch {
      res.writeHead(400);
      res.end('bad request');
      return;
    }
    let filePath = path.resolve(rootDir, '.' + path.posix.normalize('/' + pathname));
    if (filePath !== rootDir && !filePath.startsWith(rootDir + path.sep)) {
      res.writeHead(403);
      res.end('forbidden');
      return;
    }
    fs.stat(filePath, (err, stat) => {
      if (!err && stat.isDirectory()) {
        if (!pathname.endsWith('/')) {
          res.writeHead(301, { Location: pathname + '/' });
          res.end();
          return;
        }
        filePath = path.join(filePath, 'index.html');
        fs.stat(filePath, (err2, stat2) => serveFile(err2, stat2));
        return;
      }
      serveFile(err, stat);
    });

    function serveFile(err, stat) {
      if (err || !stat || !stat.isFile()) {
        res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store' });
        res.end('not found: ' + pathname);
        if (!quiet) { console.log(`404 ${pathname}`); }
        return;
      }
      const { type, encoding } = contentType(filePath);
      const headers = {
        'Content-Type': type,
        'Content-Length': stat.size,
        'Cache-Control': 'no-store'
      };
      if (encoding) { headers['Content-Encoding'] = encoding; }
      res.writeHead(200, headers);
      if (method === 'HEAD') {
        res.end();
        return;
      }
      fs.createReadStream(filePath).on('error', () => res.destroy()).pipe(res);
      if (!quiet) { console.log(`200 ${pathname}`); }
    }
  });

  return new Promise((resolve, reject) => {
    server.on('error', reject);
    server.listen(port, host, () => {
      const address = server.address();
      const actualPort = typeof address === 'object' && address ? address.port : port;
      const url = `http://${host}:${actualPort}`;
      resolve({
        server,
        port: actualPort,
        url,
        root: rootDir,
        close: () => new Promise((done) => server.close(() => done()))
      });
    });
  });
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href;
if (isMain) {
  const args = process.argv.slice(2);
  const get = (flag, fallback) => {
    const i = args.indexOf(flag);
    return i >= 0 && args[i + 1] ? args[i + 1] : fallback;
  };
  const port = Number(get('--port', '8321'));
  const root = get('--root', REPO_ROOT);
  startServer({ port, root, quiet: false }).then((srv) => {
    console.log(`Serving ${srv.root} at ${srv.url}`);
    console.log(`Renderer harness: ${srv.url}/tools/renderer-harness/`);
  }).catch((err) => {
    console.error(err);
    process.exit(1);
  });
}
