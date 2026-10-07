# HumanQuery — Preguntale a Northwind en castellano

**Sistemas de Datos II — ESCMB — Unidad 4: IA y Datos**

Escribís una pregunta como *"¿cuáles son los 3 países con más clientes?"* y recibís la respuesta redactada, junto con el SQL que se generó para obtenerla.

![Arquitectura](wwwroot/humanquery/Resumen%20IA%20POC%20.jpeg)

1. El usuario hace la consulta desde el navegador o desde un GPT.
2. El backend le manda a la IA la pregunta y la **estructura** de la base (tablas y columnas, sin datos).
3. La IA devuelve una sentencia SQL.
4. El backend ejecuta ese SQL en SQL Server.
5. La base devuelve los resultados crudos.
6. El backend le manda esos resultados a la IA, que los devuelve "humanizados".
7. El usuario recibe la respuesta.

> 🔎 **Para pensar:** en el paso 2 no se mandan datos… pero en el paso 6 sí. ¿Qué implica eso si la base tuviera datos sensibles?

---

## ✅ Lista rápida

Antes de empezar, necesitás estas 4 cosas. Abajo está el paso a paso de cada una.

- [ ] **.NET SDK 8** (o más nuevo) instalado
- [ ] **SQL Server con Northwind** corriendo (el contenedor de la Unidad 1)
- [ ] **Usuario de solo lectura** creado en Northwind
- [ ] **Clave de Gemini** (gratis) guardada en tu máquina

Tiempo estimado: 20 minutos la primera vez.

---

## 1. Instalar .NET

1. Descargá el **.NET SDK 8** (o uno más nuevo) desde <https://dotnet.microsoft.com/download>. Tiene que ser el **SDK**, no el *Runtime*.
2. Instalalo con las opciones por defecto.
3. **Cerrá y volvé a abrir la terminal** (PowerShell en Windows, Terminal en Mac).
4. Verificá:

```bash
dotnet --version
```

Tiene que mostrar un número como `8.0.x` o mayor.

---

## 2. Tener SQL Server con Northwind

Usamos el mismo contenedor de la Unidad 1, que ya trae Northwind.

**Si ya lo tenés creado**, solo encendelo:

```bash
docker start ESCMB-Datos2-MSSQL-container
```

**Si no lo tenés**, crealo (con Docker Desktop abierto):

```bash
docker run -d --name ESCMB-Datos2-MSSQL-container -p 1433:1433 fernandobono/escmb-datos2-sql-server:v1
```

> En Mac con chip Apple (M1 a M4), si Docker pregunta por la plataforma, agregá `--platform linux/amd64` después de `-d`.

Datos de conexión del contenedor: servidor `localhost,1433`, usuario `sa`, contraseña `TuPasswordSegura123!`.

---

## 3. Crear el usuario de solo lectura

La aplicación ejecuta SQL que **escribió una IA**. Si la IA se equivoca y genera un `DELETE` o un `UPDATE`, no queremos que se ejecute. Por eso la aplicación se conecta con un usuario que **solo puede leer**.

1. Conectate al SQL Server como `sa` con tu cliente de siempre (SSMS, Azure Data Studio o DBeaver).
2. Abrí el archivo [`sql/crear-usuario-lectura.sql`](sql/crear-usuario-lectura.sql) de este repositorio.
3. Ejecutalo completo.

Esto crea el usuario `ia_lector` (contraseña `IaLector#2026`) con permiso de solo lectura sobre Northwind. Es el usuario que ya figura en `appsettings.json`.

---

## 4. Sacar la clave de Gemini (gratis)

### ¿Qué es y por qué la necesito?

La aplicación necesita una IA para dos cosas: escribir el SQL y redactar la respuesta. Usamos **Gemini**, la IA de Google, a través de su API (una forma de que un programa le haga preguntas).

Para usar la API, Google pide una **clave** (*API key*). Es como una contraseña que identifica a tu aplicación.

- **Es gratis.** No pide tarjeta de crédito.
- **Tiene límites:** una cantidad máxima de consultas por minuto y por día. Para practicar y para la clase alcanza de sobra.
- **Solo necesitás una cuenta de Google** (la de Gmail sirve).

### Paso a paso

1. Entrá a **<https://aistudio.google.com/apikey>** e iniciá sesión con tu cuenta de Google.
2. La primera vez, Google te pide **aceptar los términos de uso**. Aceptalos y continuá.
3. Tocá el botón **"Create API key"** (Crear clave de API).
4. Si te pregunta en qué **proyecto** crearla, elegí uno existente o dejá que cree uno nuevo. Un proyecto es solo una "carpeta" de Google donde queda guardada la clave; no hace falta configurar nada más.
5. Aparece la clave: un texto largo de letras y números. Tocá **copiar**.

¡Listo! Ya tenés tu clave. Si la perdés, podés volver a la misma página, verla o crear otra.

### ⚠️ Cuidados con la clave

- **Es personal.** No la compartas ni la mandes por el grupo del curso.
- **Nunca la subas a GitHub** ni la pegues dentro del código. Si alguien la consigue, puede usar tu cuota.
- Si creés que alguien la vio, borrala desde la misma página y creá una nueva.
- En el plan gratis, según los términos de Google, lo que le enviás a Gemini puede usarse para mejorar sus productos. **No le mandes datos personales o sensibles reales.** Northwind es una base de ejemplo, así que no hay problema.

### Guardar la clave en tu máquina

En la terminal, **parado en la carpeta del proyecto** (donde está `HumanQuery.csproj`), ejecutá:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "PEGÁ_ACÁ_TU_CLAVE"
```

Reemplazá `PEGÁ_ACÁ_TU_CLAVE` por la clave que copiaste, dejando las comillas.

Esto guarda la clave en tu usuario del sistema, **fuera** del proyecto. Así nunca termina subida a GitHub por accidente.

> ¿Por qué dice `OpenAI` si usamos Gemini? Porque el código habla el formato de la API de OpenAI, que es un estándar que Gemini también entiende. Ver [Sobre la IA](#sobre-la-ia).

---

## 5. Correr la aplicación

En la carpeta del proyecto:

```bash
dotnet run
```

La primera vez tarda un poco más porque descarga las librerías. Cuando termina, se abre el navegador en **<http://localhost:5126/humanquery/>**. Para preguntar, tocá el botón 🤖 de abajo a la derecha: se abre el chat.

Probá con preguntas como:

- ¿Cuántos clientes hay?
- ¿Cuáles son los 5 productos más vendidos?
- ¿Qué empleado hizo más pedidos?
- ¿Cuántos pedidos hubo por año?

Para detenerla: `Ctrl + C` en la terminal.

### Páginas disponibles

| Página | Qué muestra |
|---|---|
| `/humanquery/` | Inicio. El botón 🤖 abre el chat para preguntar: muestra la respuesta humanizada |
| `/humanquery/monitor.html` | **El SQL generado, los datos crudos y todo lo que se le mandó a la IA en cada paso.** Mirala: ahí se ve el flujo completo por dentro |
| `/humanquery/learn.html` | Explicación del código |
| `/swagger` | La API, para probarla a mano |

---

## 6. (Opcional) Usarlo desde un GPT de ChatGPT

Un GPT puede llamar a esta API con una **Acción**. ChatGPT está en internet y tu aplicación en tu máquina, así que hace falta un túnel que le dé una dirección pública. Usamos [ngrok](https://ngrok.com/download) (gratis, pide crear una cuenta).

Con la aplicación corriendo, en otra terminal:

```bash
ngrok http 5126
```

1. Copiá la dirección `https://….ngrok-free.app` que muestra ngrok.
2. En tu GPT: **Configurar → Acciones → Crear nueva acción**.
3. Pegá el contenido de [`gpt/openapi-humanquery.yaml`](gpt/openapi-humanquery.yaml), reemplazando la `url` de `servers` por la dirección de ngrok.
4. Autenticación: **Ninguna**.

> ⚠️ Mientras el túnel está abierto, cualquiera con la dirección puede usar tu aplicación (y tu cuota de Gemini). Cerralo con `Ctrl + C` cuando termines. Crear un GPT requiere ChatGPT pago; usar uno ya creado, no.

---

## 🛠️ Problemas frecuentes

| Ves este error | Qué pasa | Qué hacer |
|---|---|---|
| `dotnet` no se reconoce como comando | .NET no está instalado o la terminal es vieja | Instalá el **SDK** y abrí una terminal nueva |
| `Login failed for user 'ia_lector'` | No se creó el usuario de solo lectura | Ejecutá `sql/crear-usuario-lectura.sql` como `sa` (paso 3) |
| `A network-related or instance-specific error…` | SQL Server no está corriendo | `docker start ESCMB-Datos2-MSSQL-container` |
| `API key not valid` o error 400/401 | La clave está mal copiada o no se guardó | Repetí el comando `dotnet user-secrets set …` del paso 4 |
| Error **429** | Llegaste al límite del plan gratis | Esperá un minuto y volvé a probar |
| Error **503** / *high demand* | Google está saturado en ese momento | La aplicación prueba sola otros modelos; si fallan todos, esperá un rato |
| `ningún modelo respondió` | Fallaron todos los modelos configurados | Esperá unos minutos. Si sigue, revisá en `appsettings.json` que los modelos de `FallbackModels` existan |
| `address already in use` / puerto 5126 ocupado | Otra aplicación usa ese puerto | `dotnet run --urls http://localhost:5200` y entrá por ese puerto |
| Responde algo raro o inventado | **La IA se equivocó** | Mirá el SQL generado y el monitor. ¿Era la consulta correcta? Esto es parte de lo que estudiamos 😉 |

Si tu SQL Server no está en `localhost,1433` (por ejemplo, una instalación local como `localhost\SQLEXPRESS`), cambiá `Data Source` en `DefaultConnection` dentro de `appsettings.json`.

---

## Sobre la IA

El código usa el formato de la API de OpenAI, que Gemini también entiende. Por eso, para cambiar de proveedor alcanza con modificar en `appsettings.json`:

- `OpenAI:BaseUrl` — la dirección del proveedor
- `OpenAI:Model` — el modelo principal
- la clave (con `dotnet user-secrets`)

Si un modelo está saturado o fue retirado, la aplicación prueba en orden los de `OpenAI:FallbackModels`.

## Estructura del proyecto

```
Controllers/HumanQueryController.cs   ← el flujo completo (pasos 2 a 7)
Controllers/MonitoringController.cs   ← historial para monitor.html
Services/OpenAiService.cs             ← llamada a la IA, con reintentos y modelos de respaldo
Services/QueryLogService.cs           ← guarda en memoria cada consulta
wwwroot/humanquery/                   ← páginas del navegador
sql/crear-usuario-lectura.sql         ← usuario de solo lectura
gpt/openapi-humanquery.yaml           ← esquema para la Acción del GPT
appsettings.json                      ← conexión a la base y configuración de la IA (sin claves)
```
