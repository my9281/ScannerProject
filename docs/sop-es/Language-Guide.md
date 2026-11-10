# Guía multilingüe de lectura de SOP

[Índice en español](README.md) | [中文指引](../sop/Language-Guide.md) | [English guide](../sop-en/Language-Guide.md) | [Índice de documentación](../README.md)

Actualizado: 2026-11-09.

## 1. Abrir el procedimiento correcto

1. Web: iniciar sesión y abrir **我的账户 → 操作文档与 SOP** (mi cuenta → documentos operativos y SOP). Tablet Checklist: **工作台 → 操作文档** (área de trabajo → documentos). Se necesita permiso `checklist.read`.
2. Elegir [中文](../sop/README.md), [English](../sop-en/README.md) o [Español](README.md), y después la tarea. La barra lateral Web también agrupa los SOP por idioma.
3. Los enlaces 中文 / English / Español de cada SOP abren **el mismo procedimiento** en otro idioma. “Volver al índice” lleva a la lista del idioma actual.
4. En tablet, cambiar mediante enlaces del documento. Al actualizar el directorio vuelve a seleccionarse el índice chino predeterminado; elegir de nuevo el idioma deseado.

## 2. Idioma del documento y de la aplicación

Cambiar de documento no cambia los botones. AndroidTester PDA permite English / Español en el inicio; inglés es el predeterminado y se recuerda la selección. Las traducciones conservan los nombres chinos de Web, WPF, Checklist y after-sales con explicación. Esto no significa que todos esos sistemas tengan selector de idioma.

| Referencia china | Significado inglés | Explicación en español |
| --- | --- | --- |
| 良品区扫描 | Scan good stock | Escanear stock apto |
| 记录 | Record | Registrar |
| 上传 | Upload | Cargar al servidor |
| 完成（PDA） | Done: return, not upload | Volver; no carga datos |
| 入库检测 | Receiving inspection | Inspección de recepción |
| 检测通过 | Inspection passed | Inspección aprobada |
| 维修处理 | Repair processing | Tratamiento de reparación |
| 翻新处理 | Refurbishment | Reacondicionamiento |
| 良品上架 | Good-stock putaway | Ubicación de stock apto |
| 详情 / 打印 | Details / Print | Detalles / Imprimir |
| 开始出库 / 完成出库 | Start / Complete outbound | Iniciar / Completar salida |

Las expresiones españolas explican el significado; no se garantiza que sean el texto literal del botón en cada versión. Identificar controles con el nombre chino conservado y la captura. No traducir códigos SN, SKU, RMA, OMS, WPF, INB o UNB. Conservar identificadores completos y ceros iniciales.

## 3. Capturas, anexos y ejemplos

Los tres idiomas comparten capturas y Excel del directorio chino. Los enlaces abren los mismos archivos; el texto chino dentro de imágenes no se traduce automáticamente. La explicación junto al nombre del control ayuda a localizarlo. Fechas, SKU, cantidades, costes y clientes de las imágenes son ejemplos. Usar datos completos de la orden actual, fechas/cantidades reales y fotos originales; no subir imágenes censuradas de documentación como comprobantes.

El lector en línea utiliza Markdown. Los Word de recepción existentes son archivos independientes; esta actualización mantiene Markdown. El lector no ofrece descargas Word. Los Excel se descargan por enlaces protegidos; en Checklist, guardar/abrir mediante el menú para compartir. Los enlaces a código y archivos no publicados son solo para consulta en el repositorio.

## 4. Revisar el estado antes de actuar

- Recepción, área de stock apto, ubicación y retorno por reparación tienen contenido. El área sigue pendiente de validación en almacén; ubicación termina en **entrega pendiente de confirmación**, no en ubicación definitiva.
- Pruebas de paneles solares, descarte, cierre mensual y anual solo tienen marcadores en chino. Sus traducciones siguen pendientes hasta definir las reglas.
- “Documentado” significa que hay contenido, no aprobación ni validación en almacén. Traducir no concede permisos ni convierte dudas en reglas aprobadas.
- Cambiar de pallet dentro del área, trasladar stock ya ubicado, cargar desde PDA, completar una orden y ubicar definitivamente son acciones distintas. No sustituir unas por otras. Una etiqueta que solo dice OK no acredita inspección aprobada.

## 5. Diferencias y problemas de acceso

Ante ambigüedad, comparar con la fuente china enlazada y consultar al responsable. Pausar el paso afectado si hay dudas sobre SN, SKU, decisiones, fechas, unidades o envíos del formulario; registrar sección y diferencia sin adivinar. Al cambiar el original, actualizar ambas traducciones, estados de los tres índices y enlaces.

Si no abre, revisar sesión y permisos, actualizar directorio y reintentar. Nuevos documentos, imágenes o anexos requieren publicar de nuevo `Documentation/` de Scanner.Web; guardar cambios en el repositorio no actualiza el servicio activo. Véase [lectura y publicación — chino](../文档浏览与发布.md).
