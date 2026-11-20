const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
const file=fs.readFileSync('output/pdf/BLUETTI 配件文字索引.html','utf8');
const data=file.match(/id="dataset">(.+)<\/script>/)[1];
const script=file.match(/<script>\s*([\s\S]+)<\/script>/)[1];
class Element {
 constructor(t=''){this.textContent=t;this.value='';this.children=[];this.listeners={};}
 add(x){this.children.push(x)}
 append(...xs){this.children.push(...xs)}
 replaceChildren(...xs){this.children=xs}
 addEventListener(n,f){this.listeners[n]=f}
 focus(){}
}
const e=Object.fromEntries(['dataset','q','model','category','clear','rows','pages','count','results','empty'].map(id=>[id,new Element()]));
e.dataset.textContent=data;
vm.runInNewContext(script,{document:{getElementById:i=>e[i],querySelector:()=>new Element(),createElement:()=>new Element()},Option:Element});
assert.equal(e.rows.children.length,1061);assert.equal(e.pages.children.length,161);
e.q.value='AC180 solar charging cable';e.model.value='AC180';e.category.value='标准随箱';e.q.listeners.input();
assert.equal(e.rows.children.length,1);
assert.equal(e.rows.children[0].children[2].textContent,'1');
assert.match(e.rows.children[0].children[4].children[0].href,/#page=1$/);
e.q.value='no matches expected';e.q.listeners.input();assert.equal(e.empty.hidden,false);
e.clear.listeners.click();assert.equal(e.rows.children.length,1061);
const csv=fs.readFileSync('output/pdf/BLUETTI 配件文字索引.csv','utf8');
assert.equal(csv.trimEnd().split(/\r?\n/).length,1062);
console.log('Verified offline search, combined filters, reset, PDF page links, and CSV row count');

