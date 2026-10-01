import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';

import { compileScript, compileTemplate, parse } from '@vue/compiler-sfc';

/** Compiles SFC runtime macros as well as templates; vue-tsc alone cannot validate macro extraction. */
async function checkDirectory(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) await checkDirectory(path);
    else if (entry.name.endsWith('.vue')) {
      const { descriptor, errors } = parse(await readFile(path, 'utf8'), { filename: path });
      if (errors.length) throw new Error(`${path}: ${errors.join('\n')}`);
      const script = compileScript(descriptor, { id: path });
      const template = compileTemplate({
        source: descriptor.template?.content ?? '',
        filename: path,
        id: path,
        compilerOptions: { bindingMetadata: script.bindings },
      });
      if (template.errors.length) throw new Error(`${path}: ${template.errors.join('\n')}`);
    }
  }
}

await checkDirectory('src');
