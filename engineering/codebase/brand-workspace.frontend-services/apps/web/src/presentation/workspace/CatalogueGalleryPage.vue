<script setup lang="ts">
import { computed, onMounted, onScopeDispose, ref, watch } from 'vue';
import type { AccessControls } from '@/application/workspace/UseAccess';
import { failureMessage } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { requestConfirmation, notifyError } from '@/application/shared/UseFeedback';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import {Modal,ModalContent,ModalTitle} from '@wow-two-beta/ui-vue/presentation/overlays';
import ContentSkeleton from '@/presentation/shared/ContentSkeleton.vue';
import { catalogueLocales, type CatalogueLocale } from '@/domain/workspace/Commerce';
import { catalogueMetadata, catalogueSlug, createCatalogue, duplicateCatalogue } from '@/domain/workspace/CatalogueDocuments';
import { cloneSnapshot, validCatalogueSlug, type CatalogueMetadata, type DesignDocument, type DesignSummary } from '@/domain/workspace/EditorModels';

defineOptions({name:'CatalogueGalleryPage'});
const props=defineProps<{access:AccessControls;workspace:WorkspaceControls}>();
const emit=defineEmits<{open:[id:string,mode:'web'|'pdf']}>();
const {t,uiLocale}=useUiLocale(),controller=new AbortController();
const documents=ref<DesignSummary[]>([]),loading=ref(true),busy=ref(false),query=ref(''),loadError=ref('');
const selected=ref<DesignDocument|null>(null),name=ref(''),locale=ref<CatalogueLocale>('uz'),brandId=ref('');
const metadata=ref<CatalogueMetadata>({slug:'',description:'',isActive:false,outputs:{web:true,pdf:true},web:{theme:'editorial',pages:[]}});
const dialogOpen=ref(false),baseline=ref(''),slugError=ref('');
const serialize=()=>JSON.stringify({name:name.value,locale:locale.value,brandId:brandId.value,metadata:metadata.value});
const dirty=computed(()=>dialogOpen.value&&serialize()!==baseline.value);
watch(dirty,value=>props.access.setDirty(value),{flush:'sync'});
async function close(){if(busy.value)return;if(dirty.value&&!await requestConfirmation(t('Discard unsaved catalogue settings?'),{confirmLabel:t('Discard changes')}))return;dialogOpen.value=false;}

const visible=computed(()=>documents.value.filter(item=>!item.archived&&`${item.title} ${item.catalogue?.slug??''}`.toLowerCase().includes(query.value.toLowerCase())));
const org=()=>props.workspace.organization.value!.id;
const locked=computed(()=>busy.value||props.access.busy.value||!props.workspace.canEdit.value);
onMounted(load);onScopeDispose(()=>{controller.abort();props.access.setDirty(false);});
async function load(){loading.value=true;loadError.value='';try{const result=await props.access.api.getDesigns(org(),'catalogue',controller.signal);if(controller.signal.aborted)return;if(result.ok)documents.value=result.value;else loadError.value=failureMessage(result.failure);}catch{if(!controller.signal.aborted)loadError.value=t('Unable to load catalogues.');}finally{loading.value=false;if(loadError.value)notifyError(loadError.value);}}
function formats(item:DesignSummary){return item.catalogue?.outputs??{web:false,pdf:true};}
async function get(item:DesignSummary){const result=await props.access.api.getDesign(org(),item.id,controller.signal);if(!result.ok){notifyError(failureMessage(result.failure));return null;}return result.value;}
async function edit(item?:DesignSummary){
 if(locked.value)return;busy.value=true;
 try{const document=item?await get(item):null;if(item&&!document)return;selected.value=document;
  name.value=document?.title??'';locale.value=document?.snapshot.locale??'uz';brandId.value=document?.snapshot.catalogueSettings?.brandId??props.workspace.brands.value?.[0]?.id??'';
  metadata.value=document?catalogueMetadata(document.snapshot):{slug:'',description:'',isActive:false,outputs:{web:true,pdf:true},web:{theme:'editorial',pages:[{id:crypto.randomUUID(),title:t('Collection'),layout:'grid',isActive:true,productIds:[]}]}};
  if(document&&!document.snapshot.catalogue)metadata.value.slug=`catalogue-${document.id.slice(0,8)}`;
  slugError.value='';baseline.value=serialize();dialogOpen.value=true;
 }finally{busy.value=false;}
}
function nameChanged(){if(!selected.value)metadata.value.slug=catalogueSlug(name.value);}
async function save(){
 if(locked.value)return;
 slugError.value=validCatalogueSlug(metadata.value.slug)?'':t('Use 1–100 lowercase letters or numbers, separated by single hyphens. This slug must not be a reserved site path.');if(slugError.value)return;
 if(!name.value.trim()||!brandId.value||(!metadata.value.outputs.web&&!metadata.value.outputs.pdf)){notifyError(t('Choose a name, brand and at least one output.'));return;}
 if(metadata.value.outputs.web&&!metadata.value.web.pages.length)metadata.value.web.pages=[{id:crypto.randomUUID(),title:name.value,layout:'grid',isActive:true,productIds:[]}];
 const snapshot=selected.value?cloneSnapshot(selected.value.snapshot):createCatalogue(name.value,locale.value,metadata.value,brandId.value);
 snapshot.catalogue=JSON.parse(JSON.stringify(metadata.value));
 // Content language changes belong in the designers, which resolve translated bindings together.
 if(!selected.value)snapshot.locale=locale.value;
 if(metadata.value.outputs.pdf&&!snapshot.pages.length)snapshot.pages=createCatalogue(name.value,snapshot.locale,metadata.value,brandId.value).pages;
 snapshot.catalogueSettings??={copyByLocale:{},mediaGroup:'all',priceDisplay:{show:true,showCurrency:true,showQuantity:true,showRetail:true,showWholesale:true}};snapshot.catalogueSettings.brandId=brandId.value;
 busy.value=true;try{const result=await props.access.write(signal=>props.access.api.saveDesign(org(),selected.value?.id??null,{kind:'catalogue',title:name.value.trim(),snapshot,expectedRevision:selected.value?.revision??null},signal),controller.signal);
  if(!result.ok){notifyError(failureMessage(result.failure));return;}dialogOpen.value=false;await load();
 }catch(cause){notifyError(cause instanceof Error?cause.message:t('Unable to save this catalogue.'));}finally{busy.value=false;}
}
async function duplicate(item:DesignSummary){if(locked.value)return;busy.value=true;try{const source=await get(item);if(!source)return;const snapshot=duplicateCatalogue(source.snapshot,crypto.randomUUID().slice(0,8));const result=await props.access.write(signal=>props.access.api.saveDesign(org(),null,{kind:'catalogue',title:`${source.title} · ${t('Copy')}`.slice(0,160),snapshot,expectedRevision:null},signal),controller.signal);if(result.ok)await load();else notifyError(failureMessage(result.failure));}finally{busy.value=false;}}
async function activate(item:DesignSummary){if(locked.value)return;busy.value=true;try{const source=await get(item);if(!source)return;const snapshot=cloneSnapshot(source.snapshot);snapshot.catalogue=catalogueMetadata(snapshot);snapshot.catalogue.slug=source.snapshot.catalogue?.slug??`catalogue-${source.id.slice(0,8)}`;snapshot.catalogue.isActive=!snapshot.catalogue.isActive;const result=await props.access.write(signal=>props.access.api.saveDesign(org(),source.id,{kind:'catalogue',title:source.title,snapshot,expectedRevision:source.revision},signal),controller.signal);if(result.ok)await load();else notifyError(failureMessage(result.failure));}finally{busy.value=false;}}
async function remove(item:DesignSummary){if(locked.value||!await requestConfirmation(t('Remove “{name}” from the catalogue gallery? Saved history and published editions are preserved.',{name:item.title}),{confirmLabel:t('Remove catalogue')}))return;busy.value=true;try{const result=await props.access.write(signal=>props.access.api.archiveDesign(org(),item.id,item.revision,signal),controller.signal);if(result.ok)await load();else notifyError(failureMessage(result.failure));}finally{busy.value=false;}}
</script>
<template>
 <section class="catalogue-gallery">
  <header><div><p class="eyebrow">{{t('Your catalogues')}}</p><h1>{{t('A collection, in every format.')}}</h1><p>{{t('Create one catalogue and design its website, PDF, or both.')}}</p></div><button :disabled="locked" @click="edit()">{{t('New catalogue')}}</button></header>
  <label class="search">{{t('Find a catalogue')}}<input v-model="query" type="search" :placeholder="t('Name or slug')"></label>
  <div v-if="loadError" role="status" class="load-error"><p>{{loadError}}</p><button :disabled="loading" @click="load">{{t('Try again')}}</button></div>
  <ContentSkeleton v-if="loading&&!documents.length" :cards="3" :label="t('Loading catalogues')" />
  <div v-else class="catalogue-grid"><article v-for="item in visible" :key="item.id">
   <div class="catalogue-cover"><span>Ocharo</span><strong>{{item.title}}</strong><small>{{item.locale?.toUpperCase()??'PDF'}}</small></div>
   <div class="card-body"><div class="card-heading"><h2>{{item.title}}</h2><span>{{(item.catalogue?.isActive??true)?t('Active'):t('Inactive')}}</span></div><p v-if="item.catalogue?.slug" class="slug">/{{item.catalogue.slug}}</p><p>{{item.catalogue?.description}}</p><small>{{t('Last edited')}} · {{new Date(item.updatedAtUtc).toLocaleString(uiLocale)}}</small>
    <div class="designer-actions"><button v-if="formats(item).web" @click="emit('open',item.id,'web')">{{t('Web designer')}}</button><button v-if="formats(item).pdf" @click="emit('open',item.id,'pdf')">{{t('PDF designer')}}</button></div>
    <div class="card-actions"><button :disabled="locked" @click="edit(item)">{{t('Settings')}}</button><button :disabled="locked" @click="duplicate(item)">{{t('Duplicate')}}</button><button :disabled="locked" @click="activate(item)">{{(item.catalogue?.isActive??true)?t('Deactivate'):t('Activate')}}</button><button :disabled="locked" @click="remove(item)">{{t('Remove')}}</button></div>
   </div>
  </article><p v-if="!visible.length&&!loadError&&!loading">{{query?t('No catalogues match this search.'):t('Create your first catalogue.')}}</p></div>
  <p class="catalogue-note">{{t('Changes affect the working catalogue. Published editions change only after explicit preparation and publication.')}}</p>
  <Modal :open="dialogOpen" :dismiss-on-escape="!busy" :dismiss-on-outside-click="!busy" @update:open="value=>{if(!value)void close();}"><ModalContent v-if="dialogOpen" :is-blurred="true" class="catalogue-settings-modal"><form @submit.prevent="save"><header><ModalTitle>{{selected?t('Catalogue settings'):t('New catalogue')}}</ModalTitle><button type="button" :disabled="busy" @click="close()">{{t('Close')}}</button></header><fieldset :disabled="busy">
   <label>{{t('Name')}}<input v-model="name" maxlength="160" required @input="nameChanged"></label><label>{{t('Slug')}}<input v-model="metadata.slug" maxlength="100" pattern="[a-z0-9]+(-[a-z0-9]+)*" :aria-invalid="!!slugError" :aria-describedby="slugError?'slug-error':undefined" required><span v-if="slugError" id="slug-error" role="alert">{{slugError}}</span></label><label>{{t('Description')}}<textarea v-model="metadata.description" maxlength="2000" rows="3" /></label>
   <label>{{t('Brand')}}<select v-model="brandId" required><option value="">{{t('Choose brand')}}</option><option v-for="brand in workspace.brands.value" :key="brand.id" :value="brand.id">{{brand.name}}</option></select></label>
   <label v-if="!selected">{{t('Catalogue language')}}<select v-model="locale"><option v-for="language in catalogueLocales" :key="language.code" :value="language.code">{{language.name}}</option></select></label>
   <div class="output-options"><label><input v-model="metadata.outputs.web" type="checkbox">{{t('Web catalogue')}}</label><label><input v-model="metadata.outputs.pdf" type="checkbox">{{t('PDF catalogue')}}</label><label><input v-model="metadata.isActive" type="checkbox">{{t('Active')}}</label></div>
  </fieldset><button type="submit" :disabled="busy">{{busy?t('Saving…'):t('Save catalogue')}}</button></form></ModalContent></Modal>
 </section>
</template>
<style scoped>
.catalogue-gallery{display:grid;gap:24px;color:var(--color-foreground)}header,.card-heading,.designer-actions,.card-actions,.output-options{display:flex;gap:12px;align-items:center;justify-content:space-between;flex-wrap:wrap}h1{font:42px/1.1 Georgia,serif;margin:8px 0}h2{font-size:19px;margin:0}p{font-size:14px;line-height:1.6}.search{max-width:420px}.catalogue-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:24px}.catalogue-grid article{border:1px solid var(--color-border,#d9d3c7);border-radius:12px;overflow:hidden;background:var(--color-card,#fffdf8)}.catalogue-cover{min-height:230px;background:var(--color-muted,#e9e4d8);padding:30px;color:var(--color-foreground,#34342c);display:flex;flex-direction:column;justify-content:space-between;gap:24px}.catalogue-cover span{font:24px Georgia,serif}.catalogue-cover strong{font:32px/1.1 Georgia,serif;overflow-wrap:anywhere}.card-body{padding:20px;display:grid;gap:14px}.slug,small,.catalogue-note{font-size:12px;color:var(--color-muted-foreground,#6e6b61)}.designer-actions{justify-content:start}.card-actions{border-top:1px solid var(--color-border,#ded8cc);padding-top:12px;justify-content:start}.card-actions button{font-size:12px;background:transparent;padding:6px}.output-options{justify-content:start}.output-options label{display:flex;gap:7px;align-items:center}label{display:grid;gap:7px;font-size:13px}input,select,textarea,button{font:inherit;color:inherit;border:1px solid var(--color-border,#c9c1b1);border-radius:6px;padding:9px;background:var(--color-card,#fffdf8)}button{cursor:pointer}button:disabled{opacity:.5;cursor:default}.catalogue-settings-modal{width:min(550px,calc(100vw - 32px));max-height:90dvh;overflow:auto;border:1px solid #ccc4b5;border-radius:14px;padding:24px;background:var(--color-card,#fffdf8);color:inherit}.catalogue-settings-modal form,fieldset{display:grid;gap:18px}fieldset{border:0;padding:0}input,textarea,select{box-sizing:border-box;max-width:100%;min-width:0}@media(max-width:600px){h1{font-size:32px}.catalogue-settings-modal{width:100%;max-width:none;min-height:100dvh;max-height:100dvh;border:0;border-radius:0;margin:0;box-sizing:border-box}}
</style>
