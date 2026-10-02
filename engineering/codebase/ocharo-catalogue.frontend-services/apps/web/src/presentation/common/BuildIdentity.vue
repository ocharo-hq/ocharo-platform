<script setup lang="ts">
import { useUiLocale } from '../../application/shared/UseUiLocale';
import { useBuildIdentity } from '../../application/common/UseBuildIdentity';
import './BuildIdentity.css';

/** Displays the compiled app and independently reported backend build. */
defineOptions({ name: 'BuildIdentity' });
const appVersion = __APP_VERSION__;
const { t } = useUiLocale();
const { backendVersion, refresh } = useBuildIdentity();
function open(event: Event): void {
  if ((event.target as HTMLDetailsElement).open) void refresh();
}
</script>

<template>
  <details class="build-identity" @toggle="open">
    <summary :aria-label="t('About this app: {version}', { version: appVersion })">{{ appVersion }}</summary>
    <dl>
      <dt>{{ t('App') }}</dt>
      <dd>{{ appVersion }}</dd>
      <dt>{{ t('Backend') }}</dt>
      <dd>{{ t(backendVersion) }}</dd>
    </dl>
  </details>
</template>
