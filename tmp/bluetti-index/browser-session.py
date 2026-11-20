from pathlib import Path
import re

root=Path("Scanner.Web/wwwroot")
names=["account-dashboard.js","breezary-inbound.js","carrier-receipts.js","carrier-receiving.js",
       "receiving-sync.js","tester-pallets.js","tencent-docs.js","documents.html","wpf-updates.html",
       "login.html","register.html"]
for name in names:
    path=root/name
    s=path.read_bytes().decode("utf-8")
    s=re.sub(r", token = sessionStorage\.getItem\('accountToken'\) \|\| ''", "", s)
    s=re.sub(r"(?m)^ +const token = sessionStorage\.getItem\('accountToken'\) \|\| '';\r?\n", "", s)
    s=s.replace("const token=sessionStorage.getItem('accountToken')||'',", "const ")
    s=re.sub(r"(?m)^ +if \(!token\) \{[^\r\n]*\}\r?\n", "", s)
    s=s.replace("if(!token)location.replace('/login.html');\n","").replace("if(!token)location.replace('/login.html');\r\n","")
    s=s.replace("if(!token){location.replace('/login.html');return}","")
    s=s.replace("headers: { Authorization: 'Bearer ' + token }, ", "").replace(", headers: { Authorization: 'Bearer ' + token }","")
    s=s.replace("Authorization: 'Bearer ' + token, ", "").replace("Authorization: 'Bearer ' + token,\r\n        ", "").replace("Authorization: 'Bearer ' + token,\n        ", "")
    s=s.replace("headers:{Authorization:\x60Bearer \${token}\x60},","")
    s=s.replace("request.setRequestHeader('Authorization',\x60Bearer \${token}\x60);","")
    s=s.replace("sessionStorage.setItem('accountToken',result.token);","")
    s=s.replace("sessionStorage.removeItem('accountToken'); ","").replace("sessionStorage.removeItem('accountToken');","")
    if name=="login.html":
        s=s.replace("/api/account/login","/api/account/web-login")
        s=s.replace("<h1>用户登录</h1>","<h1>用户登录</h1><p>登录后，最近一次使用起 7 天内可继续访问。</p>")
    if name=="account-dashboard.js":
        s=re.sub(r"(?m)^ +\$\('(sidebar-name|sidebar-role|avatar)'\)\.textContent.*\r?\n","",s)
        s=s.replace("  $('logout').addEventListener('click', logout);\n","").replace("  $('logout').addEventListener('click', logout);\r\n","")
    path.write_bytes(s.encode("utf-8"))
for name in ["account.html","breezary-inbound.html","carrier-receipts.html","carrier-receiving.html","receiving-sync.html","tester-pallets.html","tencent-docs.html"]:
    path=root/name
    s=path.read_bytes().decode("utf-8")
    if name=="account.html":
        s=re.sub(r'<aside class="sidebar">.*?</aside>', '<aside class="workspace-menu" aria-label="侧边栏"></aside>',s,flags=re.S)
        s=s.replace('<body>','<body class="workspace">').replace('<div class="dashboard">','<div class="workspace-layout">')
        s=s.replace('<main id="overview">','<main id="overview" class="workspace-content">')
        s=s.replace('<link rel="stylesheet" href="/account-dashboard.css">','<link rel="stylesheet" href="/account-dashboard.css"><link rel="stylesheet" href="/workspace.css?v=3"><script src="/workspace.js?v=3" defer></script>')
        s=s.replace('会话到期后需重新登录','每次使用后续期 7 天')
    s=re.sub(r'(/(?:account-dashboard|breezary-inbound|carrier-receipts|carrier-receiving|receiving-sync|tester-pallets|tencent-docs)\.js)(?:\?v=[^"]+)?',r'\1?v=3',s)
    path.write_bytes(s.encode("utf-8"))

