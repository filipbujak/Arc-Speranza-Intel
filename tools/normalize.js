// Normalizuje ścieżki SVG do jawnej postaci: każda komenda z literą, liczby rozdzielone spacjami, flagi łuków osobno.
const AR={m:2,l:2,h:1,v:1,c:6,s:4,q:4,t:2,a:7,z:0};
function normalize(d){
  let i=0; const out=[]; let cmd=null;
  const skip=()=>{while(i<d.length&&/[\s,]/.test(d[i]))i++;};
  const num=()=>{skip(); const m=/^[+-]?(\d*\.\d+|\d+\.?\d*)([eE][+-]?\d+)?/.exec(d.slice(i)); if(!m) throw new Error('num@'+i+' '+d.slice(i,i+20)); i+=m[0].length; return parseFloat(m[0]);};
  const flag=()=>{skip(); const c=d[i]; if(c!=='0'&&c!=='1') throw new Error('flag@'+i); i++; return +c;};
  while(true){ skip(); if(i>=d.length)break;
    if(/[a-zA-Z]/.test(d[i])){cmd=d[i++];} else if(!cmd) throw new Error('no cmd');
    const lc=cmd.toLowerCase(); const n=AR[lc]; if(n===undefined) throw new Error('bad cmd '+cmd);
    if(n===0){out.push(cmd);continue;}
    const ps=[]; for(let k=0;k<n;k++){ ps.push(lc==='a'&&(k===3||k===4)?flag():num()); }
    out.push(cmd+' '+ps.map(x=>+x.toFixed(4)).join(' '));
    if(lc==='m') cmd = cmd==='m'?'l':'L';
  }
  return out.join(' ');
}
module.exports=normalize;
if(require.main===module){
 const j=require('./icons.json'); let n=0;
 for(const g of ['markers','ui']) for(const k in j[g]){ j[g][k]=normalize(j[g][k]); n++; }
 require('fs').writeFileSync('icons.json',JSON.stringify(j)); console.log('normalized',n);
 console.log(j.ui.map.slice(0,120));
}
