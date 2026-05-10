# Autenticación

El sistema de autenticación está dividido en dos microservicios según el tipo de cliente.

## Legacy Auth Service

Maneja la autenticación de clientes legacy mediante usuario y contraseña.
Emite tokens JWT compatibles con el sistema anterior.

## Accounts Service

Gestiona la autenticación de clientes nuevos con soporte OAuth2 y SSO.
Integrado con proveedores externos como Google y Microsoft.

## Flujo de autenticación

1. El cliente envía credenciales al gateway
2. El gateway determina si es cliente legacy o nuevo
3. Delega al servicio correspondiente
4. Ambos servicios devuelven un JWT con el mismo formato
