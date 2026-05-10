# Integration Events

Los integration events permiten la comunicación asíncrona entre microservicios.

## UserCreatedIntegrationEvent

Publicado por **users-ms** cuando se crea un nuevo usuario. El microservicio
**notifications-ms** está subscripto a este evento para enviar el email de bienvenida.

### Campos

- `UserId`: identificador único del usuario creado
- `Email`: dirección de correo del nuevo usuario
- `CreatedAt`: timestamp de la creación

## OrderCreatedIntegrationEvent

Publicado por **orders-ms** cuando se confirma un pedido.
El microservicio **shipping-ms** lo consume para iniciar el proceso de envío.
