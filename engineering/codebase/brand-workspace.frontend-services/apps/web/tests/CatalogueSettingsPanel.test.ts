import { deskSelect, chooseDeskValue } from './helpers/DeskSelect';
import { flushPromises, mount } from "@vue/test-utils";
import { ref } from "vue";
import { afterEach, describe, expect, it, vi } from "vitest";
import CatalogueSettingsPanel from "@/presentation/workspace/CatalogueSettingsPanel.vue";
import { useUiLocale } from "@/application/shared/UseUiLocale";
import type { WorkspaceControls } from "@/application/workspace/UseWorkspace";
import type { AccessControls } from "@/application/workspace/UseAccess";
import { createCatalogueCover } from "@/domain/workspace/CatalogueComposer";
import { initialCommerce } from "@/domain/workspace/Commerce";
import { translateDesignerNotice } from "@/domain/shared/DesignerUiMessages";
const { get } = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock("@/application/workspace/UseCommerce", () => ({
  useCommerce: () => ({ get }),
}));
const org = "11111111-1111-1111-1111-111111111111",
  brand = "22222222-2222-2222-2222-222222222222";
const controls = useUiLocale();
afterEach(() => {
  controls.setUiLocale("en");
  vi.clearAllMocks();
});
async function mountPanel() {
  const snapshot = createCatalogueCover("en");
  get.mockResolvedValue({
    ok: true,
    value: {
      brandId: brand,
      revision: 1,
      snapshot: initialCommerce("Ocharo", "ocharo"),
      updatedAt: "2026-10-01",
    },
  });
  const workspace = {
    organization: ref({ id: org }),
    brands: ref([{ id: brand, name: "Ocharo" }]),
  } as unknown as WorkspaceControls;
  const wrapper = mount(CatalogueSettingsPanel, {
    props: {
      access: {} as AccessControls,
      workspace,
      snapshot,
      pageId: snapshot.pages[0]!.id,
      selectedId: null,
      disabled: false,
    },
  });
  await flushPromises();
  return { wrapper, snapshot };
}
describe("independent catalogue and operator languages", () => {
  it("keeps English catalogue content when the operator selects Uzbek or Russian", async () => {
    controls.setUiLocale("uz");
    const { wrapper, snapshot } = await mountPanel();
    const before = JSON.stringify(snapshot);
    expect(wrapper.text()).toContain("Katalog tili");
    expect(deskSelect(wrapper, controls.t('Catalogue language')).props('modelValue')).toBe("en");
    controls.setUiLocale("ru");
    await flushPromises();
    expect(wrapper.text()).toContain("Язык каталога");
    expect(JSON.stringify(snapshot)).toBe(before);
    expect(wrapper.emitted("change")).toBeUndefined();
    wrapper.unmount();
  });
  it("changes only the catalogue locale and flags untranslated shared text", async () => {
    controls.setUiLocale("uz");
    const { wrapper, snapshot } = await mountPanel();
    await chooseDeskValue(wrapper, controls.t('Catalogue language'), 'fr');
    const change = wrapper.emitted("change")![0]![0] as typeof snapshot;
    expect(change.locale).toBe("fr");
    expect(controls.uiLocale.value).toBe("uz");
    expect(snapshot.locale).toBe("en");
    expect(wrapper.text()).toContain("fr tilidagi umumiy matnini");
    expect(
      change.pages[0]!.elements.find(
        (item) => item.binding?.field === "collection",
      )?.text,
    ).toBe("");
    wrapper.unmount();
  });
  it("emits one replacement for a global photo-group switch and translates dynamic notices", async () => {
    const { wrapper } = await mountPanel();
    await chooseDeskValue(wrapper, controls.t('Photo group for all products'), 'model');
    expect(wrapper.emitted("change")).toHaveLength(1);
    expect(
      (
        wrapper.emitted("change")![0]![0] as ReturnType<
          typeof createCatalogueCover
        >
      ).catalogueSettings?.mediaGroup,
    ).toBe("model");
    controls.setUiLocale("ru");
    expect(
      translateDesignerNotice(
        "No model images for abaya-v01; existing photographs retained.",
        controls.t,
      ),
    ).toContain("abaya-v01");
    expect(
      translateDesignerNotice(
        "No model images for abaya-v01; existing photographs retained.",
        controls.t,
      ),
    ).not.toContain("No model");
    wrapper.unmount();
  });
});

it("retains saved product choices and sends refresh failures only to the toast", async () => {
  controls.setUiLocale("en");
  const snapshot = createCatalogueCover("en");
  const commerce = initialCommerce("Ocharo", "ocharo");
  commerce.products.push({
    document: { id: "saved-product", status: "catalogue" },
  } as unknown as (typeof commerce.products)[number]);
  get.mockResolvedValue({
    ok: true,
    value: {
      brandId: brand,
      revision: 1,
      snapshot: commerce,
      updatedAt: "2026-10-01",
    },
  });
  const workspace = {
    organization: ref({ id: org }),
    brands: ref([{ id: brand, name: "Ocharo" }]),
  } as unknown as WorkspaceControls;
  const wrapper = mount(CatalogueSettingsPanel, {
    props: {
      access: {} as AccessControls,
      workspace,
      snapshot,
      pageId: snapshot.pages[0]!.id,
      selectedId: null,
      disabled: false,
    },
  });
  await flushPromises();
  get.mockResolvedValueOnce({
    ok: false,
    failure: { code: "http", status: 503, message: "private server response" },
  });
  await wrapper
    .findAll("button")
    .find((button) => button.text() === "Refresh saved product values")!
    .trigger("click");
  await flushPromises();
  expect(wrapper.text()).not.toContain("private");
  expect(wrapper.find('[role="alert"]').exists()).toBe(false);
  expect(wrapper.find("fieldset").attributes("disabled")).toBeUndefined();
  await wrapper
    .findAll("button")
    .find((button) => button.text() === "Connect products in a new copy")!
    .trigger("click");
  expect(wrapper.emitted("connect")).toHaveLength(1);
  wrapper.unmount();
});
