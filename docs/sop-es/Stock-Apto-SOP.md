# Procedimiento del área de stock apto

[中文](../sop/良品区操作SOP.md) | [English](../sop-en/Good-Stock-SOP.md) | [Español](../sop-es/Stock-Apto-SOP.md)

[Volver al índice](README.md) | [Guía multilingüe](Language-Guide.md)

Revisión de traducción/índice: 2026-11-09. Se conserva el estado del proceso; las reglas pendientes siguen sin confirmar.

Fuente actualizada: 2026-10-21. Aplicación: **Scanner.AndroidTester (PDA Yicang)**, con “查看良品区” (consultar stock apto) y “自动匹配” (asociación automática) en WPF. Incluye asignación de SN a pallets, carga, verificación, cambios de pallet dentro del área y pasos posteriores. Escanear no acredita que el equipo haya aprobado la inspección.

Estado: instrucciones del sistema contrastadas con el código; validación en almacén pendiente. Faltan reglas de admisión, autorización de movimientos, doble revisión y entrega (sección 9).

## 1. Preparación

1. Usar la aplicación PDA correcta. Verificar red, fecha, hora y zona horaria. Instalación: [uso y mantenimiento — chino](../系统使用与维护.md).
2. Localizar los equipos según la lista confirmada; comprobar SN físico, pallet actual y finalidad. El nombre “stock apto” de la pantalla no concede por sí solo la condición de aprobado o vendible.
3. Verificar que el número físico del pallet sea un **entero entre 1 y 100**, y que el SN sea legible. El número de pallet no equivale automáticamente a una ubicación de estantería.
4. Abrir el escaneo de stock apto y revisar lista y cantidad pendientes. Resolver las cargas anteriores antes de empezar, sin sobrescribir ni borrar datos.
5. Configurar el lector como teclado con Enter al final. Esta pantalla acepta solo SN; no pares SKU/SN, guías ni códigos de ubicación.

## 2. Controles del PDA

El idioma predeterminado es inglés; se puede elegir English / Español en el inicio. Los controles se muestran en el idioma elegido. La columna inglesa permite reconocerlos si se usa English.

| Referencia china | Control en inglés | Función |
| --- | --- | --- |
| 良品区扫描 | Scan good stock | Abrir la asignación de SN a pallets |
| 托盘号 | Casilla 1–100 y flechas | Pallet de los escaneos siguientes |
| 记录 | Record | Registrar entrada manual; Enter también registra |
| 上传 | Upload | Cargar toda la lista pendiente, incluso varios pallets |
| 完成 | Done | Volver; no carga ni completa la ubicación en inventario |

## 3. Escaneo inicial

1. Introducir/seleccionar el pallet físico y devolver el foco al campo SN.
2. Escanear el código de barras/QR del SN, o escribir el SN completo y registrar. Conservar ceros iniciales; no usar solo los últimos dígitos.
3. Comprobar la confirmación y **SN, pallet y hora de escaneo** en cada registro. El sonido del lector no confirma que se haya guardado.
4. Comparar cada SN y la cantidad con el pallet. Se eliminan espacios iniciales/finales; la longitud admitida es de 1–100 caracteres. Durante la ejecución actual se detectan duplicados sin distinguir mayúsculas.
5. Es más fácil entregar el trabajo si se verifica y carga cada pallet antes de cambiar al siguiente. Si se escanean varios seguidos, comprobar el nuevo número en los registros.

**Cambiar la casilla solo afecta a nuevos escaneos.** No hay edición ni eliminación individual. Ante un SN o pallet incorrecto, detenerse y seguir la sección 7.

## 4. Cargar y comprobar

1. Pausar el escaneo y revisar toda la lista pendiente, incluidos otros pallets. Upload envía todos los registros de la lista capturada al iniciar la carga.
2. Cargar y esperar. Cada lote contiene como máximo 1.000 registros; los confirmados se retiran de pendientes.
3. Comprobar la cantidad aceptada y pendientes igual a cero. Si falla una parte, los lotes confirmados ya se retiraron; reintentar solo lo pendiente, sin volver a escanear lo confirmado.
4. En WPF, abrir **良品区 → 查看良品区**, actualizar desde el servidor y comparar **SN completos, pallet y cantidad**. No basta con que los totales coincidan.
5. Si faltan datos, revisar actualización, entorno/servidor, pallet y red. La confirmación de carga no garantiza la asignación actual: un escaneo antiguo puede no sustituir uno más reciente. Pedir a mantenimiento que revise las horas.
6. Cerrar o actualizar la aplicación solo después de confirmar todo.

Los pendientes se guardan **solo en memoria** y se pierden al forzar el cierre, terminar el proceso, reiniciar o borrar datos. Mantener la aplicación abierta y reintentar cuando vuelva la red. Done no reinicia la detección de duplicados.

## 5. Movimiento dentro del área / cambio de pallet

### 5.1 Alcance y límites

Aquí se cambia un **SN activo del área del pallet A al B**, escaneándolo de nuevo con el pallet de destino y cargándolo. No existe pantalla específica de traslado ni intercambio automático de pallets completos.

Mantenimiento debe verificar antes el servicio nuevo, el índice único de SN activos, los acuses de escaneo y las migraciones de ciclos. El código no demuestra que producción ya disponga de esa función.

No aplicar este método a:

- Equipos ya ubicados que cambian de estantería: falta un procedimiento específico; consultar al responsable.
- Equipos ya ubicados que vuelven al área: el escaneo puede abrir un ciclo nuevo; confirmar el motivo real de retorno/reparación.
- Escaneo de etiquetas de ubicación: solo registra datos maestros de ubicaciones; no vincula SN ni cambia su pallet.

### 5.2 Revisar origen y destino

1. Actualizar en WPF el stock activo y registrar SN completos, origen A, destino B y cantidad. Comprobar ambos pallets físicamente.
2. Confirmar autorización y capacidad del destino. Asignar cada grupo de SN a un solo operador/dispositivo para evitar movimientos simultáneos.
3. Cargar y verificar los pendientes anteriores. Resolver primero registros con pallet incorrecto; no cargarlos como trabajo normal.
4. Un SN escaneado durante esta ejecución seguirá figurando como duplicado aunque ya se haya cargado. **Solo con cero pendientes, verificación en WPF y ningún trabajo sin terminar**, cerrar el proceso y abrir de nuevo para reiniciar los duplicados. Volver al inicio no basta. No desinstalar, borrar datos ni reiniciar para eliminar pendientes.

### 5.3 Escanear con el pallet de destino

Esta secuencia usa funciones existentes; las autorizaciones y firmas deben confirmarse en almacén.

1. En la nueva sesión, elegir B, revisar el número y enfocar el campo SN.
2. Mover solo los equipos verificados y escanear cada SN completo al colocarlo en B; comprobar B en la lista. No escanear anticipadamente equipos cuyo movimiento sea incierto.
3. Comparar los equipos físicos con la lista. Detenerse si cambia el alcance, hay errores o cambia el destino; no cargar una lista con diferencias.
4. Cargar hasta dejar cero pendientes. Si falla, conservar aplicación y lista; avisar de que la mercancía se movió físicamente pero el sistema sigue sin confirmar, para evitar búsquedas en la ubicación anterior.
5. **Actualizar desde el servidor** en WPF. Los SN movidos deben desaparecer de A y aparecer en B; los no movidos permanecen en A.
6. Comprobar disminución en origen, aumento en destino y total activo sin cambios. Suspender la entrega ante datos antiguos o duplicados; mantenimiento revisará horas, migraciones y concurrencia. No borrar datos directamente.
7. Conservar SN, origen/destino, cantidad, hora real, operador, revisor, resultado de carga e incidencias. Faltan por definir el soporte oficial y el plazo de conservación.

Ejemplo: de cinco equipos en el pallet 3, mover dos al 8. Elegir 8 y escanear solo esos dos. Tras cargar y actualizar, quedan tres en el 3 y se añaden dos al 8; el total no cambia. Son valores de ejemplo.

### 5.4 Efectos que deben revisarse

- En el ciclo activo, el servidor usa la hora del escaneo; un registro antiguo puede no sustituir al reciente. Sincronizar los PDA; no adelantar el reloj para forzar cambios.
- Reintentar conserva el identificador original y solicita confirmación. Un movimiento real necesita un escaneo nuevo; reenviar datos anteriores no mueve equipos.
- No se ha identificado actualización automática de `location` en órdenes ya generadas. La tablet puede conservar la ubicación anterior.
- La asociación automática usa los datos de escaneo en su clave de origen. Un nuevo escaneo puede parecer un origen nuevo. Si existe orden, el responsable debe revisar tarea y destino; no crear duplicados para corregir ubicación.

## 6. Órdenes de inspección y ubicación en inventario

Cargar desde el PDA registra la asignación; no completa inspección, órdenes ni ubicación definitiva.

1. Importar la tabla base verificada en WPF y usar 自动匹配 para obtener/asociar stock apto. Los detalles asociados pendientes de inspección generan la carga de órdenes.
2. La cuenta receptora se configura en el servidor; por defecto, `my9281`. La tablet solo lee órdenes del usuario conectado con indicador de revisión 0. Verificar cuenta y operador.
3. Guardar los cambios del interruptor de finalización y esperar confirmación. Finalización y revisión de ubicación son campos distintos. Los datos de inspección siguen su SOP.
4. Seguir el [SOP de ubicación](Ubicacion-SOP.md) para inspección/reacondicionamiento, etiquetas, pares SKU/SN y preparación de pallets. “SKU / SN 扫描” es otro módulo; una lista de solo SN no es un lote de pares.
5. La carga definitiva archiva los registros correspondientes y actualiza la revisión de órdenes del ciclo; no elimina físicamente el historial. No cargar anticipadamente solo para retirar un pallet pendiente de la lista.

## 7. Incidencias

| Caso | Acción |
| --- | --- |
| SN duplicado | Revisar si ya se registró/cargó durante esta ejecución. Para cambiar de pallet, sección 5; cambiar la casilla no basta. |
| SN, pallet o guía incorrectos | Detenerse y conservar pantalla y comprobación física. No existe edición/eliminación individual. Pedir un plan controlado de corrección; no perder todo un lote por un error. |
| Sin red o tiempo agotado | Mantener la aplicación abierta y reintentar los registros originales pendientes; no generar nuevos escaneos. |
| Error ya cargado | Revisar la asignación del servidor. Un SN real puede corregirse con las reglas de traslado; no hay borrado de códigos ajenos a equipos confirmado para operadores. Consultar mantenimiento. |
| Aplicación terminada o reinicio | Actualizar WPF y comprobar qué llegó; contar físicamente lo que falta. No se garantiza recuperar pendientes de memoria. |
| WPF muestra el pallet anterior | Revisar actualización, entorno, horas, concurrencia y relojes. No cambiar reiteradamente la hora y volver a escanear. |
| Orden con ubicación antigua | Registrar origen/destino e ID de orden y avisar al responsable. No usar finalización, revisión ni tareas duplicadas como corrección. |
| Cambio de estantería de equipo ya ubicado | No reingresarlo al área con este SOP. Solicitar el procedimiento específico. |

## 8. Comprobación final

- [ ] Cada SN físico está en el pallet correcto; cero pendientes y WPF actualizado y verificado.
- [ ] Origen, destino y total coinciden; no hay SN activos sobrantes, ausentes ni duplicados.
- [ ] Diferencias de ubicación en órdenes existentes registradas y entregadas, sin asumir sincronización automática.
- [ ] Cargas sin confirmar y diferencias tienen responsable; no declarar terminado un movimiento sin resolver.
- [ ] Registros conservados antes de cerrar, cambiar de turno o actualizar.

## 9. Reglas de almacén pendientes

| Tema | Definiciones pendientes |
| --- | --- |
| Admisión/salida | Estados permitidos, autorización, separación de pendientes y aprobados |
| Autorización/entrega | Solicitante, aprobador, transportista interno y revisor; doble revisión, formato y conservación |
| Pallets y ubicaciones | Asignación/reutilización, evitar números repetidos, correspondencia física y movimientos de pallet completo |
| Movimientos con orden existente | Quién cambia `location`, avisa a inspección y evita duplicados; el PDA no lo hace automáticamente |
| Stock ya ubicado | Traslados entre ubicaciones, división/unión/pallet completo e historial |
| Correcciones de escaneo | Reconstrucción controlada sin edición individual, conteo físico y códigos erróneos ya cargados |
| Reingreso por reparación | Aprobación de ciclo nuevo y enlace con órdenes/ubicación anteriores; no confundir con cambio normal de pallet |
| Capacidad y seguridad | Peso por modelo, apilado, mezcla de modelos y equipos dañados |

Referencias de código: NativeScanPage, PalletScanSession, PalletScanApiService, PalletScanRepository y registros de despliegue con conservación de datos. Solo se documenta; no se añadió una función de traslado ni se probó un traslado en almacén.
