<script setup lang="ts">
import type { AccessControls } from '@/application/workspace/UseAccess';
import type { WorkspaceControls } from '@/application/workspace/UseWorkspace';
import { useUiLocale } from '@/application/shared/UseUiLocale';
import { designEditorUrl } from '@/domain/workspace/EditorNavigation';
import CatalogueGalleryPage from './CatalogueGalleryPage.vue';
import CatalogueWebDesigner from './CatalogueWebDesigner.vue';
import DesignEditorPage from './DesignEditorPage.vue';
defineOptions({name:'CatalogueWorkspacePage'});
const props=defineProps<{access:AccessControls;workspace:WorkspaceControls;initialDocumentId?:string|null;initialPageId?:string|null;navigationError?:string|null;initialMode?:'web'|'pdf'|null}>();
const {t}=useUiLocale();
function open(id:string,mode:'web'|'pdf'){props.access.navigate(designEditorUrl(props.workspace.organization.value!.id,'catalogue',id,null,mode));}
</script>
<template><div><nav v-if="initialDocumentId" class="catalogue-navigation"><button @click="access.navigate(designEditorUrl(workspace.organization.value!.id,'catalogue'))">{{t('← Catalogue gallery')}}</button><span>{{initialMode==='web'?t('Web designer'):t('PDF designer')}}</span></nav><CatalogueGalleryPage v-if="!initialDocumentId" :access="access" :workspace="workspace" @open="open"/><CatalogueWebDesigner v-else-if="initialMode==='web'" :access="access" :workspace="workspace" :document-id="initialDocumentId"/><DesignEditorPage v-else :access="access" :workspace="workspace" kind="catalogue" :initial-document-id="initialDocumentId" :initial-page-id="initialPageId??null" :navigation-error="navigationError??null"/></div></template>
<style scoped>.catalogue-navigation{display:flex;gap:20px;align-items:center;margin:0 0 20px}.catalogue-navigation button{background:transparent;border:0;text-decoration:underline;cursor:pointer;color:inherit;font:inherit}.catalogue-navigation span{font-size:13px;opacity:.7}</style>
