# Pruebas del prototipo web
Requisitos: Node 18+ y `playwright` (con Chromium). Antes: `cd Web && node build.js`.

- `node fuzz.js` — 450 partidas aleatorias (~46 000 acciones) con invariantes y comprobación de que los pasos reproducen el tablero final.
- `node ui_edge.js` — 23 casos límite de interfaz (Atrás durante animación, guardado corrupto, victoria/derrota, entrada simultánea…).
- `node perf.js` — fotogramas y memoria con CPU 4x más lenta.
