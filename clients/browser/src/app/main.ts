import "../ui/hud.css";
import "../ui/accessibility.css";
import { startClient, type ClientRuntime } from "./bootstrap";
import { LoadingScreen } from "./loading";
import { loadRuntimeConfig } from "../config/env";

let runtime: ClientRuntime | null = null;

async function main(): Promise<void> {
  const root = document.getElementById("app");
  if (root === null) {
    throw new Error('Missing #app container in index.html');
  }

  const loading = new LoadingScreen(root);
  loading.setStep("config", "active");
  try {
    const config = loadRuntimeConfig();
    loading.setStep("config", "done");
    runtime = await startClient({ root, loading, config });
  } catch (error) {
    const message = error instanceof Error ? error.message : "The client failed to start";
    loading.setStep("config", "failed");
    loading.fail(message);
    console.error(`[client] startup failed: ${message}`);
  }
}

void main();

if (import.meta.hot) {
  import.meta.hot.dispose(() => {
    runtime?.dispose();
    runtime = null;
  });
}
