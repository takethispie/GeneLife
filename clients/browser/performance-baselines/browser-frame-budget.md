# Browser frame-budget report

Generated: 2026-11-26T21:51:15.413Z

| Field | Value |
|---|---|
| Fixture set | `stress` (2000 genoses with appearance) |
| Genoses visible (peak) | 1600 |
| Viewport | 1280x720 @ 1x |
| Renderer | webgl |
| Samples | 92 over 10s |
| Frame p50 | 6.95 ms |
| Frame p95 | 6.95 ms |
| Frame p99 | 6.98 ms |
| FPS min / mean | 143.2 / 144.0 |
| Budget (p95) | 16.7 ms |
| **Verdict** | **PASS** |

p95 frame time is compared against budgetMs (16.7ms = 60fps). Headless runs use SwiftShader and are NOT reference-hardware numbers.
