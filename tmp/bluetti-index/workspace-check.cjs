const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = 'Scanner.Web/wwwroot/';
const js = ['workspace','account-dashboard','breezary-inbound','carrier-receipts','carrier-receiving','receiving-sync','tester-pallets','tencent-docs'];
for (const name of js) new vm.Script(fs.readFileSync(root+name+'.js','utf8'),{filename:name});
for (const name of ['documents','wpf-updates','login','register']) {
  const html = fs.readFileSync(root+name+'.html','utf8');
  for (const [,src] of html.matchAll(/<script>([\s\S]*?)<\/script>/g)) new vm.Script(src,{filename:name});
}
const source = fs.readFileSync(root+'workspace.js','utf8');
async function run(hour, signedIn) {
  const greeting = {hidden: true};
  const login = {};
  vm.runInNewContext(source,{
    document: {querySelector: ()=>null, getElementById: id => id==='home-login' ? login : greeting},
    sessionStorage: {removeItem() {}},
    location: {pathname:'/'},
    Date: class {getHours(){return hour;}},
    URLSearchParams,
    fetch: async()=>({ok:signedIn,status:signedIn?200:401,json:async()=>({displayName:'测试员',domain:{domainName:'仓储域'}})})
  });
  await new Promise(setImmediate);
  return [greeting,login];
}
(async()=>{
  for(const [h, phrase] of [[1,'夜间好'],[8,'早上好'],[15,'下午好'],[21,'晚上好']]) {
    const [g,l]=await run(h,true);
    assert.equal(g.textContent,'测试员（仓储域），'+phrase);
    assert.equal(g.hidden,false);
    assert.equal(l.href,'/account.html');
  }
  const [g]=await run(8,false);
  assert.equal(g.hidden,true);
  console.log('Browser scripts and all four homepage greeting variants verified.');
})().catch(e=>{console.error(e);process.exitCode=1;});

