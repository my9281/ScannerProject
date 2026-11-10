# Procedimiento de recepción de mercancía

[中文](../sop/收货SOP.md) | [English](../sop-en/Receiving-SOP.md) | [Español](../sop-es/Recepcion-SOP.md)

[Volver al índice](README.md) | [Guía multilingüe](Language-Guide.md)

Revisión de traducción/índice: 2026-11-09. Se conserva el estado del proceso; las reglas pendientes siguen sin confirmar.

Este procedimiento está dirigido al responsable de recepción. Su objetivo es registrar la mercancía recibida en la hoja compartida **“美国Logistar仓收件登记表-更新版9.02”**. El registro de recepción y el ingreso al inventario son procesos independientes; este documento describe el registro de recepción.

Los nombres de botones, archivos y pestañas se conservan en chino para facilitar su identificación en el sistema.

## 1. Exportar los avisos de llegada y las órdenes de trabajo

Antes de iniciar la recepción, exportar los avisos de llegada y las órdenes de trabajo desde el sistema de posventa de BLUETTI y preparar la documentación necesaria.

## 2. Anotar la fecha y crear un registro de recepción

Al comenzar la recepción del día, anotar la fecha en la **columna A**, al inicio de los registros correspondientes a ese día en la hoja compartida.

En WPF, abrir “扫码录单” (registro mediante escaneo) y hacer clic en **“+ 创建新记录”** (crear un registro nuevo), para evitar mezclar los datos con registros anteriores.

**Antes de escanear, crear los archivos TXT y Excel de esta recepción. Después, escanear el número de guía.**

## 3. Escanear la guía, abrir las cajas y escanear los números de serie

### Recepción de un pallet completo

La recepción de pallets completos es poco frecuente. Aplicar estas instrucciones adicionales cuando llegue uno. Es posible que todo el pallet tenga un solo comprobante de entrega y una sola guía, grapada a una de las cajas. Localizar la guía y confirmar el número de envío del pallet; no omitirlo porque las demás cajas no tengan una guía individual.

Por ejemplo, el número de guía del pallet de referencia es **439752659**, de ArcBest; en la etiqueta también aparece como “439 752 659”. Una vez creados los archivos TXT y Excel, escanear primero este número y después continuar con la apertura de las cajas y el escaneo de los números de serie.

### Desarmar el pallet y retirar el material de embalaje

Después de escanear la guía del pallet, desarmarlo y conservar los documentos en papel, incluidos el comprobante de entrega y la guía. Tirar de inmediato la película estirable retirada al bote de basura. Este material ocupa mucho espacio; no acumularlo en el área de recepción.

### Secuencia de escaneo

Para un pallet completo, escanear primero la guía y desarmar el pallet según los pasos anteriores; después, procesar cada equipo por separado. Para los paquetes que lleguen individualmente, escanear primero el número de guía de la caja y continuar con estos pasos:

1. Abrir la caja del equipo.
2. Sacar el equipo de la caja.
3. Escanear el número de serie (SN) que aparece en el equipo.
4. Cuando el sistema identifique el modelo, imprimirá automáticamente la etiqueta.
5. Si el sistema no identifica el modelo, seleccionar el modelo correcto en la ventana emergente y confirmar. Si no aparece en la lista, introducir el modelo nuevo y utilizarlo.
6. Después de escanear cada equipo e imprimir sus etiquetas, **colocar una etiqueta dentro de la caja y pegar otra en el exterior**. Verificar que ambas correspondan al equipo.

La selección de la ventana emergente corresponde al **modelo del equipo**, no al “退件类型” (tipo de devolución) de la hoja compartida.

WPF guarda automáticamente los registros en Excel, en tres columnas: modelo, SN y OID (número de guía).

Durante la recepción, registrar también lo siguiente en la **columna A** de la hoja compartida:

- Los modelos **EL10, EL100 y EB3A** tienen distintos colores. Anotar el color real en la fila correspondiente.
- Para los **cables**, tomar una fotografía y subirla a la columna A de la fila correspondiente.

Si se seleccionó un modelo incorrecto, corregirlo en la fila correspondiente del Excel antes de copiar los datos. Actualmente, WPF no permite editar registros ya guardados; volver a escanear el mismo SN no modifica el registro original de Excel. Corregir el Excel tampoco cambia las etiquetas ya impresas; las etiquetas incorrectas deben corregirse por separado.

## 4. Exportar el Excel y copiar los registros por número de guía

Una vez escaneados los SN de todos los equipos de esta recepción, exportar el Excel correspondiente. WPF guarda automáticamente los datos en el Excel de escaneo; hacer clic en **“打开扫描 Excel”** (abrir Excel de escaneo) para abrir el registro actual.

Seleccionar los registros correspondientes a cada número de guía y copiarlos a la hoja compartida:

- **Guías de FedEx:** pestaña “eBay&无源头退货登记表2025”.
- **Guías de otros transportistas o canales:** pestaña “亚马逊收件登记表2025”.

Copiar únicamente las filas de datos, sin los encabezados. Usar esta correspondencia de columnas:

| Excel de escaneo | Hoja compartida |
| --- | --- |
| Columna A: modelo | Columna B: modelo |
| Columna B: SN | Columna C: SN |
| Columna C: OID | Columna D: número de guía |

Pegar los SN y los números de guía como texto, conservando todos sus caracteres para evitar que los números largos se trunquen o se modifiquen.

## 5. Verificar que el registro esté completo

Confirmar que los registros estén en la pestaña correcta, que las columnas de modelo, SN y número de guía correspondan entre sí y que la hoja compartida esté guardada.

El responsable copia las tres columnas del Excel de escaneo a las columnas B–D de la hoja compartida y registra en la columna A la fecha, los colores de los modelos indicados y las fotografías de los cables. **Las demás columnas las completan los compañeros en China.** Al copiar, no sobrescribir el contenido de la columna A ni las columnas que mantienen esos compañeros.

## 6. Trasladar los equipos al área de espera de reparación

Una vez completados el escaneo, la exportación del Excel y el registro de esta recepción, trasladar los equipos al área de espera de reparación.

## Referencia

- [Procedimiento original en chino](../sop/收货SOP.md).
