import { createServer } from 'node:http';
import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { extname, resolve, sep } from 'node:path';

const root = resolve(process.argv[2] || 'work/mobile-build/wwwroot');
const port = Number(process.argv[3] || 4177);
const mime = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.json': 'application/json', '.webmanifest': 'application/manifest+json',
  '.wasm': 'application/wasm', '.png': 'image/png', '.txt': 'text/plain; charset=utf-8'
};

createServer(async (req, res) => {
  try {
    if (req.method !== 'GET' && req.method !== 'HEAD') {
      res.writeHead(405, { Allow: 'GET, HEAD' }).end();
      return;
    }
    let url = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    if (url === '/gymz') {
      res.writeHead(308, { Location: '/gymz/' }).end();
      return;
    }
    if (url.startsWith('/gymz/')) url = url.slice(5);
    let file = resolve(root, '.' + url);
    if (file !== root && !file.startsWith(root + sep)) throw new Error('path');
    let entry = await stat(file);
    if (entry.isDirectory()) {
      file = resolve(file, 'index.html');
      entry = await stat(file);
    }
    if (!entry.isFile()) throw new Error('file');
    res.writeHead(200, {
      'Content-Type': mime[extname(file)] || 'application/octet-stream',
      'Content-Length': entry.size,
      'Cache-Control': 'no-cache',
      'X-Content-Type-Options': 'nosniff'
    });
    if (req.method === 'HEAD') res.end();
    else createReadStream(file).pipe(res);
  } catch {
    res.writeHead(404).end('Arquivo não encontrado');
  }
}).listen(port, '127.0.0.1', () => process.stdout.write(`GymZ em http://127.0.0.1:${port}/gymz/\n`));
