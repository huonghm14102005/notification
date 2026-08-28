import hashlib
import hmac
import json
from urllib.parse import parse_qs, urlparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

state = {"secret": "", "events": []}

class Handler(BaseHTTPRequestHandler):
    def send_json(self, status, value):
        body = json.dumps(value).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == "/health": return self.send_json(200, {"ok": True})
        parsed = urlparse(self.path)
        if parsed.path == "/events":
            notification_id = parse_qs(parsed.query).get("notificationId", [None])[0]
            if notification_id:
                event = next((x for x in state["events"] if x["payload"].get("notificationId") == notification_id), None)
                return self.send_json(200 if event else 404, event or {})
            return self.send_json(200, state["events"])
        self.send_json(404, {})

    def do_HEAD(self):
        self.send_response(200 if self.path == "/health" else 404)
        self.end_headers()

    def do_POST(self):
        raw = self.rfile.read(int(self.headers.get("Content-Length", "0")))
        if self.path == "/configure":
            state["secret"] = json.loads(raw)["secret"]
            state["events"] = []
            return self.send_json(204, {})
        if self.path != "/callback": return self.send_json(404, {})
        timestamp = self.headers.get("X-NTS-Timestamp", "")
        expected = "v1=" + hmac.new(state["secret"].encode(), timestamp.encode() + b"." + raw, hashlib.sha256).hexdigest()
        valid = hmac.compare_digest(expected, self.headers.get("X-NTS-Signature", ""))
        payload = json.loads(raw)
        state["events"].append({"signatureValid": valid, "headerEventId": self.headers.get("X-NTS-Event-Id"), "payload": payload})
        self.send_json(204 if valid else 401, {})

    def log_message(self, *_): pass

ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
