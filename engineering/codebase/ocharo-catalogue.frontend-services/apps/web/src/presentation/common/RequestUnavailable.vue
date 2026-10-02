<script setup lang="ts">
import { useUiLocale } from '../../application/shared/UseUiLocale';
import ResultEmpty from './ResultEmpty.vue';
const { t } = useUiLocale();
</script>
<template><ResultEmpty :label="t('Content unavailable')" /></template>
