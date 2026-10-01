<script setup lang="ts">
import { RouterLink } from 'vue-router';

/** Unknown route. */
defineOptions({ name: 'NotFoundPage' });
</script>

<template>
  <section class="panel p-12 text-center">
    <h1 class="font-editorial text-3xl">Sahifa topilmadi.</h1>
    <RouterLink to="/" class="btn-primary mt-6">Mahsulotlarga qaytish</RouterLink>
  </section>
</template>
