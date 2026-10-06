# BrowSel

Elegí con qué navegador y perfil abrís un enlace, sin cambiar el navegador
predeterminado del sistema.

BrowSel se registra como manejador de enlaces `http` y `https`. Cuando hacés
clic en un link, aparece una ventana con todos los perfiles instalados; los que
ya están abiertos se marcan en verde. Elegís uno y el enlace se abre ahí.

## Características

- **Detecta los navegadores instalados**: Brave, Google Chrome, Microsoft Edge,
  Opera y Firefox. No hay que configurar nada a mano.
- **Detecta qué perfiles están abiertos** y los marca con un punto verde, para
  no abrir un perfil a ciegas.
  - En los navegadores Chromium se cruzan cuatro señales: la línea de comandos
    de los procesos vivos, el título de las ventanas, los archivos de bloqueo
    del perfil y, como último recurso, la lista de los últimos perfiles usados.
  - En Firefox se lee `profiles.ini` y se mira el archivo `parent.lock`.
- **Abre el perfil correcto**: en Chromium pasa `--profile-directory=`, y en
  Firefox usa `-P`. Si el perfil ya estaba abierto, el enlace se suma como
  pestaña nueva en lugar de abrir otra ventana.
- **Sin instalación ni servicio**: es un solo ejecutable que se registra en tu
  usuario de Windows. No pide permisos de administrador.
- **Apariencia nativa de Windows 11**: interfaz con WinUI 3, fondo Mica,
  tipografía y colores del sistema, y sigue al tema claro u oscuro
  automáticamente.
- **Ajustes**: en el engranaje de arriba marcás qué navegadores aparecen en la
  lista. El ajuste se recuerda.
- **Recuerda cómo lo dejaste**: qué navegadores están marcados y qué navegadores
  quedaron abiertos o plegados.
- **Argumentos sin riesgo**: la dirección se pasa directamente al proceso del
  navegador, nunca por una línea de comandos.

## Requisitos

- Windows 10 versión 1809 (17763) o superior. Developed y probado en Windows 11.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) para
  compilar. La aplicación incluye todo lo necesario para ejecutarse, no
  necesitás instalar nada después.

## Compilar

```powershell
git clone https://github.com/nocloudware/BrowSel.git
cd BrowSel
dotnet build BrowSel.csproj -c Release -p:Platform=x64
```

Queda en `bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\`.

## Instalar

```powershell
.\BrowSel.exe --register
```

Eso registra el manejador de enlaces en tu usuario de Windows. Después Windows
te muestra un cartel: **registrarse no es lo mismo que ser el predeterminado**.
Tenés que confirmarlo vos:

1. Abrí **Configuración → Aplicaciones → Aplicación predeterminada**.
2. Buscá **BrowSel**.
3. Asignalo a **HTTP** y a **HTTPS**.

Listo. La próxima vez que hagas clic en un link se abre BrowSel.

> Ojo: la carpeta `bin\...` es la aplicación. **No la borres ni la muevas**,
> porque el registro de Windows apunta al ejecutable que está ahí adentro.

## Desinstalar

```powershell
.\BrowSel.exe --unregister
```

Esto borra el registro del protocolo. Después sacá BrowSel de *Aplicación
predeterminada* y ya podés borrar la carpeta.

> `--unregister` borra también tus preferencias guardadas.

## Atajos de teclado

| Acción | Cómo |
| --- | --- |
| Abrir o plegar un navegador | Clic en su nombre |
| Abrir el enlace en un perfil | Clic en el perfil |
| Cerrar sin abrir nada | `Esc` |

## Cómo funciona

- **Descubrimiento**: busca los navegadores en las rutas estándar
  (Program Files, Program Files (x86), `%LOCALAPPDATA%`) y, si no los
  encuentra ahí, consulta el registro de Windows.
- **Perfiles**: los navegadores Chromium guardan sus perfiles bajo
  `User Data`; los nombres se sacan del archivo `Local State`. Firefox usa su
  `profiles.ini`.
- **Estado abierto**: los navegadores Chromium dejan un archivo de bloqueo por
  perfil mientras lo tienen abierto. BrowSel intenta abrirlos en exclusiva; si
  no puede, es que están en uso.
- **Registro**: se escribe bajo `HKEY_CURRENT_USER\Software\BrowSel` y
  `HKEY_CURRENT_USER\Software\Classes\BrowSelURL`. Nada del sistema se toca.

## Limitaciones

- Por cómo funciona Windows, no se puede dejar un solo navegador "nativo" con
  perfil para el sistema entero: BrowSel siempre muestra su ventana para que
  elijas.
- La detección de perfiles abiertos de Chromium depende de los archivos de
  bloqueo del navegador. Si una versión futura los cambia, la detección puede
  dejar de funcionar y todos los perfiles aparecerán como cerrados (el enlace
  igual se abre bien).
- Windows no permite que una aplicación se ponga de predeterminada sola: el
  paso 3 de la instalación hay que hacerlo a mano, siempre.

## Licencia

[MIT](LICENSE). Ver [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt) para los
componentes de terceros.