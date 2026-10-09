import { chooseDeskValue } from './helpers/DeskSelect';
import { reactive } from 'vue';
import { mount, type VueWrapper } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ProductEditor from '@/presentation/workspace/ProductEditor.vue';
import { emptyCopy, productCopy, productMediaForGroup, type Product } from '@/domain/workspace/Commerce';

vi.mock('@/application/shared/UseUiLocale', () => ({ useUiLocale: () => ({ t: (source:string) => source }) }));
let wrapper:VueWrapper;
afterEach(()=>wrapper?.unmount());
function fixture():Product {return {revision:1,sourceRevision:null,document:{id:'vest',garmentType:'vest',variantCode:'v01',status:'draft',sortOrder:0,sizes:['M'],fabricCodes:['silk'],decorationCodes:[],tags:[],locales:{uz:{...emptyCopy(),name:'Nimcha'},ru:emptyCopy(),en:{...emptyCopy(),name:'Vest'}},pricing:{currency:'UZS',status:'confirmed',tiers:[{minimumQuantity:1,maximumQuantity:9,unitPrice:350000},{minimumQuantity:10,maximumQuantity:null,unitPrice:300000}],wording:null},media:[{id:'front',role:'cutout',fileName:'front.png',contentType:'image/png',byteLength:1,sha256:'a'.repeat(64),width:10,height:10,hasPreview:false,alt:null,sourcePath:null},{id:'model',role:'model',fileName:'model.png',contentType:'image/png',byteLength:1,sha256:'b'.repeat(64),width:10,height:10,hasPreview:false,alt:null,sourcePath:null}],issues:[],notes:null,stock:null,madeToOrder:false}};}
function open(){const product=reactive(fixture());wrapper=mount(ProductEditor,{props:{product,canApprove:true,imageUrl:sha=>`/images/${sha}`,'onUpdate:product':(value:Product)=>Object.assign(product,value)}});return product;}
function label(text:string){return wrapper.findAll('label').find(item=>item.text().startsWith(text))!;}
function button(text:string){return wrapper.findAll('button').find(item=>item.text()===text)!;}

describe('canonical product editing',()=>{
 it('emits an edited draft without mutating the input product',async()=>{
  const product=fixture();wrapper=mount(ProductEditor,{props:{product,canApprove:true,imageUrl:sha=>sha}});
  await label('Product name').get('input').setValue('New vest');
  expect(product.document.locales.en.name).toBe('Vest');
  const updated=wrapper.emitted('update:product')?.at(-1)?.[0] as Product;
  expect(updated.document.locales.en.name).toBe('New vest');
  expect(updated).not.toBe(product);
 });
 it('adds French wording without changing Uzbek copy or approving it',async()=>{
  const product=open();await chooseDeskValue(wrapper, 'Product content language', 'fr');
  expect(wrapper.text()).toContain('This translation is empty');expect(product.translations).toBeUndefined();
  await label('Product name').get('input').setValue('Gilet');await label('Material / fabric name').get('input').setValue('Soie');
  expect(productCopy(product,'fr')).toMatchObject({name:'Gilet',fabric:'Soie',status:'draft'});
  expect(product.document.locales.uz.name).toBe('Nimcha');
 });
 it('uses one canonical price across content language changes',async()=>{
  const product=open();await label('1–9 pieces · UZS per piece').get('input').setValue('400000');
  await chooseDeskValue(wrapper, 'Product content language', 'fr');
  expect(product.document.pricing?.tiers[0]?.unitPrice).toBe(400000);expect(product.document.pricing?.status).toBe('draft');
  expect((label('1–9 pieces · UZS per piece').get('input').element as HTMLInputElement).value).toBe('400000');
 });
 it('confirms each language independently at the top and resets only edited wording',async()=>{
  const product=open();
  const approval=()=>wrapper.findAll('label').find(item=>item.text().includes('Approve this language for publication'))!;
  await approval().get('input').setValue(true);expect(productCopy(product,'en').status).toBe('approved');
  await chooseDeskValue(wrapper, 'Product content language', 'uz');
  await approval().get('input').setValue(true);expect(productCopy(product,'uz').status).toBe('approved');
  await label('Material / fabric name').get('input').setValue('Atlas');
  expect(productCopy(product,'uz').status).toBe('draft');expect(productCopy(product,'en').status).toBe('approved');
  const elements=wrapper.findAll('input').map(item=>item.element);expect(elements.indexOf(approval().get('input').element)).toBeLessThan(elements.indexOf(label('Product name').get('input').element));
 });
 it('builds named media selections and removes detached references',async()=>{
  const product=open();await button('Add image group').trigger('click');
  expect(product.mediaGroups?.[0]?.category).toBe('model');
  const choice=wrapper.findAll('article input[type="checkbox"]').find(input=>(input.element as HTMLInputElement).value==='model')!;
  await choice.setValue(true);expect(productMediaForGroup(product,'model').map(image=>image.id)).toEqual(['model']);
  const modelFigure=wrapper.findAll('figure').find(figure=>figure.find('img').attributes('src')?.includes('b'.repeat(64)))!;
  await modelFigure.findAll('button').find(item => item.text() === 'Detach')!.trigger('click');expect(product.mediaGroups?.[0]?.mediaIds).toEqual([]);
 });
 it('keeps legacy role selections and never includes private references',()=>{
  const product=fixture();product.document.media.push({...product.document.media[0]!,id:'private',role:'reference'});
  expect(productMediaForGroup(product,'model').map(image=>image.id)).toEqual(['model']);
  expect(productMediaForGroup(product,'product').map(image=>image.id)).toEqual(['front']);
  expect(productMediaForGroup(product,'all')).toHaveLength(2);
 });
});
