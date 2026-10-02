import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { runInNewContext } from "node:vm";
import type { IncomingMessage, ServerResponse } from "node:http";
import { describe, expect, it, vi } from "vitest";
import { proxyUnavailable } from "../dev/ProxyProblem";
const root = resolve(process.cwd(), "../../..");
const appPaths = [
  "brand-workspace.frontend-services/apps/web",
  "ocharo-brand.frontend-services/apps/web",
];
function boot(
  path: string,
  values: Record<string, string> = {},
  pathname = "/",
) {
  const toggle = vi.fn();
  const style: { colorScheme?: string } = {};
  const storage = { getItem: (key: string) => values[key] ?? null };
  runInNewContext(
    readFileSync(resolve(root, path, "public/theme.js"), "utf8"),
    {
      document: { documentElement: { classList: { toggle }, style } },
      localStorage: storage,
      sessionStorage: storage,
      location: { pathname },
    },
  );
  return { toggle, style };
}
describe("first paint and proxy contracts", () => {
  it.each(appPaths)(
    "loads external first-paint assets before the app for %s",
    (path) => {
      const html = readFileSync(resolve(root, path, "index.html"), "utf8");
      expect(html.indexOf("theme.js")).toBeLessThan(html.indexOf('src="/src/'));
      expect(html).toContain("%BASE_URL%boot-splash.css");
      const css = readFileSync(
        resolve(root, path, "public/boot-splash.css"),
        "utf8",
      );
      expect(css).toMatch(/html/);
      expect(css).toMatch(/body/);
      expect(css).toContain("background");
      const actual = boot(path);
      expect(actual.style.colorScheme).toBe("light");
    },
  );
  it.each(["GET", "HEAD"])(
    "returns safe structured transport problems for %s without exposing proxy errors",
    (method) => {
      const writeHead = vi.fn(),
        end = vi.fn();
      const res = { writeHead, end, headersSent: false, writableEnded: false };
      proxyUnavailable(
        new Error("private backend certificate detail"),
        { method, url: "/api/example?secret=hidden" } as IncomingMessage,
        res as unknown as ServerResponse,
      );
      expect(writeHead).toHaveBeenCalledWith(
        503,
        expect.objectContaining({
          "Content-Type": "application/problem+json",
          "Cache-Control": "no-store",
        }),
      );
      if (method === "HEAD") expect(end).toHaveBeenCalledWith(undefined);
      else {
        const payload = JSON.parse(end.mock.calls[0]![0]);
        expect(payload).toMatchObject({
          status: 503,
          type: "about:blank",
          instance: "/api/example",
        });
        expect(payload.traceId).toBe(payload.requestId);
        expect(payload.detail).not.toContain("private");
        expect(payload.instance).not.toContain("secret");
      }
    },
  );
  it("does not write a second response or an HTTP body into a raw socket", () => {
    const end = vi.fn();
    proxyUnavailable(
      new Error(),
      {} as IncomingMessage,
      {
        headersSent: true,
        writeHead: vi.fn(),
        end,
      } as unknown as ServerResponse,
    );
    expect(end).not.toHaveBeenCalled();
  });
});

it.each(["aborted", "destroyed"])(
  "keeps disconnected %s requests silent",
  (key) => {
    const writeHead = vi.fn(),
      end = vi.fn();
    const request = { aborted: key === "aborted" };
    const response = { destroyed: key === "destroyed", writeHead, end };
    proxyUnavailable(
      new Error("private disconnect"),
      request as IncomingMessage,
      response as unknown as ServerResponse,
    );
    expect(writeHead).not.toHaveBeenCalled();
    expect(end).not.toHaveBeenCalled();
  },
);

it("copies the retained Brand proxy helper and first-paint assets into its container build", () => {
  const docker = readFileSync(
    resolve(root, "../deployment/brand/Dockerfile"),
    "utf8",
  );
  for (const folder of ["dev", "public"]) {
    expect(docker).toContain(
      `COPY ocharo-brand.frontend-services/apps/web/${folder}/ ./apps/web/${folder}/`,
    );
    const file = folder === "dev" ? "ProxyProblem.ts" : "theme.js";
    expect(
      readFileSync(
        resolve(root, "ocharo-brand.frontend-services/apps/web", folder, file),
        "utf8",
      ),
    ).not.toBe("");
  }
});
