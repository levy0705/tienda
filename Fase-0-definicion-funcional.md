# Fase 0 · Definición funcional del MVP

**Estado:** borrador listo para validación del negocio  
**Alcance:** aplicación local en un teléfono, sin API ni sincronización entre dispositivos.

## 1. Decisiones de alcance

- El MVP funciona para una sola tienda y una sola moneda.
- La información se guarda localmente en el teléfono.
- Los usuarios son perfiles locales protegidos con PIN.
- El producto se controla por unidad, paquete o caja. No habrá conversiones entre unidades en el MVP.
- Un producto con talla, color o presentación diferente se registra como un producto independiente y recibe su propio QR.
- No se eliminarán operaciones confirmadas: se anulan o revierten conservando el historial.
- No se incluyen facturación electrónica, varias sucursales ni sincronización en la primera versión.

## 2. Reglas del negocio

### Productos y costo

- Cada producto tiene un identificador interno estable y un QR único.
- El QR identifica al producto; no contiene precio ni existencia.
- El precio de venta puede cambiar sin generar un QR nuevo.
- El costo se calcula con **promedio ponderado**: costo existente más costo de la nueva compra, dividido por las unidades resultantes.
- La utilidad bruta de una línea es: precio real de venta después del descuento menos costo promedio, multiplicado por la cantidad.
- El precio de venta no puede ser negativo ni inferior al costo sin una autorización del administrador.
- Las categorías se pueden desactivar, pero no eliminar si tienen productos asociados.

### Inventario

- Toda modificación de existencias genera un movimiento con producto, cantidad, motivo, usuario, fecha y documento relacionado.
- Por defecto no se permiten existencias negativas.
- Una venta sin existencia suficiente se bloquea y muestra la cantidad disponible.
- El administrador puede activar una opción de existencias negativas si el negocio la necesita.
- Las compras, ventas, devoluciones y ajustes son las únicas fuentes de cambios de inventario.

### Medios de pago

- Efectivo.
- Tarjeta.
- Transferencia.
- Pago mixto.
- Crédito de cliente.

Los pagos recibidos entran a la caja abierta. Una venta totalmente a crédito no incrementa el efectivo de caja; un abono posterior sí se registra en la caja en la que fue recibido.

### Descuentos

- Se permite descuento porcentual o de valor fijo.
- Puede aplicarse por línea o sobre el total.
- El descuento debe quedar registrado en la venta.
- El cajero no puede superar el límite configurado.
- El administrador o encargado puede autorizar descuentos mayores.
- El descuento nunca puede dejar el total por debajo de cero.

### Devoluciones y anulaciones

- Una devolución debe referenciar la venta original.
- Se puede devolver toda la venta o líneas y cantidades específicas.
- El producto vuelve al inventario solo si está en condiciones de venta.
- La devolución genera un movimiento de inventario y un movimiento de caja o crédito a favor.
- Una venta confirmada no se edita; se anula o se devuelve.
- Anular una venta requiere permiso de administrador o encargado y un motivo obligatorio.
- El plazo de devolución será configurable; se recomienda iniciar con 7 días y ajustarlo a la política real de la tienda.

### Créditos

- Un cliente puede tener varias ventas a crédito.
- Cada crédito registra fecha, vencimiento, valor original, saldo y estado.
- Se permite pago total o abono parcial.
- El sistema muestra créditos vigentes, vencidos y pagados.
- El límite de crédito es opcional y configurable por cliente.
- El negocio puede bloquear nuevos créditos para un cliente con saldo vencido.
- Un crédito parcialmente pagado se mantiene abierto hasta quedar en cero.

### Caja

- Solo puede haber una sesión de caja abierta en el dispositivo.
- La caja debe abrirse con un monto inicial.
- Ventas cobradas, abonos, gastos, entradas y retiros se registran en la sesión activa.
- No se puede cerrar la caja sin registrar el efectivo contado.
- El cierre muestra valor esperado, valor contado y diferencia.
- Una diferencia requiere observación y permiso de encargado o administrador.
- Una caja cerrada no se modifica; cualquier corrección se registra como movimiento posterior autorizado.

### Gastos

- Todo gasto tiene concepto, categoría, valor, fecha, medio de pago y usuario.
- Un gasto pagado desde efectivo afecta la caja activa.
- Un gasto pagado fuera de caja queda registrado para reportes, pero no altera el efectivo contado.
- Los gastos confirmados se anulan, no se borran.

### Copias de seguridad

- La copia incluye base de datos, imágenes de productos y configuración.
- Solo el administrador puede crear o restaurar una copia.
- Antes de restaurar se crea una copia preventiva del estado actual.
- El archivo se valida antes de reemplazar la información local.
- La aplicación debe mostrar cuándo se realizó el último respaldo.

## 3. Roles iniciales

| Rol | Acceso principal |
|---|---|
| Administrador | Todo el sistema, configuración, permisos y respaldos |
| Encargado | Compras, inventario, reportes, anulaciones y cierre |
| Cajero | Ventas, cobros, abonos y su caja |
| Bodega | Recepción, conteos y ajustes autorizados |

## 4. Flujos principales

### 4.1 Crear producto y generar QR

1. Usuario autorizado pulsa **Nuevo producto**.
2. Registra nombre, categoría, unidad, costo inicial, precio de venta y existencia inicial opcional.
3. El sistema valida campos obligatorios y unicidad del código interno.
4. Guarda el producto y genera un QR único.
5. Permite mostrar, compartir o imprimir la etiqueta.

**Resultado:** producto activo, QR disponible y movimiento inicial si se indicó existencia.

### 4.2 Registrar una compra y recibir mercancía

1. Usuario selecciona proveedor.
2. Crea una compra y agrega productos por búsqueda o escaneo.
3. Registra cantidades, costos y descuentos.
4. Marca la compra como recibida total o parcialmente.
5. El sistema incrementa existencias y recalcula el costo promedio.
6. Registra pago total o saldo pendiente al proveedor.

**Resultado:** compra trazable, inventario actualizado y cuenta del proveedor actualizada.

### 4.3 Ajustar inventario

1. Usuario busca o escanea el producto.
2. Indica cantidad contada o diferencia.
3. Selecciona motivo: daño, pérdida, sobrante, corrección u otro.
4. El sistema muestra existencia anterior y nueva.
5. Usuario autorizado confirma.

**Resultado:** un movimiento de ajuste con responsable y motivo.

### 4.4 Abrir caja

1. Usuario autorizado pulsa **Abrir caja**.
2. Registra monto inicial y observación opcional.
3. El sistema crea la sesión con fecha, hora y usuario.

**Resultado:** se habilitan ventas y movimientos asociados a esa sesión.

### 4.5 Realizar una venta

1. Cajero abre **Nueva venta**.
2. Agrega productos por búsqueda o escaneo QR.
3. Ajusta cantidades y aplica descuento si tiene permiso.
4. Selecciona cliente o cliente general.
5. Selecciona uno o varios medios de pago.
6. Si hay saldo a crédito, define vencimiento.
7. Confirma el cobro.

**Resultado:** venta registrada, inventario descontado, pagos aplicados y comprobante disponible.

### 4.6 Registrar un abono

1. Usuario busca al cliente y selecciona un crédito abierto.
2. Indica valor del abono y medio de pago.
3. El sistema valida que no supere el saldo.
4. Registra el abono en la caja activa si corresponde.
5. Actualiza saldo y estado del crédito.

### 4.7 Registrar un gasto

1. Usuario pulsa **Registrar gasto**.
2. Indica categoría, concepto, valor y medio de pago.
3. Si se paga en efectivo, se asocia a la caja abierta.
4. Confirma el movimiento.

### 4.8 Cerrar caja

1. Usuario selecciona **Cerrar caja**.
2. El sistema calcula totales por medio de pago y valor esperado.
3. Usuario registra efectivo contado.
4. El sistema calcula diferencia.
5. Usuario agrega observación si existe sobrante o faltante.
6. Usuario autorizado confirma el cierre.

**Resultado:** sesión cerrada, resumen disponible y movimientos bloqueados para edición directa.

### 4.9 Crear y restaurar copia

1. Administrador pulsa **Crear respaldo**.
2. La aplicación empaqueta la base de datos, imágenes y configuración.
3. El usuario guarda o comparte el archivo.
4. Para restaurar, selecciona un respaldo válido e introduce su PIN.
5. La aplicación crea una copia preventiva, valida el archivo y restaura.

## 5. Pantallas del MVP

- Inicio: ventas, caja, alertas y accesos rápidos.
- Productos: listado, búsqueda, alta, edición, detalle y QR.
- Categorías.
- Inventario: existencias, ajustes y movimientos.
- Ventas: carrito, escáner, cliente, pagos y comprobante.
- Caja: apertura, movimientos, gastos y cierre.
- Compras: listado, nueva compra, recepción y detalle.
- Proveedores.
- Clientes.
- Créditos y abonos.
- Gastos.
- Reportes esenciales.
- Usuarios y permisos.
- Copias de seguridad.

## 6. Criterios de aceptación de la Fase 0

- Los 11 flujos anteriores están escritos y validados.
- Las reglas de costo, descuentos, crédito, devoluciones y caja están aprobadas.
- Existe una lista inicial de productos, categorías, medios de pago y roles.
- El prototipo permite recorrer Inicio, Vender, Inventario y Caja.
- Quedan registradas las decisiones pendientes antes de comenzar el desarrollo.

## 7. Decisiones pendientes de validación

- Moneda e impuestos que usará la tienda.
- Política real de devoluciones y plazo permitido.
- Si se venderán productos por peso o volumen.
- Si las compras a crédito requieren fecha de vencimiento y abonos a proveedores en el MVP.
- Formato del comprobante de venta.
- Categorías y límites de descuento por rol.
- Si se requiere una contraseña maestra adicional al PIN.
- Ubicación recomendada para guardar copias: nube, computador o memoria externa.
