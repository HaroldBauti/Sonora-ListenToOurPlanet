// Convertidor local de videos → hojas de cuadros WebP para SONORA (solo escucha en 127.0.0.1).
// Uso: node server.js <carpeta de salida> <carpeta de videos 1> [<carpeta 2> ...]
const http = require("http"), fs = require("fs"), path = require("path");
const [out, ...dirs] = process.argv.slice(2);
fs.mkdirSync(out, { recursive: true });
const videos = {};
for (const d of dirs) for (const f of fs.readdirSync(d)) if (f.endsWith(".mp4")) videos[f] = path.join(d, f);

http.createServer((req, res) => {
  const u = new URL(req.url, "http://x");
  if (req.method === "GET" && u.pathname === "/") {
    res.writeHead(200, { "content-type": "text/html; charset=utf-8" });
    return fs.createReadStream(path.join(__dirname, "tool.html")).pipe(res);
  }
  if (req.method === "GET" && u.pathname === "/list") {
    res.writeHead(200, { "content-type": "application/json" });
    return res.end(JSON.stringify(Object.keys(videos)));
  }
  if (req.method === "GET" && u.pathname.startsWith("/v/")) {
    const f = videos[decodeURIComponent(u.pathname.slice(3))];
    if (!f) { res.writeHead(404); return res.end(); }
    const size = fs.statSync(f).size, range = req.headers.range;
    if (range) {
      const [a, b] = range.replace("bytes=", "").split("-");
      const start = +a, end = b ? +b : size - 1;
      res.writeHead(206, { "content-type": "video/mp4", "content-range": `bytes ${start}-${end}/${size}`, "accept-ranges": "bytes", "content-length": end - start + 1 });
      return fs.createReadStream(f, { start, end }).pipe(res);
    }
    res.writeHead(200, { "content-type": "video/mp4", "content-length": size, "accept-ranges": "bytes" });
    return fs.createReadStream(f).pipe(res);
  }
  if (req.method === "POST" && u.pathname === "/save") {
    const name = path.basename(u.searchParams.get("name") || "");
    if (!/^[\w.-]+\.(webp|json)$/.test(name)) { res.writeHead(400); return res.end(); }
    const chunks = [];
    req.on("data", c => chunks.push(c));
    req.on("end", () => { fs.writeFileSync(path.join(out, name), Buffer.concat(chunks)); res.writeHead(200); res.end("ok"); });
    return;
  }
  res.writeHead(404); res.end();
}).listen(5390, "127.0.0.1", () => console.log("convertidor en http://127.0.0.1:5390 ·", Object.keys(videos).length, "videos"));
