"""Local Unity WebGL server: python Tools/NavalCombat/serve_webgl.py --port 8001."""

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class WebGLHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        for suffix in (".br", ".gz"):
            if path.endswith(suffix):
                path = path[:-len(suffix)]
                break
        if path.endswith(".wasm"):
            return "application/wasm"
        if path.endswith(".js"):
            return "application/javascript"
        if path.endswith(".data"):
            return "application/octet-stream"
        return super().guess_type(path)

    def end_headers(self):
        path = Path(self.translate_path(self.path))
        if path.is_file():
            encoding = {".br": "br", ".gz": "gzip"}.get(path.suffix)
            if encoding:
                self.send_header("Content-Encoding", encoding)
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bind", default="127.0.0.1",
                        help="Listen address; use 0.0.0.0 for LAN testing")
    parser.add_argument("--port", type=int, default=8001)
    parser.add_argument("--directory", type=Path,
                        default=Path(__file__).resolve().parents[2] / "Builds/NavalCombatWebGL")
    args = parser.parse_args()
    directory = args.directory.resolve()
    if not (directory / "index.html").is_file():
        parser.error(f"No index.html in {directory}")
    handler = partial(WebGLHandler, directory=str(directory))
    with ThreadingHTTPServer((args.bind, args.port), handler) as server:
        print(f"Serving {directory}\nListening on {args.bind}:{args.port} (Ctrl+C to stop)", flush=True)
        if args.bind == "0.0.0.0":
            print(f"On another device, open http://<this PC's LAN IP>:{args.port}/", flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
