
import urllib.request, urllib.error, json, base64
img='/Users/nathannoom/Developer/Games/The Sims/simitone-fork/tools/iff-dump/uigr-png/0011_Bus_loadscreen_800x600.png'
b64=base64.b64encode(open(img,'rb').read()).decode()
payload={
  "model":"qwen3.5:9b",
  "messages":[
    {"role":"user","content":"Describe this game loading screen. Pixelated/low-res? Colors, any text, any vehicle. One short paragraph.",
     "images":[b64]}
  ],
  "stream":False
}
req=urllib.request.Request('http://127.0.0.1:11436/api/chat', data=json.dumps(payload).encode(), headers={'Content-Type':'application/json'})
try:
    with urllib.request.urlopen(req, timeout=200) as r:
        j=json.load(r)
    print(j['message']['content'])
except urllib.error.HTTPError as e:
    print('HTTP', e.code)
    print(e.read().decode('utf-8','replace')[:1500])
