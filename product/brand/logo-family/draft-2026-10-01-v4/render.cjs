const fs=require('node:fs'), path=require('node:path');
const { chromium }=require(process.env.OCHARO_PLAYWRIGHT_MODULE || 'playwright');
const root=__dirname;
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({deviceScaleFactor:1});
 const {specs}=JSON.parse(fs.readFileSync(path.join(root,'export-spec.json'),'utf8'));
 for(const s of specs){
  await page.setViewportSize({width:s.w,height:s.h});
  await page.goto('file://'+path.join(root,'assets',s.name+'.svg'));
  await page.screenshot({path:path.join(root,'assets',s.name+'.png'),omitBackground:true});
 }
 await page.goto('about:blank');
 for(const size of [16,24,32]) for(const suffix of ['', '-reverse']){
  await page.setViewportSize({width:size,height:size});
  const svg=fs.readFileSync(path.join(root,'assets','ocharo-micro'+suffix+'.svg'),'utf8');
  await page.setContent('<style>html,body{margin:0;width:100%;height:100%;background:transparent}svg{display:block;width:100%;height:100%}</style>'+svg);
  await page.screenshot({path:path.join(root,'assets','ocharo-favicon-'+size+(suffix||'-ink')+'.png'),omitBackground:true});
 }
 await page.setViewportSize({width:1440,height:1100});
 await page.goto('file://'+path.join(root,'index.html'));
 await page.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));
 await page.screenshot({path:path.join(root,'preview.png'),fullPage:true});
 const desktop=await page.evaluate(()=>({allGalleryImagesLoaded:[...document.images].every(i=>i.complete&&i.naturalWidth>0),desktopFits:document.documentElement.scrollWidth<=innerWidth}));
 await page.locator('section[aria-labelledby="compact-title"]').screenshot({path:path.join(root,'preview-icons.png')});
 await page.setViewportSize({width:390,height:844});
 await page.screenshot({path:path.join(root,'preview-mobile.png'),fullPage:true});
 const mobile=await page.evaluate(()=>({mobileFits:document.documentElement.scrollWidth<=innerWidth}));
 fs.writeFileSync(path.join(root,'gallery-verification.json'),JSON.stringify({...desktop,...mobile},null,2)+'\n');
 console.log(JSON.stringify({...desktop,...mobile}));
 await browser.close();
})();
