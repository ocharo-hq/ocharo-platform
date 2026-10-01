import { renderCatalogueBundle } from '@/integration/workspace/DesignExport';
import { inspectDesign } from '@/integration/workspace/DesignPreflight';
import { layoutDesignText } from '@/integration/workspace/DesignTextLayout';
const groups=[
 {id:'ar',heading:'العربية — حرير بخارى',direction:'rtl',text:'هذا القماش من الحرير الطبيعي. زخارف جميلة وألوان مستوحاة من سمرقند وبخارى. السعر ١٢٥٬٠٠٠ سوم.',fontFamily:'sans'},
 {id:'zh-Hans',heading:'中文 — 乌兹别克丝绸',direction:'ltr',text:'这件服装采用乌兹别克斯坦丝绸制作，图案灵感来自布哈拉和撒马尔罕。柔软的面料适合日常穿着。尺寸：S、M、L。',fontFamily:'sans'},
 {id:'uz',heading:'O‘zbekcha — Русский',direction:'ltr',text:'Buxoro ipak matosi — шёлк Бухары. O‘lchamlar: S, M, L. Qo‘lda ishlangan bezak; naqsh, rang, mato. Размеры и состав ткани. Narx: 125 000 so‘m.',fontFamily:'serif'},
];
const pages=groups.map(group=>({id:group.id,title:group.id,width:150,height:190,background:'#fbf8f1',elements:[
 {id:'header',type:'text',label:'Locale heading',x:12,y:12,width:126,height:25,text:group.heading,fontSize:6,fontFamily:group.fontFamily,direction:group.direction,color:'#272822'},
 {id:'body',type:'text',label:'Paragraph',x:12,y:50,width:126,height:58,text:group.text,fontSize:4.3,lineHeight:1.5,fontFamily:group.fontFamily,direction:group.direction,color:'#272822'},
 {id:'narrow',type:'text',label:'Narrow wrapping',x:12,y:115,width:70,height:62,text:group.text,fontSize:3.5,lineHeight:1.4,fontFamily:group.fontFamily,direction:group.direction,color:'#272822'},
]}));
(window as any).runTypography=async()=>{
 const snapshot={schemaVersion:1,kind:'catalogue',locale:'en',templateId:'typography-smoke',pages} as any;
 const report=await inspectDesign(snapshot);
 const context=document.createElement('canvas').getContext('2d')!;
 const layouts=pages.map(page=>({locale:page.id,body:layoutDesignText(context,page.elements[1] as any,240/25.4),narrow:layoutDesignText(context,page.elements[2] as any,240/25.4)}));
 const result=await renderCatalogueBundle(snapshot,'Multilingual catalogue smoke',()=>{});
 // FileReader avoids argument limits on high-resolution PNGs and PDFs.
 const encode=(blob:Blob)=>new Promise<string>((resolve,reject)=>{const r=new FileReader();r.onload=()=>resolve(String(r.result).split(',')[1]!);r.onerror=reject;r.readAsDataURL(blob)});
 return{report,layouts,pdf:await encode(result.pdf),pages:await Promise.all(result.pages.map(async(page)=>({id:page.id,png:await encode(page.png)})))};
};
