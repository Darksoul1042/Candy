# Pétalo Pop — estado (v0.2)

Juego Match-3 original (nombre, formas, colores, sonidos y código propios). Candy Crush solo se usó como referencia del
género; no se copió código, gráficos, sonidos ni niveles. Del XAPK solo se hizo un inventario de tamaños.

## Arquitectura
- `Assets/Scripts/Core` (asmdef sin dependencias de Unity): Board (capas: tipo, especial, vida, gelatina), MatchDetector,
  GravitySystem, MoveFinder, BoardGenerator (+reordenado), SpecialTileSystem, ObstacleSystem, ObjectiveManager,
  BoosterManager, ScoreManager, MoveManager, GameSession (orquesta y devuelve `Step`s), LevelConfig, LevelCatalog (30 niveles),
  SaveSystem (texto clave=valor, escritura atómica), GameRng (azar determinista y clonable), Bot (pruebas de equilibrio).
- `Assets/Scripts/Game` (Unity): GameManager, BoardView (pooling), TileView, AnimationManager, InputController, UIManager,
  AudioManager (efectos y música de fondo sintéticos, composición propia), Fx (partículas con pooling), SpriteFactory (gráficos por código), LevelDefinition (ScriptableObject).
- `Assets/Scripts/Editor`: menú *PetaloPop > Generate Level Assets* (crea los 30 `Level_XX.asset`) y *Build Android APK*.
- `Tests/CoreTests`: pruebas de la lógica (`dotnet run --project Tests/CoreTests`). `Tests/Calib`: herramienta de medición de equilibrio.

## Implementado y VERIFICADO (ejecutado aquí): 49 pruebas, 0 fallos
Tablero, intercambio, líneas, gravedad, cadenas · especiales (4 en línea → raya, L/T → bomba, 5 → color) y 6 combos ·
activación en cadena · gelatina, hielo (1-2 capas), cajas (1-3 capas), piedras · objetivos (recoger color, gelatina, hielo, cajas) ·
5 potenciadores (martillo, cambio libre, quitar obstáculo, +5 movimientos, crear bomba) · progreso y guardado (ida y vuelta,
datos corruptos, archivo real) · 30 niveles generables · contrato "reproducir pasos = tablero final" con especiales/vida/gelatina ·
simulaciones aleatorias con invariantes (tablero lleno, sin líneas, siempre hay movimiento).

Equilibrio (12 intentos por bot; primera calibración, no balance definitivo). Jugador voraz sin potenciadores: gana 12/12 los
niveles 1-10, entre 5 y 12 de 12 en los niveles 11-25, y solo 0-4 de 12 en los niveles 26-30 (0/12 en el 30). Con los potenciadores
iniciales gana al menos 3/12 en los 30 niveles (3/12 en el 28, 6/12 en el 27, 8/12 en el 30). Un jugador al azar gana 0-8/12 según el
nivel. Los primeros niveles son fáciles para un bot; los últimos exigen potenciadores. La prueba automática solo exige que todos sean
ganables (con potenciadores) y los 25 primeros sin ellos; la dificultad real hay que ajustarla con jugadores.

## Escrito pero NO verificado en Unity
La capa `Game/` y `Editor/` compila contra stubs mínimos de la API (detecta errores de sintaxis y tipos), pero **no se ha
abierto en Unity**: no hay Unity ni Android SDK aquí. Pueden aparecer errores o fallos visuales al ejecutarla. Pensada para
Unity 2022.3 LTS con Input Manager clásico. Sin pruebas de rendimiento en dispositivo.
Para jugar: abrir la carpeta con Unity 2022.3, *PetaloPop > Generate Level Assets* (opcional; sin assets usa el catálogo),
Play en cualquier escena vacía.

## Desviaciones del encargo
Textos con uGUI `Text` (fuente integrada) y no TextMeshPro; sin Universal Render Pipeline (no aporta en 2D simple);
animaciones por corrutinas y no con el sistema Animator; partículas propias con pooling.
Reglas de diseño decididas: multiplicador de cadena con tope x5; piedras solo las destruyen efectos de especiales/potenciadores.

## FASE 10 — APK: NO generado
No hay Unity ni Android SDK en este entorno, así que no hay APK. Para generarlo: Unity 2022.3 LTS + módulo Android, abrir el
proyecto, menú *PetaloPop > Build Android APK* (IL2CPP, ARM64, minSdk 24, `com.petalopop.game`). Para publicarlo hace falta firmarlo
con una clave propia. El tamaño final no se ha medido.

## Cómo compilar
Ver `Docs/COMO_COMPILAR.md` (en tu equipo, o en la nube con el flujo `.github/workflows/build-android.yml`; ninguno probado aún).

## Pendiente
Abrir en Unity y corregir lo que aparezca; TextMeshPro; ajustes separados de música y efectos (hoy un solo botón); pruebas de rendimiento y memoria en un
móvil real; ajuste fino de dificultad con jugadores reales; APK firmado.

## Rendimiento de la lógica (medido en este servidor, NO en un móvil)
`dotnet run --project Tests/Perf -c Release`: 2000 jugadas aleatorias por nivel. Cada jugada completa (incluida la búsqueda de
movimientos) tarda ~0.04-0.11 ms de media; el peor caso medido fue 4 ms. Asigna 7-13 KB por jugada (poca presión para el recolector
de basura). Esto solo cubre la lógica: animaciones, dibujado y memoria en Android no se han medido. Unity y el SDK de Android no son
accesibles desde este entorno (descargas bloqueadas), por eso no hay APK ni pruebas en Unity.
