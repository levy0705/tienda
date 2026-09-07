# Fase 11: pruebas y piloto real

Esta fase valida el comportamiento de la aplicación con datos de una tienda pequeña antes de usarla como sistema principal. Las pruebas deben ejecutarse en un teléfono de prueba o con una copia de seguridad disponible; no se recomienda comenzar con la información real sin haber probado primero la restauración.

## Preparación del escenario

1. Instalar la versión candidata y completar el inicio con el administrador.
2. Crear una categoría y dos productos:
   - Producto A: costo `$100`, venta `$200`, existencia mínima `2`, existencia inicial `10`.
   - Producto B: costo `$50`, venta `$100`, existencia `0`.
3. Crear un proveedor, un cliente de crédito y usuarios de los roles Administrador, Encargado, Cajero y Bodega.
4. Cambiar el PIN temporal del administrador y crear un respaldo cifrado. Compartirlo fuera del teléfono.
5. Anotar el saldo inicial y conservar capturas o comprobantes de cada operación.

## Casos funcionales

| ID | Escenario | Pasos resumidos | Resultado esperado |
|---|---|---|---|
| F11-01 | Compra incrementa inventario | Crear compra de 5 unidades de A, recibirla y consultar inventario | Existencia `10 → 15`, costo actualizado y movimiento de entrada asociado |
| F11-02 | Venta reduce inventario | Abrir caja, vender 2 unidades de A en efectivo y confirmar | Existencia `15 → 13`, movimiento de salida, venta y cobro registrados |
| F11-03 | Venta de contado | Registrar una venta pagada en efectivo y otra con tarjeta | Cada pago queda con su medio; el efectivo solo aumenta por la parte en efectivo |
| F11-04 | Venta a crédito | Registrar una venta total o parcialmente a crédito para el cliente | La venta queda registrada, se crea o actualiza el crédito y solo lo cobrado entra en caja |
| F11-05 | Abono actualiza crédito y caja | Registrar un abono en una caja abierta y generar comprobante | El saldo pendiente disminuye, el abono queda en el historial y la caja recibe el efectivo |
| F11-06 | Gasto afecta el cierre | Registrar un gasto en efectivo con categoría y comprobante opcional | El gasto queda asociado a la sesión y reduce el efectivo esperado |
| F11-07 | Anulación o devolución | Anular/devolver una venta autorizada | Inventario restituido, reembolso trazable, estado actualizado y auditoría registrada |
| F11-08 | Diferencia de caja | Abrir con monto conocido, hacer movimientos, contar un valor diferente y cerrar | El cierre conserva esperado, contado, diferencia, medios, gastos, retiros y observaciones |
| F11-09 | Producto sin existencias | Intentar vender más unidades de B que las disponibles | La operación se bloquea sin venta, cobro ni movimiento parcial |
| F11-10 | Escaneo repetido | Escanear el QR de A, agregarlo; repetir la lectura y agregarlo de nuevo | El mismo producto se identifica correctamente y la cantidad se acumula sin duplicar la línea |
| F11-11 | Cierre inesperado | Registrar una operación, cerrar la aplicación y volver a abrirla | La última operación confirmada permanece; una operación no confirmada no aparece como registrada |
| F11-12 | Restauración de respaldo | Crear datos de prueba, restaurar el respaldo compartido y volver a iniciar sesión | Regresan productos, movimientos, configuración e imágenes; se conserva la copia preventiva |
| F11-13 | Actualización sin pérdida | Instalar una versión posterior sobre una instalación con datos | Las migraciones conservan productos, ventas, inventario, caja, créditos, usuarios y auditoría |

Las operaciones compuestas usan una transacción local común: si la aplicación se cierra durante una venta, recepción, abono, gasto o anulación, SQLite revierte el conjunto incompleto al volver a abrir.

## Validación de permisos

- Cajero: puede vender, cobrar, registrar gastos y consultar reportes operativos, pero no ver costos, ajustar inventario ni administrar usuarios.
- Bodega: puede consultar y ajustar inventario, pero no vender, abrir/cerrar caja ni ver costos.
- Encargado: puede operar y crear/restaurar respaldos, pero no administrar usuarios.
- Administrador: puede ejecutar todas las acciones y cambiar los PIN.
- Intentar cada acción restringida desde la interfaz y confirmar que el servicio también la rechaza aunque se invoque desde otra pantalla.

## Criterios de aceptación

- No hay diferencias entre los movimientos trazables y las existencias mostradas.
- Cada venta, compra, abono, gasto y cierre puede explicarse desde su historial.
- Las operaciones fallidas no dejan cobros, movimientos o créditos a medias.
- Un respaldo compartido fuera del teléfono puede validarse y restaurarse.
- La actualización de versión no elimina ni reinicia datos existentes.
- El piloto se considera listo cuando todos los casos críticos F11-01 a F11-13 pasan en dos jornadas consecutivas.

## Piloto real sugerido

1. Usar la app primero durante una jornada con una selección limitada de productos.
2. Mantener el sistema anterior como respaldo durante los primeros días.
3. Realizar un cierre diario y compartir un respaldo al final de cada jornada.
4. Registrar incidencias con fecha, usuario, operación, resultado esperado y resultado observado.
5. Revisar al final de la semana ventas, inventario, créditos, gastos, cierres y respaldos con el responsable del negocio.

La aplicación es local: las pruebas de pérdida del teléfono solo son satisfactorias si el archivo cifrado se conserva realmente fuera del dispositivo.
