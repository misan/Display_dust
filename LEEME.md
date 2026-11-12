# Mota de polvo

Abre **MotaPantalla.exe** con doble clic. No necesita instalación.

- Muestra un fondo blanco a pantalla completa.
- Haz clic con el botón izquierdo en el centro de la mota: aparece una circunferencia negra de **10 píxeles de radio**, sin relleno.
- El cursor tiene un círculo central de **3 píxeles de radio** con interior transparente para ver la mota mientras apuntas. Las guías de la cruz quedan fuera del círculo; el clic corresponde exactamente a su centro.
- La coordenada aparece abajo a la derecha. El origen **X: 0, Y: 0** es la esquina superior izquierda del monitor; X crece hacia la derecha e Y hacia abajo.
- Cada clic en una posición distinta de la última añade un punto al histórico y lo guarda inmediatamente. Aparta el puntero para ver bien la mota.
- Pulsa **H** para mostrar u ocultar el histórico: círculos de **3 píxeles de radio**, unidos por líneas rectas en el orden de los clics. El último punto muestra ambos círculos, de **3 y 10 píxeles de radio**.
- Al volver a abrir el programa se recuperan la marca, el histórico y el monitor utilizado. El histórico empieza oculto; pulsa H para verlo.
- Pulsa **Esc** o **Alt+F4** para salir.

La primera vez se abre en el monitor donde esté el puntero. Usa píxeles físicos, también si Windows tiene un escalado del 125 %, 150 % u otro valor. Cerca del borde, la parte de la circunferencia que queda fuera de la pantalla se recorta.

El histórico se guarda en `%LOCALAPPDATA%\MotaPantalla\posicion.xml`. La posición guardada con la versión anterior se conserva como primer punto. Si el monitor guardado está desconectado, se utiliza el monitor del puntero. Si cambias la resolución o la orientación, las coordenadas anteriores no se transforman; puedes resetear el histórico y volver a marcar la mota.

Para **borrar todo el histórico**, cierra la app y ejecútala con la opción `/R` (también admite `/r`) desde PowerShell, en la carpeta del programa:

```powershell
.\MotaPantalla.exe /R
```

La opción `/R` elimina directamente el archivo del histórico y abre una pantalla blanca sin marcas con el aviso **Histórico borrado**. El borrado persiste aunque salgas sin hacer clic. Los clics siguientes crean un histórico nuevo. Si Windows impide borrar el archivo, aparece un mensaje de error y la aplicación se cierra para no dar el reseteo por realizado.

En **Git Bash / MINGW64** también funciona `./MotaPantalla.exe /R`: la aplicación reconoce la conversión automática de `/R` en `R:/` que hace Bash al lanzar un ejecutable de Windows. Puedes usar también `./MotaPantalla.exe --reset` o `./MotaPantalla.exe -r`. Una opción desconocida muestra un error en lugar de cargar silenciosamente el histórico.

Incluye el código fuente. Para recompilar en Windows de 64 bits con .NET Framework 4.x, ejecuta `compilar.cmd`.
