# Fase 9: usuarios y permisos

## Acceso local

La aplicación comienza en una pantalla de inicio de sesión. El usuario selecciona una persona activa, introduce su PIN y continúa al menú principal. Cambiar de usuario cierra la sesión actual y vuelve a solicitar el PIN. Todo se almacena en el teléfono; no hay cuentas sincronizadas.

## Usuarios

- Crear y editar usuarios.
- PIN inicial obligatorio y cambio de PIN propio.
- Activar o bloquear usuarios.
- Registrar la fecha y hora del último acceso.
- Evitar bloquear al usuario actual o dejar la aplicación sin un administrador activo.
- Auditar inicios de sesión y cambios de usuarios.

## Roles iniciales

- Administrador: todos los permisos.
- Encargado: operación completa y respaldos, excepto administración de usuarios.
- Cajero: ventas, descuentos, caja, créditos, abonos, gastos y reportes sin costos.
- Bodega: inventario y reportes operativos sin costos.

Los permisos se validan en los servicios de negocio, no únicamente en la interfaz. Así, una acción restringida sigue protegida aunque se invoque desde otra pantalla.
