<script setup lang="ts">
import { useAppForm } from '@wow-two-beta/ui-vue/forms-engine/house';
import { Button } from '@wow-two-beta/ui-vue/presentation/actions';
import { Field, EmailInput, PasswordInput } from '@wow-two-beta/ui-vue/presentation/forms';

import type { AccessControls } from '@/application/workspace/UseAccess';

/** Presents the explicitly provisioned operator sign-in. */
defineOptions({ name: 'LoginPage' });
const props = defineProps<{ access: AccessControls }>();
const form = useAppForm(() => ({
  defaultValues: { email: '', password: '' },
  isDisabled: props.access.busy.value,
  onSubmit: async (values, { signal }) => {
    const result = await props.access.login(values, signal);
    form.setValue('password', '');
    if (result.ok) props.access.authenticated();
    return result;
  },
}));
</script>

<template>
  <main class="mx-auto grid min-h-screen max-w-6xl items-center gap-12 px-6 py-12 md:grid-cols-2 md:px-12">
    <div>
      <div class="mb-10 flex items-center gap-3">
        <span class="brand-mark" aria-hidden="true">b·</span><b>Brand Workspace</b>
      </div>
      <p class="eyebrow">Content &amp; preparation</p>
      <h1 class="mt-4 text-4xl font-medium leading-tight tracking-tight sm:text-5xl">
        Good work.<br />Ready to share.
      </h1>
      <p class="mt-6 max-w-md text-lg leading-8 text-muted-foreground">
        Keep original media, considered copy, and the exact approved version together.
      </p>
      <p class="mt-8 text-sm text-muted-foreground">Prepare and export manually. Social publishing is not connected.</p>
    </div>
    <section class="workspace-card p-7 sm:p-10" aria-labelledby="signin-title">
      <h2 id="signin-title" class="text-2xl font-semibold">Operator sign-in</h2>
      <template v-if="access.session.value?.setupRequired">
        <p class="mt-5 leading-7 text-muted-foreground">
          An administrator needs to provision the first operator before sign-in is available.
        </p>
        <p class="mt-4 text-sm text-muted-foreground">This workspace does not offer public registration.</p>
      </template>
      <form v-else class="mt-6 space-y-5" @submit.prevent="form.handleSubmit($event)">
        <form.Field v-slot="f" name="email" is-required
          ><Field label="Email"
            ><EmailInput v-model="f.value" autocomplete="username" required @blur="f.onBlur" /></Field
        ></form.Field>
        <form.Field v-slot="f" name="password" is-required
          ><Field label="Password"
            ><PasswordInput v-model="f.value" autocomplete="current-password" required @blur="f.onBlur" /></Field
        ></form.Field>
        <p v-if="form.state.submitError" role="alert" class="text-sm text-destructive">
          Sign-in was not completed. Check your credentials and retry; repeated failures may temporarily lock the
          account.
        </p>
        <Button type="submit" :is-loading="form.state.isSubmitting" :is-disabled="access.busy.value" is-full-width
          >Sign in</Button
        >
        <p class="text-xs leading-5 text-muted-foreground">
          Use the account provisioned by your administrator.
        </p>
      </form>
    </section>
  </main>
</template>
