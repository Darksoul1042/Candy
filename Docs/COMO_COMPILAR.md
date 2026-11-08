# Cómo compilar el APK de Pétalo Pop

El APK **no se ha generado todavía**: el entorno donde se desarrolló no puede descargar Unity ni el SDK de Android (sitios bloqueados por la política de red).
Tienes dos caminos. Ninguno se ha probado de extremo a extremo.

## Camino A — En tu ordenador (el más directo)
1. Instala **Unity Hub** y, desde él, **Unity 2022.3 LTS** con los módulos **Android Build Support**, **Android SDK & NDK Tools**, **OpenJDK** y **IL2CPP**.
2. En Unity Hub: *Add* → carpeta `PetaloPop`. Ábrelo (la primera vez tarda en importar).
3. Mira la consola: si hay errores de compilación, **pásamelos** y los corrijo. Pulsa Play en cualquier escena para probar el juego.
4. Menú **PetaloPop → Build Android APK**. Resultado: `Build/PetaloPop.apk` (ARM64, IL2CPP, mínimo Android 7.0 / API 24, `com.petalopop.game`), firmado con la clave de depuración de Unity.
5. Instálalo en tu móvil (activa "instalar apps desconocidas") o con `adb install Build/PetaloPop.apk`.

## Camino B — En la nube con GitHub (sin instalar Unity)
1. Sube la carpeta `PetaloPop` como repositorio de GitHub (la carpeta `.github/workflows` debe quedar en la raíz del repositorio).
2. Activa tu licencia gratuita de Unity Personal para CI siguiendo https://game.ci/docs/github/activation (se obtiene un archivo de licencia).
3. En el repositorio: *Settings → Secrets and variables → Actions* y crea `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
4. *Actions → Build Android APK → Run workflow*. Al terminar, descarga el artefacto `PetaloPop-apk`.

## Firma para publicar (Google Play)
El APK de depuración sirve para probar, no para publicar. Con tu propia clave (no la compartas en ningún chat) define estas variables de entorno antes de compilar:
`PETALOPOP_KEYSTORE` (ruta), `PETALOPOP_KEYSTORE_PASS`, `PETALOPOP_KEY_ALIAS`, `PETALOPOP_KEY_PASS`. `BuildScript` las usa si existen.
Para Google Play se recomienda además generar un `.aab` (Build Settings → Build App Bundle); eso aún no está automatizado.

## Después de compilar
Mide el tamaño real del APK y pruébalo en un móvil: rendimiento, memoria, tamaños de pantalla. Esas pruebas siguen pendientes.

## Camino C — APK del prototipo web (sin Unity ni licencia)
`Android/` contiene un envoltorio WebView mínimo que empaqueta `Web/` (el prototipo jugable).
Súbelo a tu repositorio de GitHub y ejecuta el workflow `build-web-apk` (Actions → Run workflow);
descarga `PetaloPopWeb-debug-apk`. Es un APK de depuración firmado con la clave debug de Android:
sirve para instalar y probar, no para publicar. SIN PROBAR (no hay SDK de Android en el entorno de desarrollo).
