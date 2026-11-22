export {};
declare global {
  interface Window { csPanelLayoutV1: { add(node: Element | null, parts?: string): void; moved(node: Element | null): void; drag(node: Element | null): void } }
}
