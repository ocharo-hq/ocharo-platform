
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const root=__dirname;
const out=path.join(root,'assets');
fs.mkdirSync(out,{recursive:true});
fs.mkdirSync(path.join(root,'source'),{recursive:true});
for(const name of ['oval','lettering']) {
 if(!fs.existsSync(path.join(root,'source',name+'.png'))) throw new Error('Missing source/'+name+'.png');
}
const data=name=>'data:image/png;base64,'+fs.readFileSync(path.join(root,'source',name+'.png')).toString('base64');
const needle={
 id:'ocharo-thin-needle-v1',
 outer:'M 0 1000 C -1.8 880 -5 570 -7 340 C -8.5 250 -25 143 -26 69 C -26 29 -17 0 0 0 C 17 0 26 29 26 69 C 25 143 8.5 250 7 340 C 5 570 1.8 880 0 1000 Z',
 eye:'M 0 28 C -8 28 -12 47 -12 71 C -12 96 -7 132 0 149 C 7 132 12 96 12 71 C 12 47 8 28 0 28 Z'
};
needle.d=needle.outer+' '+needle.eye;
needle.sha256=crypto.createHash('sha256').update(needle.d).digest('hex');
fs.writeFileSync(path.join(root,'source','needle.json'),JSON.stringify(needle,null,2)+'\n');
const ink='#20211E', white='#FFFFFF', ivory='#F7F3E9';
const needleDef='<path id="shared-needle" fill-rule="evenodd" d="'+needle.d+'"/>';
function mask(name,w,h){return '<mask id="body" maskUnits="userSpaceOnUse" x="0" y="0" width="'+w+'" height="'+h+'" style="mask-type:alpha"><image href="'+data(name)+'" width="'+w+'" height="'+h+'"/></mask>';}
const compactTransform='translate(1025 171) rotate(42) scale(1.24)';
const wordmarkTransform='translate(1160 84) rotate(27) scale(0.68)';
function compact(color){return '<g fill="'+color+'"><rect width="1254" height="1254" mask="url(#body)"/><use href="#shared-needle" transform="'+compactTransform+'"/></g>';}
function wordmark(color){return '<g fill="'+color+'"><rect width="2172" height="724" mask="url(#body)"/><use href="#shared-needle" transform="'+wordmarkTransform+'"/></g>';}
const specs=[
 {name:'ocharo-wordmark-ink',kind:'wordmark',color:ink,w:2172,h:724},
 {name:'ocharo-wordmark-reverse',kind:'wordmark',color:white,w:2172,h:724},
 {name:'ocharo-symbol-ink',kind:'compact',color:ink,w:1254,h:1254},
 {name:'ocharo-tile',kind:'compact',color:white,bg:ink,w:1254,h:1254},
 {name:'ocharo-micro',kind:'compact',color:ink,w:1254,h:1254},
 {name:'ocharo-micro-reverse',kind:'compact',color:white,w:1254,h:1254},
 {name:'ocharo-avatar',kind:'compact',color:ink,bg:ivory,w:1254,h:1254},
 {name:'ocharo-endorsement',kind:'endorsement',color:ink,w:2472,h:724}
];
for(const s of specs){
 const body=s.kind==='compact'?compact(s.color):s.kind==='wordmark'?wordmark(s.color):
  '<text x="70" y="600" font-family="Arial, Helvetica, sans-serif" font-size="142" fill="'+s.color+'">by</text><g transform="translate(300 0)">'+wordmark(s.color)+'</g>';
 const defs=needleDef+mask(s.kind==='compact'?'oval':'lettering',s.kind==='compact'?1254:2172,s.kind==='compact'?1254:724);
 const svg='<svg xmlns="http://www.w3.org/2000/svg" width="'+s.w+'" height="'+s.h+'" viewBox="0 0 '+s.w+' '+s.h+'" role="img" aria-label="'+s.name+'"><metadata>Shared needle '+needle.id+' SHA256 '+needle.sha256+'</metadata><defs>'+defs+'</defs>'+(s.bg?'<rect width="100%" height="100%" fill="'+s.bg+'"/>':'')+body+'</svg>';
 fs.writeFileSync(path.join(out,s.name+'.svg'),svg);
}
const standalone='<svg xmlns="http://www.w3.org/2000/svg" width="260" height="1100" viewBox="-130 -50 260 1100"><title>Ocharo shared thin needle</title><defs>'+needleDef+'</defs><use href="#shared-needle" fill="'+ink+'"/></svg>';
fs.writeFileSync(path.join(root,'source','needle.svg'),standalone);
fs.writeFileSync(path.join(root,'export-spec.json'),JSON.stringify({needle,compactTransform,wordmarkTransform,specs},null,2)+'\n');
console.log(JSON.stringify({generated:specs.length,needleHash:needle.sha256}));
