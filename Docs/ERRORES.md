# Registro de errores encontrados y corregidos

| # | Qué pasó | Causa | Corrección |
|---|---|---|---|
| 1 | Dos pruebas de detección fallaban | Mis tableros de prueba tenían líneas extra (no era el código) | Tableros rearmados y revisados a mano |
| 2 | Pruebas de potenciadores fallaban | El tablero de prueba no tenía movimientos y el juego lo reordenó (comportamiento correcto) | Se planta un movimiento posible (`T.WithMove`) |
| 3 | Una jugada daba miles de puntos | El multiplicador de cadena no tenía tope | Tope x5 (`ScoreManager.MaxMultiplier`) |
| 4 | Niveles 1-10 se ganaban en 1-6 movimientos | El bot "veía" las fichas futuras (clon con el mismo azar) y objetivos mal escalados | `CloneReseeded`; objetivos recalibrados con datos medidos |
| 5 | Nivel 27/30 imposibles | Las piedras indestructibles caían sobre celdas de gelatina y bloqueaban el objetivo | Las piedras ahora las destruyen efectos de especiales y potenciadores |
| 6 | Stub de compilación de Unity fallaba | Error en mi stub (campo sin asignar) | Corregido; el código del juego compiló sin cambios |
| 7 | El guardado podía lanzar excepción al no poder escribir | `Save()` sin protección | `Save()` captura errores y el juego continúa |
| 8 | (Revisión estática, sin ejecutar) Con escena vacía no se oiría ningún sonido | La cámara creada por código no tenía `AudioListener` | Se añade `AudioListener` a la cámara |
| 9 | (Revisión estática, sin ejecutar) Un toque sobre un botón contaba también sobre el tablero | `InputController` no consultaba la interfaz | Se ignora el toque si el puntero está sobre la UI |

Los errores 8 y 9 se encontraron leyendo el código; no se han reproducido en Unity.

Pendiente de descubrir: cualquier error que solo aparezca al ejecutar en Unity (no probado aquí).
