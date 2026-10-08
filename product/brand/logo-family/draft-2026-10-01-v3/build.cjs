
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=__dirname;
fs.mkdirSync(path.join(root,'assets'),{recursive:true});
const source=fs.readFileSync(path.join(root,'source','approved-master.png'));
const sourceHash=crypto.createHash('sha256').update(source).digest('hex');
if(sourceHash!=='32f2efeb381b92fdbf228c6f551909c8d1f78bc1212f1c6b79778cbbf8bcd33e')throw new Error('Approved original source changed');
const uri='data:image/png;base64,'+source.toString('base64');
const needleClip='M656 664 682 579 696 537 781 362 798 331 829 328 837 354 817 385 700 575 714 578 704 590 696 600 684 620 673 640 667 650 659 660Z';
const ovalClip='M295 340H605V505L550 570V640H295Z';
const eyeClip='M795 353H817V370L797 407H776V390Z';
const ink='#20211E',white='#FFFFFF',ivory='#F7F3E9';
const filters='<filter id="ink-extraction" color-interpolation-filters="sRGB" x="0" y="0" width="100%" height="100%"><feColorMatrix type="matrix" values="0 0 0 0 1 0 0 0 0 1 0 0 0 0 1 -.2126 -.7152 -.0722 0 1"/><feComponentTransfer><feFuncA type="table" tableValues="0 0 0.08 0.28 0.48 0.68 0.88 1 1 1 1"/></feComponentTransfer></filter>';
const eyeFilter='<filter id="eye-extraction" color-interpolation-filters="sRGB" x="0" y="0" width="100%" height="100%"><feColorMatrix type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 .2126 .7152 .0722 0 0"/><feComponentTransfer><feFuncA type="table" tableValues="0 0 0 0.12 0.32 0.52 0.72 0.92 1 1 1"/></feComponentTransfer></filter>';
const raster='<image id="original-pixels" href="'+uri+'" width="1536" height="1024" filter="url(#ink-extraction)"/>';
function masked(id,body,type='alpha'){
 return '<mask id="'+id+'" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="1536" height="1024" style="mask-type:'+type+'">'+body+'</mask>';
}
const originalUse='<use href="#original-pixels"/>';
const compactUses='<g clip-path="url(#oval-clip)">'+originalUse+'</g><g transform="translate(-278 0)"><g clip-path="url(#needle-clip)">'+originalUse+'</g></g>';
const eyeKnockout='<g transform="translate(-278 0)"><g clip-path="url(#eye-clip)"><image href="'+uri+'" width="1536" height="1024" filter="url(#eye-extraction)"/></g></g>';
const definitions=filters+eyeFilter+raster+'<clipPath id="needle-clip"><path d="'+needleClip+'"/></clipPath><clipPath id="oval-clip"><path d="'+ovalClip+'"/></clipPath><clipPath id="eye-clip"><path d="'+eyeClip+'"/></clipPath>'+masked('whole-master',originalUse)+masked('compact-master',compactUses+eyeKnockout,'luminance')+masked('needle-only','<g clip-path="url(#needle-clip)">'+originalUse+'</g>');
const specs=[
 {name:'ocharo-wordmark-ink',kind:'wordmark',view:[280,315,1010,380],color:ink,w:2020,h:760},
 {name:'ocharo-wordmark-reverse',kind:'wordmark',view:[280,315,1010,380],color:white,w:2020,h:760},
 {name:'ocharo-symbol-ink',kind:'compact',view:[275,315,370,370],color:ink,w:1110,h:1110},
 {name:'ocharo-tile',kind:'compact',view:[275,315,370,370],color:white,bg:ink,w:1110,h:1110},
 {name:'ocharo-micro',kind:'compact',view:[275,315,370,370],color:ink,w:1110,h:1110},
 {name:'ocharo-micro-reverse',kind:'compact',view:[275,315,370,370],color:white,w:1110,h:1110},
 {name:'ocharo-avatar',kind:'compact',view:[275,315,370,370],color:ink,bg:ivory,w:1110,h:1110},
 {name:'ocharo-endorsement',kind:'wordmark',view:[100,315,1190,380],color:ink,w:2380,h:760,endorsement:true}
];
for(const s of specs){
 const mask=s.kind==='compact'?'compact-master':'whole-master';
 const body='<rect width="1536" height="1024" fill="'+s.color+'" mask="url(#'+mask+')"/>';
 const bg=s.bg?'<rect x="'+s.view[0]+'" y="'+s.view[1]+'" width="'+s.view[2]+'" height="'+s.view[3]+'" fill="'+s.bg+'"/>':'';
 const by=s.endorsement?'<text x="145" y="610" font-family="Arial, Helvetica, sans-serif" font-size="65" fill="'+ink+'">by</text>':'';
 const svg='<svg xmlns="http://www.w3.org/2000/svg" width="'+s.w+'" height="'+s.h+'" viewBox="'+s.view.join(' ')+'" role="img" aria-label="'+s.name+'"><metadata>Approved original master SHA256 '+sourceHash+'</metadata><defs>'+definitions+'</defs>'+bg+body+by+'</svg>';
 fs.writeFileSync(path.join(root,'assets',s.name+'.svg'),svg);
}
const needle='<svg xmlns="http://www.w3.org/2000/svg" width="394" height="696" viewBox="645 326 197 348"><title>Needle extracted from original Ocharo master</title><defs>'+definitions+'</defs><rect width="1536" height="1024" fill="'+ink+'" mask="url(#needle-only)"/></svg>';
fs.writeFileSync(path.join(root,'source','original-needle.svg'),needle);
const originalView='<svg xmlns="http://www.w3.org/2000/svg" width="2020" height="760" viewBox="280 315 1010 380"><image href="'+uri+'" width="1536" height="1024"/></svg>';
fs.writeFileSync(path.join(root,'source','original-wordmark-view.svg'),originalView);
fs.writeFileSync(path.join(root,'export-spec.json'),JSON.stringify({sourceHash,needleClip,ovalClip,eyeClip,compactTranslation:[-278,0],needleRedrawn:false,wordmarkLocalTransforms:false,specs},null,2)+'\n');
console.log(JSON.stringify({sourceHash,compositions:specs.length,needleRedrawn:false}));
