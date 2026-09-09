# Tienda MVP

Base de una aplicación .NET MAUI para administrar una tienda física de forma local.

## Estado de la Fase 1

- Navegación con Inicio, Vender, Inventario, Caja y Más.
- Persistencia local SQLite en el directorio de datos de la aplicación.
- Inicialización idempotente con versionado de esquema.
- Usuario administrador local inicial.
- Repositorios y servicios registrados mediante inyección de dependencias.
- Servicio para almacenar imágenes de productos en archivos locales.
- Servicio de auditoría.
- Servicio común para mensajes y confirmaciones.
- Utilidades de búsqueda que ignoran mayúsculas y acentos.
- Pantallas base listas para conectar los siguientes módulos.

## Ejecutar

El proyecto requiere el SDK de .NET 10 y la carga de trabajo .NET MAUI.

```powershell
dotnet restore TiendaMvp/TiendaMvp.csproj --configfile NuGet.Config
dotnet build TiendaMvp/TiendaMvp.csproj -f net10.0-windows10.0.19041.0
```

Para Android se puede compilar con:

```powershell
dotnet build TiendaMvp/TiendaMvp.csproj -f net10.0-android
```

El usuario administrador inicial se crea con el PIN temporal `1234`. Debe cambiarse cuando se implemente la pantalla de configuración de usuarios.

## Estructura

- `Core/Entities`: entidades persistidas localmente.
- `Core/Services`: base de datos, repositorios y servicios transversales.
- `Core/Utilities`: utilidades reutilizables.
- `Pages`: pantallas de navegación de la aplicación.
- `Platforms`: configuración específica de cada plataforma.

## Estado de la Fase 2

- Alta y edición de productos con nombre, descripción, precios, unidad, impuesto, existencia mínima y fotografía.
- Activación y desactivación de productos.
- Código interno generado automáticamente con formato `PROD-0001`, sin permitir duplicados.
- Categorías con alta, edición y activación/desactivación.
- Protección contra eliminación de categorías con productos asociados.
- Búsqueda por nombre, código interno o QR y filtro por categoría.
- Identificador QR estable por producto, sin precios ni existencias embebidos.
- Vista, guardado local y compartir del QR en formato SVG.
- Escáner QR con cámara y apertura de la ficha del producto encontrado.
- Auditoría básica para altas, cambios, activaciones y desactivaciones.

## Estado de la Fase 3

- Movimientos trazables para inventario inicial, compras, ventas, devoluciones, ajustes, daños, pérdidas y conteos.
- `Product.Stock` se actualiza únicamente desde `InventoryService` dentro de una transacción local.
- Bloqueo de cantidades inválidas y de existencias negativas.
- Ajustes con motivo obligatorio.
- Conteo físico con diferencia calculada antes de guardar.
- Historial de movimientos por producto o general.
- Alertas de existencias mínimas en Inicio e Inventario.
- Valor estimado del inventario según costo de compra.

## Estado de la Fase 4

- Proveedores con contacto, identificación fiscal, dirección, observaciones y estado activo/inactivo.
- Resumen por proveedor de compras, productos relacionados y saldo pendiente.
- Compras en borrador con productos seleccionados o agregados mediante QR.
- Cálculo de subtotal, descuentos, costos adicionales, total y saldo por pagar.
- Recepción de compras con actualización transaccional de inventario y movimiento de entrada asociado.
- Costo promedio ponderado y último costo de compra actualizados al recibir mercancía.
- Abonos a compras pendientes y consulta del historial por proveedor.
- Anulación controlada: las compras recibidas generan devoluciones a proveedor y no se permiten anulaciones con pagos registrados.
- Estados: Borrador, Recibida, Pendiente de pago, Pagada y Anulada.

## Estado de la Fase 5

- Clientes con documento, contacto, dirección, notas, estado activo/bloqueado y límite de crédito opcional.
- Cliente general para ventas sin identificación.
- Ventas con pago total, pago parcial o crédito total.
- Crédito vinculado a la venta con fecha de creación, vencimiento, saldo original y saldo actual.
- Estados de cartera vigentes, vencidos y pagados, calculados con base en la fecha de vencimiento.
- Abonos con historial, autorización de ajustes y comprobante local compartible.
- Bloqueo de nuevos créditos para clientes bloqueados o fuera de límite.
- Caja local con apertura, movimientos, cierre y monto esperado.
- Regla contable aplicada: la venta total se registra, pero solo el dinero cobrado entra en caja; los abonos posteriores se asocian a la caja abierta.

## Estado de la Fase 6

- Punto de venta funcional con búsqueda, escaneo QR, alta y retiro de líneas y cambio de cantidades.
- Métodos de pago en efectivo, tarjeta, transferencia y otro, incluyendo pagos mixtos.
- Cálculo de cambio a partir del efectivo recibido.
- Descuentos con límite del 20 % para operadores y autorización ampliada para administradores.
- Validación de caja abierta, existencias, cantidades, crédito y doble confirmación.
- Comprobante local de venta compartible.
- Historial de ventas con detalle, comprobante y anulación/devolución de ventas de contado.
- La devolución restituye inventario y registra el reembolso como egreso de la caja abierta.

## Estado de la Fase 7

- Sesiones de caja independientes por usuario, con apertura y monto inicial.
- Entradas extraordinarias, retiros, ventas cobradas, abonos y gastos asociados a la sesión.
- Gastos con concepto, categoría, fecha, medio de pago, responsable y comprobante fotográfico opcional.
- Consulta de movimientos, efectivo esperado y totales cobrados por medio de pago.
- Arqueo y cierre con monto contado, diferencia explicada, observaciones y confirmación mediante PIN.
- Historial local de cierres con esperado, contado, diferencia y observaciones.
- El efectivo esperado solo considera movimientos en efectivo; tarjeta, transferencia y otros medios se muestran por separado.

## Estado de la Fase 8

- Inicio con ventas, cobros, gastos, efectivo esperado, cartera, productos agotados y existencias bajas.
- Productos más vendidos del día y accesos rápidos a venta, caja, inventario y reportes.
- Consulta en pantalla de ventas por periodo y producto, utilidad bruta estimada, inventario, valor del inventario y movimientos.
- Reportes de compras por proveedor, gastos por categoría, cierres de caja, créditos pendientes/vencidos, abonos y bajas existencias.
- Filtros por fecha para los reportes que dependen de un periodo.
- Exportación a archivo pendiente para una iteración posterior.

## Estado de la Fase 9

- Inicio local mediante selección de usuario y PIN, sin cuentas sincronizadas.
- Cambio de usuario desde la aplicación y registro de último acceso.
- Alta, edición, bloqueo y activación de usuarios.
- Cambio de PIN propio y PIN inicial obligatorio para nuevos usuarios.
- Roles iniciales: Administrador, Encargado, Cajero y Bodega.
- Permisos centralizados para costos/utilidad, precios, descuentos, anulaciones, inventario, gastos, caja, créditos, abonos, reportes, usuarios y respaldos.
- Validaciones aplicadas en las operaciones sensibles y auditoría de inicios de sesión y cambios de usuarios.
- Migración de base de datos al esquema 9 para conservar el último acceso.

## Estado de la Fase 10

- Copias manuales cifradas en archivos `.tbackup` protegidos con un PIN de administrador.
- El respaldo incluye la base de datos local, la configuración de la tienda y las imágenes de productos.
- Consulta de fecha, tamaño y tipo de cada copia almacenada en el teléfono.
- Compartir el archivo mediante el selector del sistema para guardarlo fuera del teléfono.
- Validación previa de formato, cifrado, manifiesto, esquema, configuración, base de datos, imágenes y rutas antes de restaurar.
- PIN de administrador obligatorio para validar y restaurar.
- La restauración utiliza el PIN del administrador que protegió la copia, aunque posteriormente se haya cambiado en el teléfono.
- Copia preventiva automática antes de reemplazar los datos actuales.
- Restauración controlada de base de datos e imágenes y cierre de sesión posterior.
- Recordatorio visible cuando no existe un respaldo reciente.
- La recuperación ante pérdida del teléfono depende de que el usuario conserve una copia fuera del dispositivo.

## Estado de la Fase 11

- Matriz de pruebas funcionales para compras, ventas, créditos, abonos, gastos, anulaciones, cierres, existencias, QR, cierre inesperado, restauración y actualización.
- Preparación de datos de prueba y validación de permisos por rol.
- Criterios de aceptación para consistencia de inventario, caja, cartera, auditoría y respaldos.
- Guía de piloto real con operación limitada, cierre diario, respaldo externo e identificación de incidencias.
- Transacciones locales compuestas para que ventas, compras, abonos, gastos, cierres y anulaciones no queden aplicados parcialmente ante un cierre inesperado.
