# Fase 7: caja, cierres y gastos

La caja funciona localmente en el teléfono y conserva cada variación como un movimiento trazable.

## Caja

- Apertura con monto inicial y una sesión independiente por usuario.
- Ventas cobradas, abonos, entradas extraordinarias, retiros y gastos asociados a la sesión.
- El efectivo esperado se calcula como monto inicial más movimientos en efectivo. Los pagos con tarjeta, transferencia u otro medio se reportan aparte.

## Cierre

- Arqueo con total contado, total esperado y diferencia.
- Resumen de ventas, abonos, gastos, retiros, entradas extraordinarias, devoluciones y totales por medio de pago.
- Observaciones y confirmación mediante el PIN del usuario.
- Historial de cierres con los valores que explican la diferencia.

## Gastos

- Concepto, categoría, valor, fecha y medio de pago.
- Usuario responsable y asociación con la caja abierta.
- Comprobante fotográfico opcional almacenado en el dispositivo.
- Historial con búsqueda y filtros por categoría y medio de pago.

## Criterio de finalización

Al cerrar la caja, el usuario puede revisar el monto inicial, cada movimiento, el efectivo esperado, el total contado y la diferencia resultante.
