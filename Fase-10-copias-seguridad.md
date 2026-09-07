# Fase 10: copias de seguridad

La aplicación permite proteger la información local de la tienda sin depender todavía de un servidor.

## Copia manual

- Desde `Más > Copias de seguridad`, un administrador o encargado introduce el PIN de un administrador.
- Se genera un archivo `.tbackup` cifrado que incluye la base de datos SQLite, la configuración de la tienda y las imágenes de productos.
- Se muestra la fecha, el tamaño y el tipo de cada copia guardada en el teléfono.
- Después de crear una copia, la aplicación ofrece compartirla mediante el selector del sistema para guardarla en Drive, correo, mensajería u otro lugar fuera del teléfono.

## Validación y restauración

- Se puede seleccionar un archivo `.tbackup` desde el teléfono o restaurar una copia ya guardada.
- Antes de reemplazar los datos, el archivo se descifra y valida: formato, PIN, manifiesto, versión de esquema, base de datos, configuración, cantidad de imágenes y rutas seguras.
- La restauración exige el PIN del administrador que protegió la copia, incluso si posteriormente se cambió el PIN en el teléfono.
- Antes de restaurar se crea automáticamente una copia preventiva de la información actual.
- Las imágenes se preparan en una carpeta temporal y la base de datos se reemplaza de forma controlada.
- Al finalizar, se cierra la sesión y se solicita volver a iniciar sesión para trabajar con los datos restaurados.

## Protección y limitación

El archivo no es legible como una base de datos normal: se cifra con AES-GCM y una clave derivada del PIN mediante PBKDF2. Sin el PIN usado para protegerlo, no se puede abrir ni restaurar.

Las copias que permanecen únicamente dentro del teléfono no protegen frente a pérdida, daño o robo del dispositivo. La aplicación muestra un recordatorio cuando no hay una copia reciente, pero el usuario debe compartirla o guardarla fuera del teléfono.
